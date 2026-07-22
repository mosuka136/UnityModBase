using System;
using UnityEngine.InputSystem.LowLevel;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HotkeyManager;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 管理单个热键配置项从展开、等待按键、录制到等待确认的编辑状态机。
    /// 该类负责捕获输入和管理工作副本，不负责绘制界面；已确认的变更通过 <see cref="EntryChangeSink"/> 交由配置层写回。
    /// 录制期间会暂时禁用原热键及 <see cref="Hotkey.GlobalValid"/>，避免待录制输入触发游戏中的现有快捷键；
    /// 会话按轮次保存两个开关的进入值，并在确认、取消或清理时按快照恢复原热键对象和全局开关。
    /// 快照恢复不提供引用计数或所有权协调；实例应在 Unity GUI 主线程串行推进，同一时刻只能有一个录制会话，
    /// 且其他模块不应在录制期间改写上述开关，否则结束录制时可能覆盖期间发生的变更。
    /// 调用方必须在切换配置根、关闭编辑器或释放资源时调用 <see cref="CancelEdit"/> 或 <see cref="Dispose"/>，以结束未完成的录制并恢复外部状态。
    /// </summary>
    public class HotkeyEditSession : IDisposable
    {
        // 仅在 IsRecording 为 true 时表示本轮录制开始时的快照；恢复时不会合并录制期间的外部修改。
        private bool _globalValidBeforeEdit = true;
        private bool _originalValidBeforeEdit = true;

        /// <summary>
        /// 获取或设置当前正在编辑的配置项；null 表示没有会话。
        /// </summary>
        public IEntryBinding Entry { get; set; }
        /// <summary>
        /// 获取或设置录制状态机的当前阶段。
        /// </summary>
        public HotkeyEditState State { get; set; } = HotkeyEditState.Idle;
        /// <summary>
        /// 获取或设置本轮工作副本的基准热键引用，来源为开始编辑或上次确认后的当前有效值。
        /// 录制期间会临时将该对象的 <see cref="Hotkey.Valid"/> 设为 false；确认产生的新热键对象不复用此引用。
        /// </summary>
        public Hotkey OriginalValue { get; set; }
        /// <summary>
        /// 获取或设置供界面修改的热键副本；仅在确认得到非空变化时才作为新的热键对象写回。
        /// </summary>
        public Hotkey WorkingValue { get; set; }
        /// <summary>
        /// 获取或设置当前正在替换或新增的组合键对象。
        /// </summary>
        public HotkeyChord WorkingChord { get; set; }
        /// <summary>
        /// 获取或设置当前录制过程中最近保留的手柄组合候选。
        /// </summary>
        public GamepadChord PreviewGamepadChord { get; set; }
        /// <summary>
        /// 获取或设置当前录制过程中最近保留的键盘组合候选。
        /// </summary>
        public KeyboardChord PreviewKeyboardChord { get; set; }
        /// <summary>
        /// 指示会话是否已绑定配置项且不处于空闲状态。
        /// </summary>
        public bool IsEditing => Entry != null && State != HotkeyEditState.Idle;
        /// <summary>
        /// 指示状态是否既非 <see cref="HotkeyEditState.Idle"/> 也非 <see cref="HotkeyEditState.Expanded"/>；预期对应等待或处理设备输入阶段。
        /// </summary>
        public bool IsRecording => State != HotkeyEditState.Idle && State != HotkeyEditState.Expanded;

        /// <summary>
        /// 推进一次录制状态机。空闲和仅展开状态不读取输入。
        /// </summary>
        public void Update()
        {
            switch (State)
            {
                case HotkeyEditState.Idle:
                case HotkeyEditState.Expanded:
                    return;
                case HotkeyEditState.WaitingPress:
                    DealWaitingPress();
                    break;
                case HotkeyEditState.Recording:
                    DealRecording();
                    break;
                case HotkeyEditState.WaitingConfirm:
                    DealWaitingConfirm();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 结束当前会话，并清空所有编辑副本和输入预览。
        /// 若仍处于录制阶段，会先将 <see cref="OriginalValue"/> 引用的热键及全局热键开关恢复为 <see cref="BeginRecord"/> 前的值；
        /// 仅展开但未录制时不会改写这两个外部状态。
        /// </summary>
        public void Clear()
        {
            if (IsRecording)
            {
                OriginalValue.Valid = _originalValidBeforeEdit;
                Hotkey.GlobalValid = _globalValidBeforeEdit;
            }

            Entry = null;
            State = HotkeyEditState.Idle;
            OriginalValue = null;
            WorkingValue = null;
            WorkingChord = null;
            PreviewGamepadChord = null;
            PreviewKeyboardChord = null;
        }

        /// <summary>
        /// 为配置项建立编辑副本并进入展开状态。
        /// null 配置项或已有会话时不做处理；底层热键在实际开始录制前仍保持可用。
        /// </summary>
        /// <param name="entry">要编辑的热键配置项绑定。</param>
        public void BeginEdit(IEntryBinding entry)
        {
            if (entry == null || State != HotkeyEditState.Idle)
                return;

            var current = ValueProvider.GetValidValue<Hotkey>(entry);

            Entry = entry;
            State = HotkeyEditState.Expanded;
            OriginalValue = current;
            WorkingValue = OriginalValue.Clone();
            WorkingValue.Valid = false;
            WorkingChord = null;
            PreviewGamepadChord = null;
            PreviewKeyboardChord = null;
        }

        /// <summary>
        /// 开始录制指定组合键，禁用原热键和全局热键响应，并等待首次设备按下。
        /// 仅在会话已展开且尚未录制时生效；禁用前的有效状态会保留到本轮录制结束并按快照恢复。
        /// </summary>
        /// <param name="chord">要替换或新增的非 null 工作组合键对象；通常应属于 <see cref="WorkingValue"/>。</param>
        public void BeginRecord(HotkeyChord chord)
        {
            if (!IsEditing || State != HotkeyEditState.Expanded)
                return;

            // 必须在禁用前保存现值；结束录制时应恢复调用方状态，而不是无条件启用热键。
            _globalValidBeforeEdit = Hotkey.GlobalValid;
            _originalValidBeforeEdit = OriginalValue.Valid;

            Hotkey.GlobalValid = false;
            OriginalValue.Valid = false;
            WorkingChord = chord;
            State = HotkeyEditState.WaitingPress;
        }

        /// <summary>
        /// 放弃当前组合键录制，恢复录制前的原热键与全局热键有效状态，
        /// 然后从已提交值重建工作副本并返回展开状态。
        /// </summary>
        public void CancelRecord()
        {
            if (!IsEditing || State == HotkeyEditState.Expanded)
                return;

            Hotkey.GlobalValid = _globalValidBeforeEdit;
            OriginalValue.Valid = _originalValidBeforeEdit;
            WorkingValue = OriginalValue.Clone();
            WorkingValue.Valid = false;
            WorkingChord = null;
            State = HotkeyEditState.Expanded;
        }

        /// <summary>
        /// 放弃整个编辑会话；若录制尚未结束，会恢复录制前的热键有效状态。
        /// </summary>
        public void CancelEdit()
        {
            Clear();
        }

        /// <summary>
        /// 在组合键已释放并等待确认时提交本次录制，然后关闭整个编辑会话。
        /// 若调用时状态尚不可确认，则直接清理会话而不写回。
        /// </summary>
        /// <param name="changeSink">接收确认后热键值的配置变更提交器。</param>
        public void ConfirmEdit(EntryChangeSink changeSink)
        {
            if (!IsEditing || State != HotkeyEditState.WaitingConfirm)
            {
                Clear();
                return;
            }

            ConfirmRecord(changeSink);
            Clear();
        }

        /// <summary>
        /// 确认当前组合键，清理无效组合，并仅在结果非空且相对原值发生变化时写回配置。
        /// 写回前会将新的工作副本标记为有效；随后只恢复录制前的全局开关及旧 <see cref="OriginalValue"/> 对象，
        /// 再以写回后的当前值重建工作副本并保留展开编辑会话，以便继续增删组合。
        /// 状态不允许确认时会退回取消录制流程。
        /// </summary>
        /// <param name="changeSink">接收确认后热键值的配置变更提交器。</param>
        public void ConfirmRecord(EntryChangeSink changeSink)
        {
            if (!IsEditing || State != HotkeyEditState.WaitingConfirm)
            {
                CancelRecord();
                return;
            }

            if (PreviewGamepadChord != null && PreviewKeyboardChord != null)
            {
                // 同一轮同时出现两类有效设备输入时无法仅凭快照消歧，保留状态机此前选定的 WorkingChord。
                if (PreviewGamepadChord.IsValid && PreviewKeyboardChord.IsValid)
                {
                    BLog.Warn($"Both gamepad and keyboard inputs are detected, which is ambiguous");
                }
                else if (PreviewGamepadChord.IsValid)
                {
                    WorkingChord.Chord = PreviewGamepadChord;
                }
                else if (PreviewKeyboardChord.IsValid)
                {
                    WorkingChord.Chord = PreviewKeyboardChord;
                }
            }

            if (WorkingChord != null)
                WorkingValue.Add(WorkingChord);

            WorkingValue.RemoveInvalidHotkey();
            if (!WorkingValue.HasSameHotkey(OriginalValue) && WorkingValue.Count > 0)
            {
                WorkingValue.Valid = true;
                changeSink.SetValue(Entry, WorkingValue);
            }

            Hotkey.GlobalValid = _globalValidBeforeEdit;
            OriginalValue.Valid = _originalValidBeforeEdit;

            WorkingChord = null;
            PreviewGamepadChord = null;
            PreviewKeyboardChord = null;
            OriginalValue = ValueProvider.GetValidValue<Hotkey>(Entry);
            WorkingValue = OriginalValue.Clone();
            WorkingValue.Valid = false;
            State = HotkeyEditState.Expanded;
        }

        /// <summary>
        /// 确认此前待处理的组合键后，开始重新录制指定组合键。
        /// </summary>
        /// <param name="chord">要重新录制的工作组合键。</param>
        /// <param name="changeSink">接收此前可确认变化的配置变更提交器。</param>
        public void SetWorkingChord(HotkeyChord chord, EntryChangeSink changeSink)
        {
            if (!IsEditing)
                return;

            ConfirmRecord(changeSink);
            BeginRecord(chord);
        }

        /// <summary>
        /// 确认此前待处理的组合键，将新组合加入工作副本并立即进入其录制流程。
        /// </summary>
        /// <param name="chord">要新增并录制的组合键。</param>
        /// <param name="changeSink">接收此前可确认变化的配置变更提交器。</param>
        public void AddChord(HotkeyChord chord, EntryChangeSink changeSink)
        {
            if (!IsEditing)
                return;

            ConfirmRecord(changeSink);
            WorkingValue.Add(chord);
            BeginRecord(chord);
        }

        /// <summary>
        /// 从工作副本移除组合键并立即尝试提交剩余结果。
        /// 删除当前录制项时会先取消录制；删除最后一个有效组合不会把空热键写回配置。
        /// </summary>
        /// <param name="chord">要从工作副本移除的组合键。</param>
        /// <param name="changeSink">接收剩余热键值的配置变更提交器。</param>
        public void RemoveChord(HotkeyChord chord, EntryChangeSink changeSink)
        {
            if (!IsEditing)
                return;

            var chordIndex = WorkingValue.Hotkeys.IndexOf(chord);
            if (WorkingChord == chord)
            {
                CancelRecord();

                if (chordIndex >= 0 && chordIndex < WorkingValue.Count)
                    chord = WorkingValue.Hotkeys[chordIndex];
            }

            WorkingValue.Remove(chord);
            State = HotkeyEditState.WaitingConfirm;
            ConfirmRecord(changeSink);
        }

        /// <summary>
        /// 在等待首次按键阶段捕获输入；检测到任一按下状态后保存双设备预览并进入录制阶段。
        /// </summary>
        public void DealWaitingPress()
        {
            if (!IsEditing || State != HotkeyEditState.WaitingPress)
                return;

            var snapshot = CaptureHotkeyInput();
            if (snapshot == null || !snapshot.HasAnyPressed)
                return;

            PreviewGamepadChord = snapshot.GamepadChord;
            PreviewKeyboardChord = snapshot.KeyboardChord;
            State = HotkeyEditState.Recording;
        }

        /// <summary>
        /// 在录制阶段持续更新组合键，全部按键释放后进入等待确认。
        /// 手柄优先保留按键数不减少的快照；有效键盘快照会清除手柄预览，以保持单设备组合。
        /// </summary>
        public void DealRecording()
        {
            if (!IsEditing || State != HotkeyEditState.Recording)
                return;

            var snapshot = CaptureHotkeyInput();
            if (snapshot == null)
                return;

            if (!snapshot.HasAnyPressed)
            {
                State = HotkeyEditState.WaitingConfirm;
                return;
            }

            if (snapshot.GamepadChord.IsValid && snapshot.GamepadChord.Count >= PreviewGamepadChord.Count)
            {
                PreviewGamepadChord = snapshot.GamepadChord;
                PreviewKeyboardChord.Clear();
                WorkingChord.Chord = PreviewGamepadChord;
                return;
            }

            if (!PreviewKeyboardChord.IsValid || snapshot.KeyboardChord.IsValid)
            {
                PreviewKeyboardChord = snapshot.KeyboardChord;
                PreviewGamepadChord.Clear();
                WorkingChord.Chord = PreviewKeyboardChord;
                return;
            }
        }

        /// <summary>
        /// 在等待确认阶段处理再次按下或无效组合。
        /// 再次按下会恢复录制；组合仍无效时也回到录制阶段等待补全。
        /// </summary>
        public void DealWaitingConfirm()
        {
            if (!IsEditing || State != HotkeyEditState.WaitingConfirm)
                return;

            var snapshot = CaptureHotkeyInput();
            if (snapshot?.HasAnyPressed == true)
            {
                PreviewGamepadChord = snapshot.GamepadChord;
                PreviewKeyboardChord = snapshot.KeyboardChord;
                if (PreviewGamepadChord.IsValid)
                    WorkingChord.Chord = PreviewGamepadChord;
                else
                    WorkingChord.Chord = PreviewKeyboardChord;

                State = HotkeyEditState.Recording;
                return;
            }

            if (!WorkingChord.IsValid)
            {
                PreviewGamepadChord.Clear();
                PreviewKeyboardChord.Clear();
                State = HotkeyEditState.Recording;
                return;
            }
        }

        /// <summary>
        /// 从当前 Input System 键盘和手柄读取一份组合键快照。
        /// 手柄记录当前按下或本帧按下的按钮；键盘只将本帧按下的非修饰键设为主键，同时保留持续按下的修饰键。
        /// </summary>
        /// <returns>编辑中的设备快照；没有活动会话时返回 null。</returns>
        public HotkeyInputSnapshot CaptureHotkeyInput()
        {
            if (!IsEditing)
                return null;

            var snapshot = new HotkeyInputSnapshot
            {
                HasAnyPressed = false,
                GamepadChord = new GamepadChord(WorkingValue.UnityService),
                KeyboardChord = new KeyboardChord(WorkingValue.UnityService)
            };

            var gamepad = WorkingValue.UnityService.GamepadCurrent;
            if (gamepad != null)
            {
                foreach (GamepadButton button in Enum.GetValues(typeof(GamepadButton)))
                {
                    var controller = gamepad[button];
                    if (controller == null)
                        continue;

                    if (controller.wasPressedThisFrame || controller.isPressed)
                    {
                        snapshot.GamepadChord.AddButton(button);
                        snapshot.HasAnyPressed = true;
                    }
                }
            }

            var keyboard = WorkingValue.UnityService.KeyboardCurrent;
            if (keyboard == null)
                return snapshot;

            foreach (var key in keyboard.allKeys)
            {
                if (key == null)
                    continue;

                if (key.isPressed)
                {
                    snapshot.HasAnyPressed = true;
                    if (KeyboardModifierTrigger.IsModifierKey(key.keyCode))
                    {
                        snapshot.KeyboardChord.AddModifier(key.keyCode);
                        continue;
                    }
                }

                if (key.wasPressedThisFrame)
                {
                    snapshot.KeyboardChord.SetMainKey(key.keyCode);
                    snapshot.HasAnyPressed = true;
                }
            }

            return snapshot;
        }

        /// <summary>
        /// 清理会话；若录制尚未结束，会恢复录制前的热键有效状态。
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
    }
}
