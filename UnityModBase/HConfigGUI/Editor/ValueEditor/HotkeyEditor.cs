using System;
using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    /// <summary>
    /// 绘制热键组合列表，并协调 <see cref="HotkeyEditSession"/> 与配置模态窗口完成录制、增删和提交。
    /// 编辑期间显示工作副本，只有状态机确认后的非空变化才通过上下文提交器写回配置。
    /// </summary>
    /// <remarks>
    /// 会话和弹窗状态预期仅由 Unity 主线程推进。录制或布局内部绘制失败时，已开启的 IMGUI 布局会闭合，
    /// 但会话状态不会在此处自动回滚，调用方可通过 <see cref="HotkeyEditSession.CancelEdit"/> 结束编辑。
    /// </remarks>
    public class HotkeyEditor : IValueEditor
    {
        /// <summary>
        /// 获取当前编辑器独占的热键录制会话。
        /// </summary>
        public HotkeyEditSession Session { get; }
        /// <summary>
        /// 获取热键列表和录制弹窗使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取热键录制提示样式资源。
        /// </summary>
        public StyleResource StyleProvider { get; }

        /// <summary>
        /// 创建热键编辑器和独立录制会话。
        /// </summary>
        /// <param name="unityGui">用于绘制热键控件的 IMGUI 提供器。</param>
        /// <param name="styleProvider">提供录制提示样式的资源。</param>
        public HotkeyEditor(IUnityGuiProvider unityGui, StyleResource styleProvider)
        {
            Session = new HotkeyEditSession();
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
            StyleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider));
        }

        /// <inheritdoc/>
        public bool CanEdit(IEntryBinding entry)
        {
            if (entry != null && typeof(Hotkey).IsAssignableFrom(entry.ValueType) && entry.Metadata == null)
                return true;
            else
                return false;
        }

        /// <inheritdoc/>
        public void DrawValue(IEntryBinding entry, GuiContext context)
        {
            if (!typeof(Hotkey).IsAssignableFrom(entry.ValueType))
                return;

            var valueString = GetHotkeyDisplayString(entry);
            if (valueString == null)
                return;

            if (UnityGui.Button(valueString, UnityGui.ExpandWidth(true)))
            {
                if (Session.Entry == entry)
                    Session.ConfirmEdit(context.ChangeSink);
                else
                {
                    Session.ConfirmEdit(context.ChangeSink);
                    Session.BeginEdit(entry);
                }
            }
        }

        /// <inheritdoc/>
        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
            if (Session.Entry != entry)
                return;

            Session.Update();

            var value = Session.WorkingValue;
            if (value == null)
                return;

            UnityGui.BeginHorizontal();
            try
            {
                UnityGui.Space(context.GetEntryLabelWidth(context.SelectedGroupKey));
                UnityGui.BeginVertical(UnityGui.BoxStyle);
                try
                {
                    // 删除操作会直接修改工作副本；使用快照可避免在枚举期间使热键集合失效。
                    var chords = new List<HotkeyChord>(value.Hotkeys);
                    foreach (var chord in chords)
                    {
                        UnityGui.BeginHorizontal();
                        try
                        {
                            UnityGui.Button(chord.ToString(), UnityGui.ExpandWidth(true));

                            if (UnityGui.Button(TranslatorResource.Record, UnityGui.ExpandWidth(false)))
                            {
                                chord.Clear();
                                Session.SetWorkingChord(chord, context.ChangeSink);
                                SetPopupWindow(context);
                            }

                            if (value.Count > 1 && UnityGui.Button(TranslatorResource.Remove, UnityGui.ExpandWidth(false)))
                                Session.RemoveChord(chord, context.ChangeSink);
                        }
                        finally
                        {
                            UnityGui.EndHorizontal();
                        }
                    }

                    UnityGui.BeginHorizontal();
                    try
                    {
                        if (UnityGui.Button(TranslatorResource.Add, UnityGui.ExpandWidth(true)))
                        {
                            Session.AddChord(new HotkeyChord(value.UnityService), context.ChangeSink);
                            SetPopupWindow(context);
                        }
                    }
                    finally
                    {
                        UnityGui.EndHorizontal();
                    }
                }
                finally
                {
                    UnityGui.EndVertical();
                }
            }
            finally
            {
                UnityGui.EndHorizontal();
            }
        }

        /// <summary>
        /// 将上下文弹窗配置为当前热键录制界面；关闭弹窗会取消本轮录制但保留展开编辑会话。
        /// </summary>
        /// <param name="context">接收弹窗状态和回调的配置 GUI 上下文。</param>
        public void SetPopupWindow(GuiContext context)
        {
            context.Popup.IsOpen = true;
            context.Popup.Title = TranslatorResource.RecordHotkeyPopupTitle;
            context.Popup.DrawAction = () => RecordHotkey(context);
            context.Popup.CloseAction = Session.CancelRecord;
        }

        /// <summary>
        /// 推进录制状态并绘制当前组合预览。点击应用后会关闭弹窗；只有等待确认状态会提交组合，其他录制阶段按会话规则取消本轮录制。
        /// </summary>
        /// <param name="context">提供变更提交器和弹窗状态的配置 GUI 上下文。</param>
        public void RecordHotkey(GuiContext context)
        {
            if (!Session.IsRecording)
                return;

            Session.Update();

            UnityGui.FlexibleSpace();
            UnityGui.Label(Session.WorkingChord.ToString(), StyleProvider.RecordingHotkeyLabelStyle, UnityGui.ExpandWidth(true));

            UnityGui.FlexibleSpace();
            if (UnityGui.Button(TranslatorResource.Apply, UnityGui.ExpandWidth(true)))
            {
                Session.ConfirmRecord(context.ChangeSink);
                context.Popup.IsOpen = false;
            }
        }

        /// <summary>
        /// 获取按钮应显示的热键文本；当前编辑项优先使用工作副本，其他项使用最后一个有效值。
        /// </summary>
        /// <param name="entry">要生成显示文本的配置项绑定。</param>
        /// <returns>工作副本或有效配置值的文本；工作副本为 null 时返回空字符串。</returns>
        public string GetHotkeyDisplayString(IEntryBinding entry)
        {
            if (Session.Entry == entry)
                return Session.WorkingValue?.ToString() ?? string.Empty;

            return ValueProvider.GetValidValue(entry)?.ToString();
        }

        /// <summary>
        /// 释放录制会话；若存在未完成的录制，会按该轮开始时的快照恢复原热键对象和全局开关。
        /// </summary>
        public void Dispose()
        {
            Session?.Dispose();
        }
    }
}
