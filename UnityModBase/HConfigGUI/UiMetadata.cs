using System;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 配置项 GUI 元数据接口。
    /// 元数据只影响控件选择和展示方式，不改变配置值的文件格式。
    /// </summary>
    public interface IUiMetadata
    {
        /// <summary>
        /// 获取实现声明的元数据运行时类型。
        /// </summary>
        Type MetadataType { get; }
    }

    /// <summary>
    /// 数值配置项的滑条元数据。
    /// </summary>
    public class UiSliderMetadata : IUiMetadata
    {
        /// <summary>
        /// 获取滑条元数据类型。
        /// </summary>
        public Type MetadataType => typeof(UiSliderMetadata);
        /// <summary>
        /// 滑条最小显示值；构造函数不校验其与 <see cref="Max"/> 的关系。
        /// </summary>
        public float Min { get; }
        /// <summary>
        /// 滑条最大显示值；构造函数不校验其与 <see cref="Min"/> 的关系。
        /// </summary>
        public float Max { get; }
        /// <summary>
        /// 滑条吸附步长；正值时以 <see cref="Min"/> 为吸附起点，小于等于 0 时不吸附。
        /// </summary>
        public float Step { get; }
        /// <summary>
        /// 创建滑条展示元数据。范围合法性由声明方保证，编辑器只负责夹取显示值和可选步进吸附。
        /// </summary>
        /// <param name="min">最小显示值。</param>
        /// <param name="max">最大显示值。</param>
        /// <param name="step">以 <paramref name="min"/> 为起点的吸附步长；小于等于 0 表示连续值。</param>
        public UiSliderMetadata(float min, float max, float step)
        {
            Min = min;
            Max = max;
            Step = step;
        }
    }
}
