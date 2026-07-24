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
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ConfigSliderAttribute : Attribute
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
    }
}
