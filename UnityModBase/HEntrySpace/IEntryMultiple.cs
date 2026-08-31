using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 多元素条目的共享只读视图：在 <see cref="IEntry"/> 之上额外暴露元素数量，
    /// 以及“整体说明”与“各元素说明”两级文本，供 GUI 绑定层在不依赖具体空间的前提下
    /// 为每个元素槽位提供独立提示。
    /// </summary>
    /// <remarks>
    /// 当前唯一实现是配置空间的双元素门面 <c>ConfigEntry&lt;T1, T2&gt;</c>（<see cref="Count"/> 为 2）。
    /// 通用组合编辑器以本契约为绘制前提：只有实现本接口的条目才会被投影为逐元素编辑，
    /// 仅有复合值类型而未实现本接口的条目不提供分元素说明来源。
    /// </remarks>
    public interface IEntryMultiple : IEntry
    {
        /// <summary>获取元素数量；实现必须与值类型的实际元素个数一致。</summary>
        int Count { get; }

        /// <summary>
        /// 获取不含分元素文本的整体说明；GUI 条目行的名称提示使用它，
        /// 避免与各元素控件的悬停提示重复展示相同内容。
        /// </summary>
        Translator BaseDescription { get; }

        /// <summary>
        /// 获取各元素说明，下标与元素顺序一一对应，长度必须等于 <see cref="Count"/>；
        /// 组合编辑器按下标取用，作为对应元素控件的悬停提示。
        /// 数组实例直接暴露给调用方，元素不应被外部修改。
        /// </summary>
        Translator[] ValueDescription { get; }
    }
}
