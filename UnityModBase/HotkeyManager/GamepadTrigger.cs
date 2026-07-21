using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem.LowLevel;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 单个手柄按钮的叶级触发器，供 <see cref="GamepadChord"/> 组合多个同时按下的按钮。
    /// 配置文本允许省略 <c>Gamepad</c> 前缀解析，但写出时始终带前缀，以避免和键盘键名混淆。
    /// </summary>
    /// <remarks>
    /// 本类不选择或锁定具体手柄，只读取 <see cref="UnityProvider.GamepadCurrent"/>。设备不存在时输入查询返回 <c>false</c>。
    /// 别名列表是无并发保护的进程级可变对象，列表项不得为 <c>null</c>，首项决定规范化输出。
    /// 可写属性只接受 <see cref="GamepadButton"/> 中已定义的值；未定义值会被静默忽略，配置解析器则返回明确的失败结果。
    /// </remarks>
    public class GamepadTrigger : IHotkeyTrigger
    {
        private GamepadButton _button = GamepadButton.South;

        /// <inheritdoc />
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 要查询的 Input System 手柄按钮；默认使用 A/South 按钮。赋入未定义枚举值时保留原值。
        /// </summary>
        public GamepadButton Button
        {
            get => _button;
            set
            {
                if (_button != value && Enum.IsDefined(typeof(GamepadButton), value))
                    _button = value;
            }
        }

        /// <summary>
        /// 规范化手柄按钮文本使用的前缀。解析时该前缀可省略，输出时始终添加。
        /// </summary>
        public const string Prefix = "Gamepad";

        /// <summary>
        /// South/A 按钮别名。
        /// </summary>
        public static readonly List<string> SouthStr = new List<string>() { "A", "South", "Cross" };

        /// <summary>
        /// East/B 按钮别名。
        /// </summary>
        public static readonly List<string> EastStr = new List<string>() { "B", "East", "Circle" };

        /// <summary>
        /// West/X 按钮别名。
        /// </summary>
        public static readonly List<string> WestStr = new List<string>() { "X", "West", "Square" };

        /// <summary>
        /// North/Y 按钮别名。
        /// </summary>
        public static readonly List<string> NorthStr = new List<string>() { "Y", "North", "Triangle" };

        /// <summary>
        /// 左肩键别名。
        /// </summary>
        public static readonly List<string> LeftShoulderStr = new List<string>() { "LB", "LeftShoulder" };

        /// <summary>
        /// 右肩键别名。
        /// </summary>
        public static readonly List<string> RightShoulderStr = new List<string>() { "RB", "RightShoulder" };

        /// <summary>
        /// 选择/返回按钮别名。
        /// </summary>
        public static readonly List<string> SelectStr = new List<string>() { "Back", "Select" };

        /// <summary>
        /// 开始按钮别名。
        /// </summary>
        public static readonly List<string> StartStr = new List<string>() { "Start" };

        /// <summary>
        /// 左摇杆按压按钮别名。
        /// </summary>
        public static readonly List<string> LeftStickStr = new List<string>() { "LS", "LeftStick" };

        /// <summary>
        /// 右摇杆按压按钮别名。
        /// </summary>
        public static readonly List<string> RightStickStr = new List<string>() { "RS", "RightStick" };

        /// <summary>
        /// 十字键上方向别名。
        /// </summary>
        public static readonly List<string> DpadUpStr = new List<string>() { "DpadUp" };

        /// <summary>
        /// 十字键下方向别名。
        /// </summary>
        public static readonly List<string> DpadDownStr = new List<string>() { "DpadDown" };

        /// <summary>
        /// 十字键左方向别名。
        /// </summary>
        public static readonly List<string> DpadLeftStr = new List<string>() { "DpadLeft" };

        /// <summary>
        /// 十字键右方向别名。
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
        /// 创建指定按钮的触发器；未定义枚举值会被属性拒绝，使触发器保留默认的 A/South 按钮。
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
        /// 枚举名称和数字文本都必须对应已定义的枚举值；别名匹配优先于枚举解析。
        /// </summary>
        /// <param name="token">单个按钮文本，前后空白会被移除。</param>
        /// <param name="unityProvider">输入状态提供器；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>成功时包含手柄触发器，否则包含解析错误。</returns>
        /// <exception cref="NullReferenceException">参与匹配的可变别名列表包含 <c>null</c> 项。</exception>
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
                if (!Enum.IsDefined(typeof(GamepadButton), parsed))
                    return HotkeyResult<GamepadTrigger>.Fail($"Parsed button '{parsed}' is not defined in GamepadButton enum.");

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
        /// 返回带 <see cref="Prefix"/> 的规范化按钮文本；有专用别名的按钮使用对应列表首项，
        /// 其他已定义按钮使用枚举名称，因此输出仍可由 <see cref="TryParse(string, UnityProvider)"/> 读回。
        /// </summary>
        /// <returns>手柄按钮配置文本；命中专用别名但对应列表为空时仅返回前缀。</returns>
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
