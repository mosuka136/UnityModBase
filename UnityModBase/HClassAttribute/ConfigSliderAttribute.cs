using System;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 为数值配置属性声明 GUI 滑条的显示范围和可选吸附步长。
    /// 该特性经配置 GUI 转换为控件元数据，不参与配置读取、持久化或值域验证。
    /// </summary>
    /// <remarks>
    /// 构造函数要求范围和步长均为有限数值，且最小值不得大于最大值；非正步长表示禁用吸附。
    /// 元数据在构造后不可变，配置 GUI 可直接使用已验证的范围和步长。
    /// 仅滑条交互会应用范围和步长，文本输入以及配置文件中的既有值仍可超出该范围。
    /// 除单独声明外，可经带索引的构造函数与 <see cref="ConfigGuiAttribute"/> 组合，
    /// 为同一配置属性打包多个滑条声明，每个槽位独立生效。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public class ConfigSliderAttribute : Attribute, IConfigGuiAttribute
    {
        /// <summary>
        /// 获取已验证的滑条最小显示值。
        /// </summary>
        public float Min { get; }
        /// <summary>
        /// 获取已验证的滑条最大显示值。
        /// </summary>
        public float Max { get; }
        /// <summary>
        /// 获取滑条吸附步长；仅当值大于 0 时，以 <see cref="Min"/> 为吸附起点。
        /// </summary>
        public float Step { get; }

        /// <summary>
        /// 获取本声明在 <see cref="ConfigGuiAttribute"/> 组合中的槽位下标；
        /// <c>-1</c>（默认）表示未指定，组合展开路径会跳过未指定槽位的滑条声明。
        /// </summary>
        public int Index { get; } = -1;

        /// <summary>
        /// 创建用于声明数值配置项滑条范围的特性。
        /// </summary>
        /// <param name="min">有限的滑条最小显示值，不得大于 <paramref name="max"/>。</param>
        /// <param name="max">有限的滑条最大显示值。</param>
        /// <param name="step">有限的吸附步长，以 <paramref name="min"/> 为起点；默认 <c>-1</c>，仅大于 0 时启用吸附。</param>
        /// <exception cref="ArgumentException">
        /// 任一参数为 NaN 或无穷值，或 <paramref name="min"/> 大于 <paramref name="max"/> 时抛出。
        /// </exception>
        /// <remarks>步长为 0 或负数是受支持的禁用吸附约定，不会被视为无效参数。</remarks>
        public ConfigSliderAttribute(float min, float max, float step = -1f)
        {
            if (float.IsNaN(min) || float.IsNaN(max) || float.IsNaN(step))
                throw new ArgumentException("Slider range and step cannot be NaN.");
            if (float.IsInfinity(min) || float.IsInfinity(max) || float.IsInfinity(step))
                throw new ArgumentException("Slider range and step cannot be infinite.");
            if (min > max)
                throw new ArgumentException("Slider min value cannot be greater than max value.");

            Min = min;
            Max = max;
            Step = step;
        }

        /// <summary>
        /// 创建用于 <see cref="ConfigGuiAttribute"/> 组合声明中指定槽位的滑条特性。
        /// 范围与步长参数的约束与 <see cref="ConfigSliderAttribute(float, float, float)"/> 相同。
        /// </summary>
        /// <param name="index">在组合声明中的槽位下标，写入组合元数据数组的对应位置。</param>
        /// <param name="min">有限的滑条最小显示值，不得大于 <paramref name="max"/>。</param>
        /// <param name="max">有限的滑条最大显示值。</param>
        /// <param name="step">有限的吸附步长，以 <paramref name="min"/> 为起点；默认 <c>-1</c>，仅大于 0 时启用吸附。</param>
        /// <remarks>
        /// <paramref name="index"/> 不做校验：负值等效于未指定；不小于组合长度的越界正值
        /// 会在元数据解析阶段触发异常，并被调用方降级为“无元数据”（整体返回 <c>null</c>）。
        /// </remarks>
        public ConfigSliderAttribute(int index, float min, float max, float step = -1f) : this(min, max, step)
        {
            Index = index;
        }
    }
}
