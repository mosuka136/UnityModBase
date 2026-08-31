using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 多元素条目绑定的通用 GUI 契约：在 <see cref="IEntryBinding"/> 之上暴露元素数量与
    /// “整体/各元素”两级说明，使组合编辑器（<c>DualValueEditor</c>）能为每个元素槽位提供
    /// 独立提示，而不依赖配置或控制空间的具体绑定实现。
    /// </summary>
    public interface IEntryMultipleBinding : IEntryBinding
    {
        /// <summary>获取元素数量；组合编辑器据此校验可投影性，当前仅支持 2。</summary>
        int Count { get; }

        /// <summary>获取不含分元素文本的整体说明；条目行的名称提示使用它。</summary>
        Translator BaseDescription { get; }

        /// <summary>
        /// 获取各元素说明，下标与元素顺序一一对应，长度必须等于 <see cref="Count"/>；
        /// 组合编辑器按下标取用，作为对应元素控件的悬停提示。
        /// </summary>
        Translator[] ValueDescription { get; }
    }
}
