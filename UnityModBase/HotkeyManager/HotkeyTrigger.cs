using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 热键触发条件的统一接口。
    /// 实现类封装键盘键、键盘修饰键或手柄按钮，使 <see cref="HotkeyChord"/> 可以用同一流程判断输入状态。
    /// </summary>
    public interface IHotkeyTrigger
    {
        /// <summary>
        /// 读取输入设备状态时使用的 Unity 服务。
        /// </summary>
        UnityProvider UnityService { get; }

        /// <summary>
        /// 判断输入当前是否保持按下。
        /// </summary>
        /// <returns>输入处于按下状态时为 <c>true</c>；对应设备不存在时为 <c>false</c>。</returns>
        bool IsPressed();

        /// <summary>
        /// 判断输入是否在当前帧刚按下。
        /// </summary>
        /// <returns>输入在当前帧发生按下转换时为 <c>true</c>；对应设备不存在时为 <c>false</c>。</returns>
        bool WasPressedThisFrame();

        /// <summary>
        /// 返回配置文件使用的规范化输入名称。
        /// </summary>
        /// <returns>输入名称；未配置时为空字符串。</returns>
        string ToString();

        /// <summary>
        /// 复制触发条件并共享当前 Unity 服务引用。
        /// </summary>
        /// <returns>新的触发器实例。</returns>
        IHotkeyTrigger Clone();
    }

    /// <summary>
    /// 单个键盘按键触发器。
    /// </summary>
    public class KeyboardTrigger : IHotkeyTrigger
    {
        /// <inheritdoc />
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 要查询的 Input System 键；默认 <see cref="Key.None"/> 表示未配置。
        /// </summary>
        public Key Key { get; set; } = Key.None;

        /// <summary>
        /// 创建未指定键的触发器。
        /// </summary>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardTrigger(UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
        }

        /// <summary>
        /// 创建指定键的触发器；构造函数不拒绝 <see cref="Key.None"/>。
        /// </summary>
        /// <param name="key">要查询的键。</param>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardTrigger(Key key, UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
            Key = key;
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;
            return kb[Key].isPressed;
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;
            return kb[Key].wasPressedThisFrame;
        }

        /// <summary>
        /// 不区分大小写解析已定义的 <see cref="Key"/> 名称；<see cref="Key.None"/> 和未定义数值均视为失败。
        /// </summary>
        /// <param name="token">单个键名，前后空白会被移除。</param>
        /// <param name="unityService">输入状态提供器；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>成功时包含键盘触发器，否则包含解析错误。</returns>
        public static HotkeyResult<KeyboardTrigger> TryParse(string token, UnityProvider unityService)
        {
            if (unityService == null)
                return HotkeyResult<KeyboardTrigger>.Fail("UnityService is null.");

            if (string.IsNullOrWhiteSpace(token))
                return HotkeyResult<KeyboardTrigger>.Fail("Token is null or whitespace.");

            token = token.Trim();

            if (Enum.TryParse<Key>(token, true, out var key))
            {
                if (!Enum.IsDefined(typeof(Key), key))
                    return HotkeyResult<KeyboardTrigger>.Fail($"Parsed key '{key}' is not defined in Key enum.");

                if (key == Key.None)
                    return HotkeyResult<KeyboardTrigger>.Fail("Parsed key is None.");

                return new KeyboardTrigger(key, unityService);
            }

            return HotkeyResult<KeyboardTrigger>.Fail($"Failed to parse token '{token}' as a Key.");
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Key == Key.None)
                return string.Empty;
            return Key.ToString();
        }

        /// <inheritdoc />
        public IHotkeyTrigger Clone()
        {
            return new KeyboardTrigger(Key, UnityService);
        }
    }

    /// <summary>
    /// 键盘修饰键触发器。
    /// 支持左/右侧精确匹配，也支持不区分左右的 Ctrl、Shift、Alt 语义。
    /// </summary>
    public class KeyboardModifierTrigger : IHotkeyTrigger
    {
        /// <inheritdoc />
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 修饰键左侧对应键；未配置时为 <see cref="Key.None"/>。
        /// </summary>
        public Key LeftKey { get; set; } = Key.None;

        /// <summary>
        /// 修饰键右侧对应键；未配置时为 <see cref="Key.None"/>。
        /// </summary>
        public Key RightKey { get; set; } = Key.None;
        /// <summary>
        /// 为 <c>true</c> 时左右任意一侧按下都满足条件。
        /// </summary>
        public bool IsAnySide { get; set; } = true;
        /// <summary>
        /// 当 <see cref="IsAnySide"/> 为 <c>false</c> 时，决定匹配左侧还是右侧按键。
        /// </summary>
        public bool IsLeftSide { get; set; } = true;

        /// <summary>
        /// 不区分左右的 Ctrl 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> CtrlStr = new List<string>() { "Ctrl", "Control" };

        /// <summary>
        /// 不区分左右的 Shift 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> ShiftStr = new List<string>() { "Shift" };

        /// <summary>
        /// 不区分左右的 Alt 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> AltStr = new List<string>() { "Alt" };

        /// <summary>
        /// 左 Ctrl 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> LCtrlStr = new List<string>() { "LeftCtrl", "LCtrl" };

        /// <summary>
        /// 右 Ctrl 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> RCtrlStr = new List<string>() { "RightCtrl", "RCtrl" };

        /// <summary>
        /// 左 Shift 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> LShiftStr = new List<string>() { "LeftShift", "LShift" };

        /// <summary>
        /// 右 Shift 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> RShiftStr = new List<string>() { "RightShift", "RShift" };

        /// <summary>
        /// 左 Alt 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> LAltStr = new List<string>() { "LeftAlt", "LAlt" };

        /// <summary>
        /// 右 Alt 解析别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> RAltStr = new List<string>() { "RightAlt", "RAlt" };

        /// <summary>
        /// Ctrl 左右键定义模板，不含 Unity 服务，只用于复制元数据。
        /// </summary>
        public static readonly KeyboardModifierTrigger Ctrl = new KeyboardModifierTrigger(Key.LeftCtrl, Key.RightCtrl);

        /// <summary>
        /// Shift 左右键定义模板，不含 Unity 服务，只用于复制元数据。
        /// </summary>
        public static readonly KeyboardModifierTrigger Shift = new KeyboardModifierTrigger(Key.LeftShift, Key.RightShift);

        /// <summary>
        /// Alt 左右键定义模板，不含 Unity 服务，只用于复制元数据。
        /// </summary>
        public static readonly KeyboardModifierTrigger Alt = new KeyboardModifierTrigger(Key.LeftAlt, Key.RightAlt);

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
        /// 从单个键创建精确侧别触发器。已知 Ctrl、Shift、Alt 会补齐另一侧配对键；其他键只写入 <see cref="LeftKey"/>。
        /// </summary>
        /// <param name="key">要匹配的具体键。</param>
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
        /// 使用完整左右键和侧别状态创建触发器，不额外校验键是否属于标准修饰键。
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

            if (IsAnySide)
                return kb[LeftKey].isPressed || kb[RightKey].isPressed;

            return IsLeftSide ? kb[LeftKey].isPressed : kb[RightKey].isPressed;
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;

            if (IsAnySide)
                return kb[LeftKey].wasPressedThisFrame || kb[RightKey].wasPressedThisFrame;

            return IsLeftSide ? kb[LeftKey].wasPressedThisFrame : kb[RightKey].wasPressedThisFrame;
        }

        /// <summary>
        /// 不区分大小写解析 Ctrl、Shift、Alt 及其左右侧别名。
        /// </summary>
        /// <param name="token">单个修饰键别名，前后空白会被移除。</param>
        /// <param name="unityService">输入状态提供器；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>成功时包含修饰键触发器，否则包含解析错误。</returns>
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
        /// 返回当前侧别对应的首个规范化别名；所需键为 <see cref="Key.None"/> 时返回空字符串。
        /// </summary>
        /// <returns>修饰键配置文本或空字符串。</returns>
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

    /// <summary>
    /// 手柄按钮触发器。
    /// 配置文本允许省略 <c>Gamepad</c> 前缀解析，但写出时始终带前缀，以避免和键盘键名混淆。
    /// </summary>
    public class GamepadTrigger : IHotkeyTrigger
    {
        /// <inheritdoc />
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 要查询的 Input System 手柄按钮；默认使用 A/South 按钮。
        /// </summary>
        public GamepadButton Button { get; set; } = GamepadButton.A;

        /// <summary>
        /// 规范化手柄按钮文本使用的前缀。解析时该前缀可省略，输出时始终添加。
        /// </summary>
        public const string Prefix = "Gamepad";

        /// <summary>
        /// South/A 按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> SouthStr = new List<string>() { "A", "South", "Cross" };

        /// <summary>
        /// East/B 按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> EastStr = new List<string>() { "B", "East", "Circle" };

        /// <summary>
        /// West/X 按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> WestStr = new List<string>() { "X", "West", "Square" };

        /// <summary>
        /// North/Y 按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> NorthStr = new List<string>() { "Y", "North", "Triangle" };

        /// <summary>
        /// 左肩键别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> LeftShoulderStr = new List<string>() { "LB", "LeftShoulder" };

        /// <summary>
        /// 右肩键别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> RightShoulderStr = new List<string>() { "RB", "RightShoulder" };

        /// <summary>
        /// 选择/返回按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> SelectStr = new List<string>() { "Back", "Select" };

        /// <summary>
        /// 开始按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> StartStr = new List<string>() { "Start" };

        /// <summary>
        /// 左摇杆按压按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> LeftStickStr = new List<string>() { "LS", "LeftStick" };

        /// <summary>
        /// 右摇杆按压按钮别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> RightStickStr = new List<string>() { "RS", "RightStick" };

        /// <summary>
        /// 十字键上方向别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> DpadUpStr = new List<string>() { "DpadUp" };

        /// <summary>
        /// 十字键下方向别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> DpadDownStr = new List<string>() { "DpadDown" };

        /// <summary>
        /// 十字键左方向别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> DpadLeftStr = new List<string>() { "DpadLeft" };

        /// <summary>
        /// 十字键右方向别名；首项用于规范化输出。列表为进程级可变状态。
        /// </summary>
        public static readonly List<string> DpadRightStr = new List<string>() { "DpadRight" };

        /// <summary>
        /// 创建默认指向 A/South 按钮的触发器。
        /// </summary>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public GamepadTrigger(UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
        }

        /// <summary>
        /// 创建指定按钮的触发器；构造函数不校验枚举值是否已定义。
        /// </summary>
        /// <param name="button">要查询的手柄按钮。</param>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public GamepadTrigger(GamepadButton button, UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
            Button = button;
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            var gp = UnityService?.GamepadCurrent;
            if (gp == null)
                return false;
            return gp[Button].isPressed;
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            var gp = UnityService?.GamepadCurrent;
            if (gp == null)
                return false;
            return gp[Button].wasPressedThisFrame;
        }

        /// <summary>
        /// 不区分大小写解析可选 <see cref="Prefix"/>、受支持别名或 <see cref="GamepadButton"/> 文本。
        /// 数字文本可解析为未定义的枚举值，当前方法不会额外执行定义检查。
        /// </summary>
        /// <param name="token">单个按钮文本，前后空白会被移除。</param>
        /// <param name="unityProvider">输入状态提供器；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>成功时包含手柄触发器，否则包含解析错误。</returns>
        public static HotkeyResult<GamepadTrigger> TryParse(string token, UnityProvider unityProvider)
        {
            if (unityProvider == null)
                return HotkeyResult<GamepadTrigger>.Fail("UnityService is null.");

            if (string.IsNullOrWhiteSpace(token))
                return HotkeyResult<GamepadTrigger>.Fail("Token is null or whitespace.");

            string s = token.Trim();
            if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                s = s.Substring(Prefix.Length);

            var result = new GamepadTrigger(unityProvider);

            if (EqualsL(s, SouthStr)) { result.Button = GamepadButton.South; return result; }
            if (EqualsL(s, EastStr)) { result.Button = GamepadButton.East; return result; }
            if (EqualsL(s, WestStr)) { result.Button = GamepadButton.West; return result; }
            if (EqualsL(s, NorthStr)) { result.Button = GamepadButton.North; return result; }

            if (EqualsL(s, LeftShoulderStr)) { result.Button = GamepadButton.LeftShoulder; return result; }
            if (EqualsL(s, RightShoulderStr)) { result.Button = GamepadButton.RightShoulder; return result; }

            if (EqualsL(s, SelectStr)) { result.Button = GamepadButton.Select; return result; }
            if (EqualsL(s, StartStr)) { result.Button = GamepadButton.Start; return result; }

            if (EqualsL(s, LeftStickStr)) { result.Button = GamepadButton.LeftStick; return result; }
            if (EqualsL(s, RightStickStr)) { result.Button = GamepadButton.RightStick; return result; }

            if (EqualsL(s, DpadUpStr)) { result.Button = GamepadButton.DpadUp; return result; }
            if (EqualsL(s, DpadDownStr)) { result.Button = GamepadButton.DpadDown; return result; }
            if (EqualsL(s, DpadLeftStr)) { result.Button = GamepadButton.DpadLeft; return result; }
            if (EqualsL(s, DpadRightStr)) { result.Button = GamepadButton.DpadRight; return result; }

            if (Enum.TryParse<GamepadButton>(s, true, out var parsed))
            {
                result.Button = parsed;
                return result;
            }

            return HotkeyResult<GamepadTrigger>.Fail("Failed to parse token.");
        }

        /// <summary>
        /// 判断文本是否与别名列表中的任一项不区分大小写地相等。
        /// </summary>
        /// <param name="entry">候选文本；为空白时返回 <c>false</c>。</param>
        /// <param name="list">别名列表；为 <c>null</c> 或空列表时返回 <c>false</c>。</param>
        /// <returns>存在匹配别名时为 <c>true</c>。</returns>
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
        /// 返回带 <see cref="Prefix"/> 的规范化按钮文本；已知按钮使用对应别名列表首项。
        /// </summary>
        /// <returns>手柄按钮配置文本。</returns>
        public override string ToString()
        {
            switch (Button)
            {
                case GamepadButton.South: return Prefix + SouthStr.FirstOrDefault();
                case GamepadButton.North: return Prefix + NorthStr.FirstOrDefault();
                case GamepadButton.West: return Prefix + WestStr.FirstOrDefault();
                case GamepadButton.East: return Prefix + EastStr.FirstOrDefault();
                case GamepadButton.LeftShoulder: return Prefix + LeftShoulderStr.FirstOrDefault();
                case GamepadButton.RightShoulder: return Prefix + RightShoulderStr.FirstOrDefault();
                case GamepadButton.Select: return Prefix + SelectStr.FirstOrDefault();
                case GamepadButton.Start: return Prefix + StartStr.FirstOrDefault();
                case GamepadButton.LeftStick: return Prefix + LeftStickStr.FirstOrDefault();
                case GamepadButton.RightStick: return Prefix + RightStickStr.FirstOrDefault();
                case GamepadButton.DpadUp: return Prefix + DpadUpStr.FirstOrDefault();
                case GamepadButton.DpadDown: return Prefix + DpadDownStr.FirstOrDefault();
                case GamepadButton.DpadLeft: return Prefix + DpadLeftStr.FirstOrDefault();
                case GamepadButton.DpadRight: return Prefix + DpadRightStr.FirstOrDefault();
                default: return Prefix + Button.ToString();
            }
        }

        /// <inheritdoc />
        public IHotkeyTrigger Clone()
        {
            return new GamepadTrigger(Button, UnityService);
        }
    }
}
