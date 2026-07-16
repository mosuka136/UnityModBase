using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 键盘或手柄组合键的统一契约，区分“当前保持按下”和“当前帧完成触发”两种查询。
    /// </summary>
    public interface IHotkeyChord
    {
        /// <summary>
        /// 当前组合是否有效。无效的组合在任何时候都不会触发。
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 判断组合要求的全部输入当前是否保持按下。
        /// </summary>
        /// <returns>组合有效且全部输入按下时为 <c>true</c>。</returns>
        bool IsPressed();

        /// <summary>
        /// 判断组合是否在当前帧从未完成转为完成。
        /// </summary>
        /// <returns>组合在当前帧触发时为 <c>true</c>。</returns>
        bool WasPressedThisFrame();

        /// <summary>
        /// 移除组合内的全部输入定义，使组合失效。
        /// </summary>
        void Clear();

        /// <summary>
        /// 返回可供配置文件解析的规范化组合文本。
        /// </summary>
        /// <returns>组合文本；无有效输入时为空字符串。</returns>
        string ToString();

        /// <summary>
        /// 深复制组合结构，输入服务引用按实现约定共享。
        /// </summary>
        /// <returns>独立的组合对象。</returns>
        IHotkeyChord Clone();
    }

    /// <summary>
    /// 键盘组合键。由一个主键和零个或多个修饰键组成，触发时要求所有修饰键保持按下状态，并在同一帧按下主键。
    /// 键与键之间的文本分隔符为 <see cref="Separator"/>。
    /// </summary>
    public class KeyboardChord : IHotkeyChord
    {
        /// <summary>
        /// 组合内部修饰键与主键的文本分隔符。
        /// </summary>
        public const char Separator = '+';

        /// <summary>
        /// 组合内触发器读取键盘状态时使用的 Unity 服务。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 触发主键前必须保持按下的修饰键集合。调用方可直接修改列表，并应保持其非 <c>null</c>。
        /// </summary>
        public List<IHotkeyTrigger> Modifiers { get; set; }
        /// <summary>
        /// 当前帧按下时触发该组合的主键。
        /// </summary>
        public IHotkeyTrigger MainKey { get; set; }

        /// <summary>
        /// 是否至少包含主键或一个修饰键；这不等同于可触发性。
        /// </summary>
        public bool HasAnyKey => (MainKey != null) || Modifiers.Count > 0;

        /// <inheritdoc />
        public bool IsValid => MainKey != null;

        /// <summary>
        /// 创建不含按键的键盘组合。
        /// </summary>
        /// <param name="unityService">后续新建触发器使用的 Unity 服务；此构造函数不执行空值校验。</param>
        public KeyboardChord(UnityProvider unityService)
        {
            UnityService = unityService;
            Modifiers = new List<IHotkeyTrigger>();
        }

        /// <summary>
        /// 使用主键和修饰键创建组合；触发器引用浅复制到新列表。
        /// </summary>
        /// <param name="unityService">后续新建触发器使用的 Unity 服务。</param>
        /// <param name="main">主键触发器，可为 <c>null</c>。</param>
        /// <param name="modifiers">修饰键触发器数组，不可为 <c>null</c>。</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="modifiers"/> 为 <c>null</c> 时由集合转换抛出。</exception>
        public KeyboardChord(UnityProvider unityService, IHotkeyTrigger main, params IHotkeyTrigger[] modifiers)
        {
            UnityService = unityService;
            MainKey = main;
            Modifiers = modifiers.ToList();
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            foreach (var modifier in Modifiers)
            {
                if (!modifier.IsPressed())
                    return false;
            }

            return MainKey != null && MainKey.IsPressed();
        }

        /// <summary>
        /// 当全部修饰键当前保持按下，且主键在本帧按下时返回 <c>true</c>。
        /// 修饰键不满足时不会查询主键，避免无意义的输入读取。
        /// </summary>
        /// <returns>键盘组合在本帧触发时为 <c>true</c>。</returns>
        public bool WasPressedThisFrame()
        {
            foreach (var modifier in Modifiers)
            {
                if (!modifier.IsPressed())
                    return false;
            }

            return MainKey != null && MainKey.WasPressedThisFrame();
        }

        /// <summary>
        /// 添加键盘修饰键。
        /// 如果左右两侧同类修饰键都被录入，会合并为不区分左右的修饰键，方便用户输入。
        /// </summary>
        /// <param name="key">候选修饰键；非 Ctrl、Shift 或 Alt 的左右键时按空操作处理。</param>
        public void AddModifier(Key key)
        {
            if (!KeyboardModifierTrigger.IsModifierKey(key))
                return;

            var anotherKey = GetAnotherModifierKey(key);
            var modifiers = Modifiers.OfType<KeyboardModifierTrigger>().ToList();

            var currentModifiers = modifiers.Where(modifier =>
            {
                if (modifier.IsAnySide)
                {
                    if (modifier.LeftKey == key || modifier.RightKey == key)
                        return true;
                }
                else
                {
                    if (modifier.IsLeftSide)
                    {
                        if (modifier.LeftKey == key)
                            return true;
                    }
                    else
                    {
                        if (modifier.RightKey == key)
                            return true;
                    }
                }
                return false;
            });

            if (currentModifiers.Any())
                return;

            var oppositeModifiers = modifiers.Where(modifier =>
                !modifier.IsAnySide &&
                ((modifier.IsLeftSide && modifier.RightKey == key && modifier.LeftKey == anotherKey) ||
                 (!modifier.IsLeftSide && modifier.LeftKey == key && modifier.RightKey == anotherKey)));

            if (oppositeModifiers.Any())
            {
                foreach (var modifier in oppositeModifiers)
                    modifier.IsAnySide = true;

                return;
            }

            Modifiers.Add(new KeyboardModifierTrigger(key, UnityService));
        }

        /// <summary>
        /// 设置主键。主键必须是非修饰键，如果传入的键是修饰键则会被忽略。
        /// </summary>
        /// <param name="key">要设置的键；<see cref="Key.None"/> 会创建一个形式上非空但无法正常触发的键盘触发器。</param>
        public void SetMainKey(Key key)
        {
            if (KeyboardModifierTrigger.IsModifierKey(key))
                return;

            MainKey = new KeyboardTrigger(key, UnityService);
        }

        /// <summary>
        /// 清空修饰键但保留当前主键。
        /// </summary>
        public void ClearModifiers()
        {
            Modifiers.Clear();
        }

        /// <inheritdoc />
        public void Clear()
        {
            Modifiers.Clear();
            MainKey = null;
        }

        /// <summary>
        /// 解析键盘组合文本。最后一个非空分段必须是普通键，之前的分段必须是支持的修饰键别名。
        /// </summary>
        /// <param name="chordStr">以 <see cref="Separator"/> 分隔的组合文本，不可为 <c>null</c>。</param>
        /// <param name="unityService">新触发器使用的 Unity 服务；为空时返回失败结果。</param>
        /// <returns>成功时包含键盘组合；空内容、非法主键或非法修饰键时包含错误。</returns>
        /// <exception cref="System.NullReferenceException"><paramref name="chordStr"/> 为 <c>null</c> 时抛出。</exception>
        public static HotkeyResult<KeyboardChord> TryParse(string chordStr, UnityProvider unityService)
        {
            var raw = chordStr.Split(Separator);
            var parts = new List<string>(raw.Length);
            foreach (var part in raw)
            {
                var str = part.Trim();
                if (string.IsNullOrWhiteSpace(str))
                    continue;
                parts.Add(str);
            }
            if (parts.Count == 0)
                return HotkeyResult<KeyboardChord>.Fail("No valid parts found in chord string.");

            var result = new KeyboardChord(unityService);

            var keyboardTriggerResult = KeyboardTrigger.TryParse(parts.Last(), unityService);
            if (!keyboardTriggerResult.Success)
                return HotkeyResult<KeyboardChord>.Fail($"Failed to parse main key.", keyboardTriggerResult.Errors);
            result.MainKey = keyboardTriggerResult.Value;

            for (int i = 0; i < parts.Count - 1; i++)
            {
                var keyboardModifierTriggerResult = KeyboardModifierTrigger.TryParse(parts[i], unityService);
                if (!keyboardModifierTriggerResult.Success)
                    return HotkeyResult<KeyboardChord>.Fail($"Failed to parse keyboard modifier: {parts[i]}", keyboardModifierTriggerResult.Errors);
                result.Modifiers.Add(keyboardModifierTriggerResult.Value);
            }

            return result;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var modifier in Modifiers)
            {
                sb.Append(modifier.ToString());
                sb.Append(Separator);
            }

            if (MainKey != null)
                sb.Append(MainKey.ToString());
            else
            {
                if (sb.Length > 0)
                    sb.Remove(sb.Length - 1, 1);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 返回 Ctrl、Shift 或 Alt 修饰键的另一侧按键；非受支持修饰键原样返回。
        /// </summary>
        /// <param name="key">候选左右修饰键。</param>
        /// <returns>配对的另一侧按键，或原值。</returns>
        public static Key GetAnotherModifierKey(Key key)
        {
            switch (key)
            {
                case Key.LeftShift:
                    return Key.RightShift;
                case Key.RightShift:
                    return Key.LeftShift;
                case Key.LeftCtrl:
                    return Key.RightCtrl;
                case Key.RightCtrl:
                    return Key.LeftCtrl;
                case Key.LeftAlt:
                    return Key.RightAlt;
                case Key.RightAlt:
                    return Key.LeftAlt;
                default:
                    return key;
            }
        }

        /// <inheritdoc />
        public IHotkeyChord Clone()
        {
            var clone = new KeyboardChord(UnityService)
            {
                MainKey = MainKey?.Clone()
            };

            foreach (var modifier in Modifiers)
            {
                clone.Modifiers.Add(modifier.Clone());
            }

            return clone;
        }
    }

    /// <summary>
    /// 游戏手柄组合键。组合内没有主键和修饰键之分；触发要求全部按钮处于按下状态，且至少一个按钮在当前帧刚按下。
    /// 键与键之间的文本分隔符为 <see cref="Separator"/>。
    /// </summary>
    public class GamepadChord : IHotkeyChord
    {
        /// <summary>
        /// 组合内部键与键的文本分隔符。
        /// </summary>
        public const char Separator = '+';

        /// <summary>
        /// 组合内触发器读取手柄状态时使用的 Unity 服务。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 组合按钮触发器列表。调用方可直接修改，并应保持其非 <c>null</c>。
        /// </summary>
        public List<IHotkeyTrigger> Buttons { get; set; }

        /// <inheritdoc />
        public bool IsValid => Buttons.Count > 0;

        /// <summary>
        /// 当前按钮触发器数量。
        /// </summary>
        public int Count => Buttons.Count;

        /// <summary>
        /// 创建不含按钮的手柄组合。
        /// </summary>
        /// <param name="unityService">后续新建触发器使用的 Unity 服务；此构造函数不执行空值校验。</param>
        public GamepadChord(UnityProvider unityService)
        {
            UnityService = unityService;
            Buttons = new List<IHotkeyTrigger>();
        }

        /// <summary>
        /// 使用指定按钮触发器创建组合；触发器引用浅复制到新列表。
        /// </summary>
        /// <param name="unityService">后续新建触发器使用的 Unity 服务。</param>
        /// <param name="keys">初始按钮触发器数组，不可为 <c>null</c>。</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> 为 <c>null</c> 时由集合转换抛出。</exception>
        public GamepadChord(UnityProvider unityService, params IHotkeyTrigger[] keys)
        {
            UnityService = unityService;
            Buttons = keys.ToList();
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            if (Buttons.Count == 0)
                return false;

            return Buttons.All(b => b.IsPressed());
        }

        /// <summary>
        /// 当至少一个按钮本帧刚按下，且每个按钮当前按下或本帧刚按下时返回 <c>true</c>。
        /// </summary>
        /// <returns>手柄组合在本帧完成时为 <c>true</c>。</returns>
        public bool WasPressedThisFrame()
        {
            if (Buttons.Count == 0)
                return false;

            return Buttons.Any(b => b.WasPressedThisFrame()) && Buttons.All(b => b.IsPressed() || b.WasPressedThisFrame());
        }

        /// <inheritdoc />
        public void Clear()
        {
            Buttons.Clear();
        }

        /// <summary>
        /// 添加游戏手柄键。由于游戏手柄键之间没有主键和修饰键的区分，因此只要组合内没有重复的键就可以添加。
        /// </summary>
        /// <param name="button">要添加的手柄按钮；同一枚举值已存在时按空操作处理。</param>
        public void AddButton(GamepadButton button)
        {
            if (Buttons.OfType<GamepadTrigger>().Any(k => k.Button == button))
                return;

            Buttons.Add(new GamepadTrigger(button, UnityService));
        }

        /// <summary>
        /// 解析由加号分隔的手柄按钮文本；空分段会被跳过，解析路径不会自动去重。
        /// </summary>
        /// <param name="chordStr">手柄组合文本，不可为 <c>null</c>。</param>
        /// <param name="unityService">新触发器使用的 Unity 服务；为空时返回失败结果。</param>
        /// <returns>至少包含一个有效按钮且所有非空分段均成功时返回成功结果。</returns>
        /// <exception cref="System.NullReferenceException"><paramref name="chordStr"/> 为 <c>null</c> 时抛出。</exception>
        public static HotkeyResult<GamepadChord> TryParse(string chordStr, UnityProvider unityService)
        {
            var raw = chordStr.Split(Separator);
            var parts = new List<string>(raw.Length);
            foreach (var part in raw)
            {
                var str = part.Trim();
                if (string.IsNullOrWhiteSpace(str))
                    continue;
                parts.Add(str);
            }
            if (parts.Count == 0)
                return HotkeyResult<GamepadChord>.Fail("Failed to parse gamepad chord: empty input");

            var result = new GamepadChord(unityService);
            foreach (var part in parts)
            {
                var gamepadTriggerResult = GamepadTrigger.TryParse(part, unityService);
                if (!gamepadTriggerResult.Success)
                    return HotkeyResult<GamepadChord>.Fail($"Failed to parse gamepad modifier: {part}", gamepadTriggerResult.Errors);
                result.Buttons.Add(gamepadTriggerResult.Value);
            }

            return result;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Join(Separator.ToString(), Buttons.Select(k => k.ToString()));
        }

        /// <inheritdoc />
        public IHotkeyChord Clone()
        {
            var clone = new GamepadChord(UnityService);
            foreach (var key in Buttons)
            {
                clone.Buttons.Add(key.Clone());
            }
            return clone;
        }
    }

    /// <summary>
    /// 键盘组合或手柄组合的包装器，具体类型由内部的 <see cref="Chord"/> 决定；单个实例只包装一种组合。
    /// </summary>
    public class HotkeyChord : IHotkeyChord
    {
        /// <summary>
        /// 被包装的具体组合；为 <c>null</c> 时包装器无效且所有输入查询返回 <c>false</c>。
        /// </summary>
        public IHotkeyChord Chord { get; set; }

        /// <summary>
        /// 包装器关联的 Unity 服务。该引用不强制与已赋给 <see cref="Chord"/> 的触发器服务一致。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <inheritdoc />
        public bool IsValid => Chord?.IsValid ?? false;

        /// <summary>
        /// 创建尚未包装具体组合的实例。
        /// </summary>
        /// <param name="unityService">后续解析或上层关联使用的 Unity 服务。</param>
        public HotkeyChord(UnityProvider unityService)
        {
            UnityService = unityService;
        }

        /// <summary>
        /// 包装已有组合，不复制组合对象。
        /// </summary>
        /// <param name="chord">具体组合，可为 <c>null</c>。</param>
        /// <param name="unityService">包装器关联的 Unity 服务。</param>
        public HotkeyChord(IHotkeyChord chord, UnityProvider unityService)
        {
            Chord = chord;
            UnityService = unityService;
        }

        /// <summary>
        /// 依次按键盘组合和手柄组合解析文本，键盘解析成功时优先采用键盘语义。
        /// 为避免单键名称歧义，手柄按钮配置宜使用 <c>Gamepad</c> 前缀。
        /// </summary>
        /// <param name="chordStr">单个组合文本。</param>
        /// <param name="unityService">触发器使用的 Unity 服务。</param>
        /// <returns>任一解析器成功时返回包装组合；均失败时合并两侧错误。</returns>
        public static HotkeyResult<HotkeyChord> TryParse(string chordStr, UnityProvider unityService)
        {
            var keyboardResult = KeyboardChord.TryParse(chordStr, unityService);
            var gamepadResult = GamepadChord.TryParse(chordStr, unityService);

            if (keyboardResult.Success)
                return new HotkeyChord(keyboardResult.Value, unityService);

            if (gamepadResult.Success)
                return new HotkeyChord(gamepadResult.Value, unityService);

            return HotkeyResult<HotkeyChord>.Fail("Failed to parse hotkey chord as either keyboard or gamepad chord.", keyboardResult.Errors, gamepadResult.Errors);
        }

        /// <inheritdoc />
        public void Clear()
        {
            Chord?.Clear();
        }

        /// <inheritdoc />
        public IHotkeyChord Clone()
        {
            return new HotkeyChord(Chord?.Clone(), UnityService);
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            return Chord?.IsPressed() ?? false;
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            return Chord?.WasPressedThisFrame() ?? false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Chord?.ToString() ?? string.Empty;
        }
    }
}
