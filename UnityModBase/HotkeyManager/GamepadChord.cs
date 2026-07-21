using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem.LowLevel;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 游戏手柄组合键。组合内没有主键和修饰键之分；触发要求全部按钮处于按下状态，且至少一个按钮在当前帧刚按下。
    /// 键与键之间的文本分隔符为 <see cref="Separator"/>。
    /// </summary>
    /// <remarks>
    /// <see cref="Buttons"/> 使用通用触发器接口以支持扩展，但本类不会校验元素是否确为手柄按钮、是否为 <c>null</c>
    /// 或是否共享同一输入服务；<see cref="AddButton(GamepadButton)"/> 和 <see cref="TryParse(string, UnityProvider)"/> 才会建立标准手柄触发器。
    /// </remarks>
    public class GamepadChord : IHotkeyChord
    {
        /// <summary>
        /// 组合内部键与键的文本分隔符。
        /// </summary>
        public const char Separator = '+';

        /// <summary>
        /// <see cref="AddButton(GamepadButton)"/> 创建新触发器时使用的 Unity 服务；已有触发器仍读取各自持有的服务。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 组合按钮触发器列表。调用方可直接修改，应保持列表及元素非 <c>null</c>，并自行维持手柄输入语义。
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
        /// 添加游戏手柄键。重复检查只比较已有的 <see cref="GamepadTrigger"/>，自定义触发器不参与去重。
        /// </summary>
        /// <param name="button">
        /// 要添加的手柄按钮；同一枚举值已存在时按空操作处理。未定义枚举值会被新触发器拒绝，
        /// 使其保留默认的 <see cref="GamepadButton.South"/>。由于重复检查发生在该回退之前，未定义值可能追加重复的 South，
        /// 调用方应只传入已定义枚举值。
        /// </param>
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
}
