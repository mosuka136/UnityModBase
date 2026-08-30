using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 单个键盘修饰条件的叶级触发器，支持左/右侧精确匹配和不区分左右的 Ctrl、Shift、Alt 语义。
    /// 本类保存侧别约定并读取键盘状态，不负责多个修饰键之间的组合判定。
    /// </summary>
    /// <remarks>
    /// 左右键属性会拒绝未定义枚举值，但允许 <see cref="Key.None"/> 和非标准修饰键；侧别属性也可独立修改。
    /// 输入查询会跳过 <see cref="Key.None"/>，因此任意侧模式下只配置一侧仍可读取；规范化输出要求任意侧模式具备完整键对，
    /// 同一不完整状态可能可查询但无法序列化。所有别名列表都是无并发保护的进程级可变对象，列表项不得为 <c>null</c>，
    /// 首项用于规范化输出。
    /// </remarks>
    public sealed class KeyboardModifierTrigger : IHotkeyTrigger
    {
        private Key _leftKey = Key.None;
        private Key _rightKey = Key.None;

        /// <inheritdoc />
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 修饰键左侧对应键；未配置时为 <see cref="Key.None"/>。赋入未定义枚举值时保留原值。
        /// </summary>
        public Key LeftKey
        {
            get => _leftKey;
            set
            {
                if (_leftKey != value && Enum.IsDefined(typeof(Key), value))
                    _leftKey = value;
            }
        }

        /// <summary>
        /// 修饰键右侧对应键；未配置时为 <see cref="Key.None"/>。赋入未定义枚举值时保留原值。
        /// </summary>
        public Key RightKey
        {
            get => _rightKey;
            set
            {
                if (_rightKey != value && Enum.IsDefined(typeof(Key), value))
                    _rightKey = value;
            }
        }
        /// <summary>
        /// 为 <c>true</c> 时左右任意一侧按下都满足条件。
        /// </summary>
        public bool IsAnySide { get; set; } = true;
        /// <summary>
        /// 当 <see cref="IsAnySide"/> 为 <c>false</c> 时决定查询哪一侧；自定义键对输出文本也会使用该侧。
        /// </summary>
        public bool IsLeftSide { get; set; } = true;

        /// <summary>
        /// 不区分左右的 Ctrl 解析别名。
        /// </summary>
        public static readonly List<string> CtrlStr = new List<string>() { "Ctrl", "Control" };

        /// <summary>
        /// 不区分左右的 Shift 解析别名。
        /// </summary>
        public static readonly List<string> ShiftStr = new List<string>() { "Shift" };

        /// <summary>
        /// 不区分左右的 Alt 解析别名。
        /// </summary>
        public static readonly List<string> AltStr = new List<string>() { "Alt" };

        /// <summary>
        /// 左 Ctrl 解析别名。
        /// </summary>
        public static readonly List<string> LCtrlStr = new List<string>() { "LeftCtrl", "LCtrl" };

        /// <summary>
        /// 右 Ctrl 解析别名。
        /// </summary>
        public static readonly List<string> RCtrlStr = new List<string>() { "RightCtrl", "RCtrl" };

        /// <summary>
        /// 左 Shift 解析别名。
        /// </summary>
        public static readonly List<string> LShiftStr = new List<string>() { "LeftShift", "LShift" };

        /// <summary>
        /// 右 Shift 解析别名。
        /// </summary>
        public static readonly List<string> RShiftStr = new List<string>() { "RightShift", "RShift" };

        /// <summary>
        /// 左 Alt 解析别名。
        /// </summary>
        public static readonly List<string> LAltStr = new List<string>() { "LeftAlt", "LAlt" };

        /// <summary>
        /// 右 Alt 解析别名。
        /// </summary>
        public static readonly List<string> RAltStr = new List<string>() { "RightAlt", "RAlt" };

        // 标准修饰键模板不持有 Unity 服务，只向运行时实例复制键对和默认侧别状态。
        private static readonly KeyboardModifierTrigger Ctrl = new KeyboardModifierTrigger(Key.LeftCtrl, Key.RightCtrl);
        private static readonly KeyboardModifierTrigger Shift = new KeyboardModifierTrigger(Key.LeftShift, Key.RightShift);
        private static readonly KeyboardModifierTrigger Alt = new KeyboardModifierTrigger(Key.LeftAlt, Key.RightAlt);

        private KeyboardModifierTrigger(Key leftKey, Key rightKey)
        {
            LeftKey = leftKey;
            RightKey = rightKey;
            IsAnySide = true;
            IsLeftSide = true;
            UnityService = null;
        }

        /// <summary>
        /// 创建尚未指定左右键的修饰键触发器。
        /// </summary>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardModifierTrigger(UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
        }

        /// <summary>
        /// 从单个键创建精确侧别触发器。已知 Ctrl、Shift、Alt 会补齐另一侧配对键；其他已定义键只写入 <see cref="LeftKey"/>。
        /// </summary>
        /// <param name="key">要匹配的具体键；未定义枚举值会被忽略，使左右键保持 <see cref="Key.None"/>。</param>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardModifierTrigger(Key key, UnityProvider unityService)
        {
            if (key == Key.LeftCtrl)
            {
                Ctrl.CopyModifiersTo(this);
                IsLeftSide = true;
            }
            else if (key == Key.RightCtrl)
            {
                Ctrl.CopyModifiersTo(this);
                IsLeftSide = false;
            }
            else if (key == Key.LeftShift)
            {
                Shift.CopyModifiersTo(this);
                IsLeftSide = true;
            }
            else if (key == Key.RightShift)
            {
                Shift.CopyModifiersTo(this);
                IsLeftSide = false;
            }
            else if (key == Key.LeftAlt)
            {
                Alt.CopyModifiersTo(this);
                IsLeftSide = true;
            }
            else if (key == Key.RightAlt)
            {
                Alt.CopyModifiersTo(this);
                IsLeftSide = false;
            }
            else
            {
                LeftKey = key;
                IsLeftSide = true;
            }

            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
            IsAnySide = false;
        }

        /// <summary>
        /// 使用完整左右键和侧别状态创建触发器，不校验键是否属于标准修饰键；未定义枚举值会被对应属性忽略。
        /// </summary>
        /// <param name="leftKey">左侧键。</param>
        /// <param name="rightKey">右侧键。</param>
        /// <param name="isAnySide">是否允许任意一侧。</param>
        /// <param name="isLeftSide">精确匹配时是否选择左侧。</param>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardModifierTrigger(Key leftKey, Key rightKey, bool isAnySide, bool isLeftSide, UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));

            LeftKey = leftKey;
            RightKey = rightKey;
            IsAnySide = isAnySide;
            IsLeftSide = isLeftSide;
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;

            // 状态可由编辑流程逐项构造；先排除 None，避免把尚未完成的键对交给 Input System 索引器。
            if (IsAnySide)
            {
                if (LeftKey != Key.None && RightKey != Key.None)
                    return kb[LeftKey].isPressed || kb[RightKey].isPressed;
                else if (LeftKey != Key.None)
                    return kb[LeftKey].isPressed;
                else if (RightKey != Key.None)
                    return kb[RightKey].isPressed;
                else
                    return false;
            }

            if (IsLeftSide)
            {
                if (LeftKey != Key.None)
                    return kb[LeftKey].isPressed;
                else
                    return false;
            }
            else
            {
                if (RightKey != Key.None)
                    return kb[RightKey].isPressed;
                else
                    return false;
            }
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;

            // 与保持状态查询使用相同的未完成状态容错，确保两种查询不会因 None 产生不同失败方式。
            if (IsAnySide)
            {
                if (LeftKey != Key.None && RightKey != Key.None)
                    return kb[LeftKey].wasPressedThisFrame || kb[RightKey].wasPressedThisFrame;
                else if (LeftKey != Key.None)
                    return kb[LeftKey].wasPressedThisFrame;
                else if (RightKey != Key.None)
                    return kb[RightKey].wasPressedThisFrame;
                else
                    return false;
            }

            if (IsLeftSide)
            {
                if (LeftKey != Key.None)
                    return kb[LeftKey].wasPressedThisFrame;
                else
                    return false;
            }
            else
            {
                if (RightKey != Key.None)
                    return kb[RightKey].wasPressedThisFrame;
                else
                    return false;
            }
        }

        /// <summary>
        /// 不区分大小写解析 Ctrl、Shift、Alt 及其左右侧别名。
        /// </summary>
        /// <param name="token">单个修饰键别名，前后空白会被移除。</param>
        /// <param name="unityService">输入状态提供器；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>成功时包含修饰键触发器，否则包含解析错误。</returns>
        /// <exception cref="NullReferenceException">参与匹配的可变别名列表包含 <c>null</c> 项。</exception>
        public static HotkeyResult<KeyboardModifierTrigger> TryParse(string token, UnityProvider unityService)
        {
            if (unityService == null)
                return HotkeyResult<KeyboardModifierTrigger>.Fail("UnityService is null.");

            if (string.IsNullOrWhiteSpace(token))
                return HotkeyResult<KeyboardModifierTrigger>.Fail("Token is null or whitespace.");

            token = token.Trim();

            var result = new KeyboardModifierTrigger(unityService);

            if (EqualsL(token, CtrlStr))
            {
                Ctrl.CopyModifiersTo(result);
                return result;
            }

            if (EqualsL(token, ShiftStr))
            {
                Shift.CopyModifiersTo(result);
                return result;
            }

            if (EqualsL(token, AltStr))
            {
                Alt.CopyModifiersTo(result);
                return result;
            }

            if (EqualsL(token, LCtrlStr))
            {
                Ctrl.CopyModifiersTo(result);
                result.IsAnySide = false;
                result.IsLeftSide = true;
                return result;
            }

            if (EqualsL(token, RCtrlStr))
            {
                Ctrl.CopyModifiersTo(result);
                result.IsAnySide = false;
                result.IsLeftSide = false;
                return result;
            }

            if (EqualsL(token, LShiftStr))
            {
                Shift.CopyModifiersTo(result);
                result.IsAnySide = false;
                result.IsLeftSide = true;
                return result;
            }

            if (EqualsL(token, RShiftStr))
            {
                Shift.CopyModifiersTo(result);
                result.IsAnySide = false;
                result.IsLeftSide = false;
                return result;
            }

            if (EqualsL(token, LAltStr))
            {
                Alt.CopyModifiersTo(result);
                result.IsAnySide = false;
                result.IsLeftSide = true;
                return result;
            }

            if (EqualsL(token, RAltStr))
            {
                Alt.CopyModifiersTo(result);
                result.IsAnySide = false;
                result.IsLeftSide = false;
                return result;
            }

            return HotkeyResult<KeyboardModifierTrigger>.Fail("Unrecognized token.");
        }

        /// <summary>
        /// 复制修饰键定义，不复制 Unity 输入服务引用。
        /// 该方法用于静态模板向运行时实例传递左右键约定。
        /// </summary>
        /// <param name="other">接收定义的触发器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> 为 <c>null</c> 时抛出。</exception>
        public void CopyModifiersTo(KeyboardModifierTrigger other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            other.LeftKey = LeftKey;
            other.RightKey = RightKey;
            other.IsAnySide = IsAnySide;
            other.IsLeftSide = IsLeftSide;
        }

        /// <summary>
        /// 返回当前侧别对应的首个规范化别名；精确侧模式缺少所选键，或任意侧模式缺少键对任一侧时返回空字符串。
        /// 对于非标准键对，使用 <see cref="IsLeftSide"/> 指定键的枚举名称。
        /// </summary>
        /// <returns>修饰键配置文本或空字符串；对应别名列表为空时返回 <c>null</c>。</returns>
        public override string ToString()
        {
            if (IsAnySide)
            {
                if (LeftKey == Key.None || RightKey == Key.None)
                    return string.Empty;
            }
            else
            {
                if (IsLeftSide)
                {
                    if (LeftKey == Key.None)
                        return string.Empty;
                }
                else
                {
                    if (RightKey == Key.None)
                        return string.Empty;
                }
            }

            if (LeftKey == Key.LeftCtrl && RightKey == Key.RightCtrl)
            {
                if (IsAnySide)
                    return CtrlStr.FirstOrDefault();
                else
                    return IsLeftSide ? LCtrlStr.FirstOrDefault() : RCtrlStr.FirstOrDefault();
            }

            if (LeftKey == Key.LeftShift && RightKey == Key.RightShift)
            {
                if (IsAnySide)
                    return ShiftStr.FirstOrDefault();
                else
                    return IsLeftSide ? LShiftStr.FirstOrDefault() : RShiftStr.FirstOrDefault();
            }

            if (LeftKey == Key.LeftAlt && RightKey == Key.RightAlt)
            {
                if (IsAnySide)
                    return AltStr.FirstOrDefault();
                else
                    return IsLeftSide ? LAltStr.FirstOrDefault() : RAltStr.FirstOrDefault();
            }

            return IsLeftSide ? LeftKey.ToString() : RightKey.ToString();
        }

        /// <summary>
        /// 判断文本是否与别名列表中的任一项不区分大小写地相等。
        /// </summary>
        /// <param name="entry">候选文本；为空白时返回 <c>false</c>。</param>
        /// <param name="list">别名列表；为 <c>null</c> 或空列表时返回 <c>false</c>。</param>
        /// <returns>存在匹配别名时为 <c>true</c>。</returns>
        /// <exception cref="NullReferenceException"><paramref name="list"/> 包含 <c>null</c> 项。</exception>
        public static bool EqualsL(string entry, List<string> list)
        {
            if (string.IsNullOrWhiteSpace(entry) || list == null || list.Count == 0)
                return false;

            foreach (var item in list)
            {
                if (item.Equals(entry, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断键是否为左右 Ctrl、Shift 或 Alt 之一。
        /// </summary>
        /// <param name="key">候选键。</param>
        /// <returns>属于受支持修饰键时为 <c>true</c>。</returns>
        public static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftShift || key == Key.RightShift || key == Key.LeftAlt || key == Key.RightAlt;
        }

        /// <inheritdoc />
        public IHotkeyTrigger Clone()
        {
            return new KeyboardModifierTrigger(LeftKey, RightKey, IsAnySide, IsLeftSide, UnityService);
        }
    }
}
