using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.InputSystem;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 键盘组合键。由一个主键和零个或多个修饰键组成，触发时要求所有修饰键保持按下状态，并在同一帧按下主键。
    /// 键与键之间的文本分隔符为 <see cref="Separator"/>。
    /// </summary>
    /// <remarks>
    /// 主键和修饰键使用通用触发器接口以支持扩展，但本类不会校验外部写入的触发器类型、空值或输入服务。
    /// <see cref="SetMainKey(Key)"/>、<see cref="AddModifier(Key)"/> 和 <see cref="TryParse(string, UnityProvider)"/> 才会建立标准键盘结构。
    /// </remarks>
    public class KeyboardChord : IHotkeyChord
    {
        /// <summary>
        /// 组合内部修饰键与主键的文本分隔符。
        /// </summary>
        public const char Separator = '+';

        /// <summary>
        /// 辅助方法创建新触发器时使用的 Unity 服务；已有触发器仍读取各自持有的服务。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 触发主键前必须保持按下的条件集合。调用方可直接修改，应保持列表及元素非 <c>null</c>。
        /// 标准添加和解析路径只写入 <see cref="KeyboardModifierTrigger"/>，直接写入的其他实现不会被类型校验。
        /// </summary>
        public List<IHotkeyTrigger> Modifiers { get; set; }
        /// <summary>
        /// 当前帧刚按下时触发该组合的主条件。标准设置和解析路径使用 <see cref="KeyboardTrigger"/>；可为 <c>null</c> 表示未配置。
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
        /// 添加键盘修饰键。如果左右两侧同类修饰键都被录入，会把已有的精确侧别条件合并为不区分左右。
        /// 重复与合并检查只覆盖列表中的 <see cref="KeyboardModifierTrigger"/>，自定义触发器不参与判断。
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
        /// <param name="key">
        /// 要设置的键；<see cref="Key.None"/> 或未定义枚举值会创建一个形式上非空、但内部仍为 <see cref="Key.None"/> 的触发器，
        /// 此时 <see cref="IsValid"/> 为 <c>true</c>，输入查询仍不会触发。
        /// </param>
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
}
