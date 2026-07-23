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
    /// 保存数值配置项的滑条显示范围和可选吸附步长，供
    /// <see cref="Editor.ValueEditor.SliderEditor"/> 选择并绘制控件。
    /// 本类型只传递不可变的展示元数据，不负责验证或修改底层配置值。
    /// </summary>
    /// <remarks>
    /// 参数不会在构造时校验。滑条路径会夹取和吸附用户拖动后的值，但仅重绘不会修正已有越界配置；
    /// 同一编辑器中的文本输入也不受该范围或步长约束。
    /// </remarks>
    public class UiSliderMetadata : IUiMetadata
    {
        /// <summary>
        /// 获取当前实现对应的元数据运行时类型。
        /// </summary>
        public Type MetadataType => typeof(UiSliderMetadata);
        /// <summary>
        /// 获取滑条最小显示值；不校验其与 <see cref="Max"/> 的关系。
        /// </summary>
        public float Min { get; }
        /// <summary>
        /// 获取滑条最大显示值；不校验其与 <see cref="Min"/> 的关系。
        /// </summary>
        public float Max { get; }
        /// <summary>
        /// 获取滑条吸附步长；仅当值大于 0 时，以 <see cref="Min"/> 为吸附起点。
        /// </summary>
        public float Step { get; }
        /// <summary>
        /// 创建滑条展示元数据。
        /// </summary>
        /// <param name="min">最小显示值。</param>
        /// <param name="max">最大显示值。</param>
        /// <param name="step">以 <paramref name="min"/> 为起点的吸附步长；仅大于 0 时启用吸附。</param>
        /// <remarks>构造函数不校验参数的顺序、有限性或步长有效性。</remarks>
        public UiSliderMetadata(float min, float max, float step)
        {
            Min = min;
            Max = max;
            Step = step;
        }
    }
}
