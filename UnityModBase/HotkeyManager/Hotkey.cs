using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.HConfigSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 表示一个可由多个按键组合触发的热键配置。
    /// 配置文件格式使用逗号分隔多个组合，例如 <c>Ctrl+F1,GamepadStart</c>；任意一个组合在当前帧按下即视为触发。
    /// </summary>
    public class Hotkey : IConfigEntryValue
    {
        private static readonly UnityProvider _defaultUnityService = UnityProvider.Instance;

        /// <summary>
        /// 多个组合在配置文本中的分隔符。
        /// </summary>
        public const char Separator = ',';

        /// <summary>
        /// 全局禁用标记。设置为 <c>false</c> 后所有热键实例都不会触发。
        /// 该开关不提供嵌套禁用计数或线程同步；临时修改方必须自行协调修改顺序并恢复先前值。
        /// </summary>
        public static bool GlobalValid { get; set; } = true;

        /// <summary>
        /// 解析文本以及调用方新建组合时使用的顶层 Unity 服务。
        /// 实际触发查询由各 <see cref="HotkeyChord"/> 自己的服务完成；复制热键时不会用此引用重绑定已克隆的组合。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 可触发该热键的组合列表。列表为空时不会触发；该列表可被外部直接修改，调用方应保持其非 <c>null</c>。
        /// </summary>
        public List<HotkeyChord> Hotkeys { get; set; }

        /// <summary>
        /// 当前组合数量。
        /// </summary>
        public int Count => Hotkeys.Count;

        /// <summary>
        /// 临时禁用标记。GUI 录制热键时会把旧值标记为无效，避免录制过程中旧热键继续触发。
        /// </summary>
        public bool Valid { get; set; } = true;

        /// <summary>
        /// 使用共享 <see cref="UnityProvider.Instance"/> 创建空热键。
        /// </summary>
        public Hotkey()
        {
            UnityService = _defaultUnityService;
            Hotkeys = new List<HotkeyChord>();
        }

        /// <summary>
        /// 使用已有的组合列表与 Unity 服务构造热键实例。
        /// 该构造函数对两个参数执行非空校验，是 <see cref="TryParse"/> 与 <see cref="Clone"/> 等内部组装路径的统一入口，
        /// 不对外公开。
        /// </summary>
        /// <param name="hotkeyChords">已解析完成的组合列表；引用将被直接持有，调用方不应再继续修改。</param>
        /// <param name="unityService">热键及其组合查询输入时使用的 Unity 服务。</param>
        /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
        private Hotkey(List<HotkeyChord> hotkeyChords, UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
            Hotkeys = hotkeyChords ?? throw new ArgumentNullException(nameof(hotkeyChords));
        }

        /// <summary>
        /// 从配置文本创建热键。
        /// </summary>
        /// <param name="hotkey">逗号分隔的一个或多个组合文本。</param>
        /// <param name="unityService">各组合查询输入时使用的 Unity 服务。</param>
        /// <exception cref="ArgumentException">文本为空、格式非法或无法使用给定服务解析时抛出。</exception>
        public Hotkey(string hotkey, UnityProvider unityService)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
                throw new ArgumentException("Hotkey string cannot be null or whitespace.");

            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));

            if (TryParse(hotkey, unityService, out var parsedHotkey))
                Hotkeys = parsedHotkey.Hotkeys;
            else
                throw new ArgumentException($"Invalid hotkey string: {hotkey}");
        }

        /// <summary>
        /// 判断任一组合是否在当前帧触发。实例或全局标记无效时不查询任何组合。
        /// </summary>
        /// <returns>至少一个组合在当前帧触发时为 <c>true</c>。</returns>
        public bool WasPressedThisFrame()
        {
            if (!Valid || !GlobalValid)
                return false;

            foreach (var ch in Hotkeys)
            {
                if (ch.WasPressedThisFrame())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 添加一个热键组合；同一对象引用不会重复加入。
        /// </summary>
        /// <param name="chord">要添加的组合；<c>null</c> 按空操作处理。</param>
        public void Add(HotkeyChord chord)
        {
            if (chord != null && !Hotkeys.Contains(chord))
                Hotkeys.Add(chord);
        }

        /// <summary>
        /// 移除首个引用相等的组合；<c>null</c> 或不存在的组合按空操作处理。
        /// </summary>
        /// <param name="chord">要移除的组合。</param>
        public void Remove(HotkeyChord chord)
        {
            if (chord != null)
                Hotkeys.Remove(chord);
        }

        /// <summary>
        /// 移除列表中的 <c>null</c> 项及当前无效组合。
        /// </summary>
        public void RemoveInvalidHotkey()
        {
            Hotkeys.RemoveAll(h => h == null || !h.IsValid);
        }

        /// <summary>
        /// 比较两个热键是否包含相同组合。
        /// 比较基于各组合的规范化文本，顺序不参与比较，但重复组合的数量参与比较。
        /// </summary>
        /// <param name="other">另一个热键；为 <c>null</c> 时返回 <c>false</c>。</param>
        /// <returns>两侧非空组合文本排序后完全一致时为 <c>true</c>。</returns>
        public bool HasSameHotkey(Hotkey other)
        {
            if (other == null)
                return false;

            var thisChords = new List<string>(Hotkeys.Select(h => h.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)));
            var otherChords = new List<string>(other.Hotkeys.Select(h => h.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)));

            thisChords.Sort();
            otherChords.Sort();

            return thisChords.SequenceEqual(otherChords);
        }

        /// <summary>
        /// 从配置文本解析热键，并返回新建的 <see cref="Hotkey"/> 实例。
        /// 该方法为纯函数：不修改任何已有实例、不写日志，仅在解析成功时通过 <paramref name="result"/> 返回新对象。
        /// </summary>
        /// <param name="text"><see cref="Separator"/> 分隔的组合文本。</param>
        /// <param name="unityService">各组合查询输入时使用的 Unity 服务。</param>
        /// <param name="result">解析成功时返回的新热键；失败时为 <c>null</c>。</param>
        /// <returns>至少解析出一个组合、且没有任何空段或失败组合时为 <c>true</c>。</returns>
        /// <remarks>
        /// 解析策略偏严格：任意一个逗号分段为空白或无法识别，整体即判定失败并返回 <c>false</c>，
        /// 调用方需自行决定是否记录诊断。
        /// </remarks>
        public static bool TryParse(string text, UnityProvider unityService, out Hotkey result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(text) || unityService == null)
                return false;

            var newHotkeys = new List<HotkeyChord>();
            var chordStrings = text.Split(Separator);
            foreach (var ch in chordStrings)
            {
                var chordStr = ch.Trim();
                if (string.IsNullOrWhiteSpace(chordStr))
                    return false;

                var chordResult = HotkeyChord.TryParse(chordStr, unityService);
                if (!chordResult.Success)
                    return false;

                newHotkeys.Add(chordResult.Value);
            }

            if (newHotkeys.Count == 0)
                return false;

            result = new Hotkey(newHotkeys, unityService);
            return true;
        }

        /// <summary>
        /// 将所有非空组合文本用 <see cref="Separator"/> 连接为配置文件值。
        /// </summary>
        /// <returns>规范化后的热键文本；没有可输出组合时为空字符串。</returns>
        public override string ToString()
        {
            return string.Join(Separator.ToString(), Hotkeys.Select(h => h.ToString()).Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// 深复制当前组合结构，并沿用当前热键及各组合的 Unity 服务引用。
        /// </summary>
        /// <returns>独立的热键对象。</returns>
        public Hotkey Clone()
        {
            var chords = new List<HotkeyChord>(Hotkeys.Select(h => h.Clone() as HotkeyChord));
            return new Hotkey(chords, UnityService);
        }

        /// <inheritdoc />
        public ConfigFileResult<string> Encode()
        {
            return ToString();
        }

        /// <inheritdoc />
        /// <remarks>
        /// 框架通过无参构造函数创建临时实例调用本方法，实现内部委托给静态 <see cref="TryParse(string, UnityProvider, out Hotkey)"/>，
        /// 返回的是解析所得的新实例而非临时实例自身；临时实例的状态不参与结果。
        /// </remarks>
        public ConfigFileResult<object> Decode(string content)
        {
            if (TryParse(content, _defaultUnityService, out var result))
                return result;
            return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Failed to decode Hotkey"));
        }

        /// <inheritdoc />
        public ConfigFileResult<string> EncodeValueType()
        {
            return "Hotkey";
        }

        /// <summary>
        /// 比较当前热键与另一个配置值是否包含相同组合。
        /// 仅当 <paramref name="other"/> 同样是 <see cref="Hotkey"/> 时，比较才有意义，
        /// 否则直接返回 <c>false</c>；具体相等规则见 <see cref="HasSameHotkey(Hotkey)"/>。
        /// </summary>
        /// <param name="other">另一个配置值，可能为 <c>null</c> 或其他实现类型。</param>
        /// <returns>类型一致且组合集合等价时为 <c>true</c>。</returns>
        public bool Equals(IConfigEntryValue other)
        {
            if (other == null || other.GetType() != typeof(Hotkey))
                return false;
            return HasSameHotkey((Hotkey)other);
        }
    }
}
