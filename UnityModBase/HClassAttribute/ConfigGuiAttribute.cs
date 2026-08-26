using System;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 组合声明标记：声明一个配置属性上打包了多个 GUI 元数据。
    /// 本特性只提供组合长度，自身不携带子元数据；子声明是同一属性上的其他特性
    /// （如带索引的 <see cref="ConfigSliderAttribute"/>），各自声明在组合中的槽位，
    /// 由配置 GUI 展开为 <see cref="T:UnityModBase.HConfigGUI.UiConfigMetadata"/>。
    /// 与其他 GUI 特性一样，只影响控件展示，不参与配置读取、持久化或值域验证。
    /// </summary>
    /// <remarks>
    /// 组合用法依赖在同一属性上重复标注子特性，因此子特性类型必须声明 <c>AllowMultiple = true</c>
    /// （当前仅 <see cref="ConfigSliderAttribute"/> 支持）；新增可组合的子特性类型时同样需要放开该限制。
    /// 未通过带索引构造函数声明的子特性（<see cref="ConfigSliderAttribute.Index"/> 为 <c>-1</c>）不占用槽位，展开时会被跳过。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ConfigGuiAttribute : Attribute, IConfigGuiAttribute
    {
        /// <summary>
        /// 获取组合声明的槽位数量；没有子声明提供有效索引的槽位在展开结果中保持 <c>null</c>。
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// 创建组合声明标记。
        /// </summary>
        /// <param name="count">组合中的槽位数量，必须大于 0。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于等于 0 时抛出。</exception>
        public ConfigGuiAttribute(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");
            Count = count;
        }
    }
}
