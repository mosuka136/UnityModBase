using System;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 为数值配置属性声明 GUI 滑条的显示范围和可选吸附步长。
    /// 该特性经配置 GUI 转换为控件元数据，不参与配置读取、持久化或值域验证。
    /// </summary>
    /// <remarks>
    /// 参数会原样保存；构造过程不会拒绝反向范围、NaN 或无穷值，声明方应确保这些值可供 Unity 滑条使用。
    /// 仅滑条交互会应用范围和步长，文本输入以及配置文件中的既有值仍可超出该范围。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ConfigSliderAttribute : Attribute
    {
        /// <summary>
        /// 获取或设置滑条最小显示值；不校验其与 <see cref="Max"/> 的关系。
        /// </summary>
        public float Min { get; set; }
        /// <summary>
        /// 获取或设置滑条最大显示值；不校验其与 <see cref="Min"/> 的关系。
        /// </summary>
        public float Max { get; set; }
        /// <summary>
        /// 获取或设置滑条吸附步长；仅当值大于 0 时，以 <see cref="Min"/> 为吸附起点。
        /// </summary>
        public float Step { get; set; }

        /// <summary>
        /// 创建用于声明数值配置项滑条范围的特性。
        /// </summary>
        /// <param name="min">滑条最小显示值。</param>
        /// <param name="max">滑条最大显示值。</param>
        /// <param name="step">以 <paramref name="min"/> 为起点的吸附步长；默认 <c>-1</c>，仅大于 0 时启用吸附。</param>
        /// <remarks>构造函数不校验参数的顺序、有限性或步长有效性。</remarks>
        public ConfigSliderAttribute(float min, float max, float step = -1f)
        {
            Min = min;
            Max = max;
            Step = step;
        }
    }
}
