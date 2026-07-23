using System;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 为数值配置项声明 GUI 滑条范围。
    /// 该特性只影响配置界面的展示和输入方式，不会自动限制配置文件中已存在的值。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ConfigSliderAttribute : Attribute
    {
        /// <summary>
        /// 滑条最小显示值。
        /// </summary>
        public float Min { get; set; }
        /// <summary>
        /// 滑条最大显示值。
        /// </summary>
        public float Max { get; set; }
        /// <summary>
        /// 滑条吸附步长；正值时以 <see cref="Min"/> 为吸附起点，小于等于 0 时不吸附。
        /// </summary>
        public float Step { get; set; }

        /// <summary>
        /// 创建滑条元数据。
        /// </summary>
        /// <param name="min">滑条最小显示值。</param>
        /// <param name="max">滑条最大显示值。</param>
        /// <param name="step">以 <paramref name="min"/> 为起点的吸附步长；默认 <c>-1</c> 表示连续取值。</param>
        public ConfigSliderAttribute(float min, float max, float step = -1f)
        {
            Min = min;
            Max = max;
            Step = step;
        }
    }
}
