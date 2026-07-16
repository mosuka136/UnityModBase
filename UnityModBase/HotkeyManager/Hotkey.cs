using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HConfigSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 表示一个可由多个按键组合触发的热键配置。
    /// 配置文件格式使用逗号分隔多个组合，例如 <c>Ctrl+F1,GamepadStart</c>；任意一个组合在当前帧按下即视为触发。
    /// </summary>
    public class Hotkey : IConfigEntryAdapter
    {
        private static readonly UnityProvider _defaultUnityService = UnityProvider.Instance;

        /// <summary>
        /// 多个组合在配置文本中的分隔符。
        /// </summary>
        public const char Separator = ',';

        /// <summary>
        /// 全局禁用标记。设置为 <c>false</c> 后所有热键实例都不会触发。
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
        /// 使用指定 Unity 服务创建空热键。
        /// </summary>
        /// <param name="unityService">输入状态提供器；此构造函数不执行空值校验。</param>
        public Hotkey(UnityProvider unityService)
        {
            UnityService = unityService;
            Hotkeys = new List<HotkeyChord>();
        }

        /// <summary>
        /// 从配置文本创建热键。
        /// </summary>
        /// <param name="hotkey">逗号分隔的一个或多个组合文本。</param>
        /// <param name="unityService">各组合查询输入时使用的 Unity 服务。</param>
        /// <exception cref="ArgumentException">文本为空、格式非法或无法使用给定服务解析时抛出。</exception>
        public Hotkey(string hotkey, UnityProvider unityService)
        {
            UnityService = unityService;
            Hotkeys = new List<HotkeyChord>();
            if (!TryParse(hotkey))
                throw new ArgumentException($"Invalid hotkey string: {hotkey}");
        }

        /// <summary>
        /// 深复制源热键的组合结构，并为新热键设置指定服务。
        /// 内部组合按各自的克隆规则保留源组合的 Unity 服务引用，不会自动重绑定到参数服务。
        /// </summary>
        /// <param name="hotkey">要复制的源热键，不可为 <c>null</c>。</param>
        /// <param name="unityService">新热键顶层使用的 Unity 服务。</param>
        /// <exception cref="NullReferenceException"><paramref name="hotkey"/> 为 <c>null</c> 时抛出。</exception>
        public Hotkey(Hotkey hotkey, UnityProvider unityService)
        {
            UnityService = unityService;
            Hotkeys = hotkey.Hotkeys.Select(h => h.Clone() as HotkeyChord).ToList();
        }

        /// <summary>
        /// 使用指定组合创建热键。组合引用会浅复制到新的列表，组合对象本身仍与调用方共享。
        /// </summary>
        /// <param name="unityService">热键顶层使用的 Unity 服务。</param>
        /// <param name="hotkeys">初始组合数组，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="hotkeys"/> 为 <c>null</c> 时由集合转换抛出。</exception>
        public Hotkey(UnityProvider unityService, params HotkeyChord[] hotkeys)
        {
            UnityService = unityService;
            Hotkeys = hotkeys.ToList();
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
        /// 从配置文本解析热键。空的逗号分段会被跳过；任一非空组合失败时保留原列表并记录警告。
        /// </summary>
        /// <param name="text"><see cref="Separator"/> 分隔的组合文本。</param>
        /// <returns>至少解析出一个组合且所有非空组合均成功时为 <c>true</c>。</returns>
        public bool TryParse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                BLog.Warn("Failed to parse hotkey: input is empty.");
                return false;
            }

            var newHotkeys = new List<HotkeyChord>();
            var chordStrings = text.Split(Separator);
            foreach (var ch in chordStrings)
            {
                var chordStr = ch.Trim();
                if (string.IsNullOrWhiteSpace(chordStr))
                    continue;

                var chordResult = HotkeyChord.TryParse(chordStr, UnityService);
                if (!chordResult.Success)
                {
                    foreach (var error in chordResult.Errors)
                    {
                        BLog.Warn($"Failed to parse hotkey chord '{chordStr}': {error}");
                    }
                    return false;
                }
                newHotkeys.Add(chordResult.Value);
            }

            if (newHotkeys.Count == 0)
            {
                BLog.Warn("Failed to parse hotkey: no valid chords found.");
                return false;
            }

            Hotkeys = newHotkeys;
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
            return new Hotkey(this, UnityService);
        }

        /// <inheritdoc />
        public ConfigFileResult<string> Encode()
        {
            return ToString();
        }

        /// <inheritdoc />
        public ConfigFileResult<object> Decode(string content)
        {
            if (TryParse(content))
                return this;
            return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Failed to decode Hotkey"));
        }

        /// <inheritdoc />
        public ConfigFileResult<string> EncodeValueType()
        {
            return "Hotkey";
        }
    }
}
