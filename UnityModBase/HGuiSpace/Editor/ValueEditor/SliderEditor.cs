using System;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 为带 <see cref="UiSliderMetadata"/> 的基础数值配置组合滑条与文本框输入。
    /// 滑条路径负责显示夹取和可选步长吸附，文本路径仍沿用数值编辑器的转换与延迟提交流程，
    /// 不受滑条范围或步长约束。
    /// 仅重绘时会保留暂存文本、原数值格式和已有越界配置，避免把显示转换误当作用户编辑。
    /// </summary>
    public class SliderEditor : NumberEditor, IValueEditor
    {
        /// <summary>
        /// 获取滑条夹取、吸附和近似比较使用的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }

        /// <summary>
        /// 获取滑条轨道和滑块样式资源。
        /// </summary>
        public IEntryStyleResource StyleProvider { get; }

        /// <summary>
        /// 创建带文本输入的滑条编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制滑条和文本框的 IMGUI 提供器。</param>
        /// <param name="unityService">提供夹取、吸附和近似比较的 Unity 服务。</param>
        /// <param name="styleProvider">提供滑条样式的资源。</param>
        public SliderEditor(IUnityGuiProvider unityGui, IUnityProvider unityService, IEntryStyleResource styleProvider)
            : base(unityGui)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
            StyleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider));
        }

        /// <inheritdoc/>
        public override bool CanEdit(IEntryBinding entry)
        {
            if (entry == null)
                return false;

            var type = entry.ValueType;
            return type.IsPrimitive &&
                   type != typeof(bool) &&
                   type != typeof(char) &&
                   entry.Metadata is UiSliderMetadata;
        }

        /// <summary>
        /// 绘制滑条和文本框，并在控件值发生变化时把新文本交给当前用户上下文的变更提交器。
        /// 滑条未产生值变化时沿用编辑缓冲区中的原文本和夹取前的有效值。
        /// </summary>
        /// <param name="entry">要绘制的数值条目绑定。</param>
        /// <param name="context">提供变更提交器的用户 GUI 上下文。</param>
        /// <remarks>
        /// 正常调用路径应先通过 <see cref="CanEdit"/> 选择本编辑器；直接传入不支持的类型时不绘制，
        /// 缺少滑条元数据时显示错误标签。只有滑条返回的新值会执行范围夹取和步长吸附；
        /// 文本框返回值由提交器转换，有效值按 <see cref="NumberEditor.DelayApplyDuration"/> 延迟或立即写回，
        /// 无效文本则仅保留在编辑缓冲区中。
        /// </remarks>
        public override void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            if (!entry.ValueType.IsPrimitive || entry.ValueType == typeof(bool) || entry.ValueType == typeof(char))
                return;

            var metadata = entry.Metadata as UiSliderMetadata;
            if (metadata == null)
            {
                UnityGui.Label(UnityGui.GetContent(TranslatorResource.InvalidSliderMetadata), UnityGui.ExpandWidth(true));
                return;
            }

            var value = ValueProvider.GetValidValue<float>(entry);
            var displayValue = UnityService.Clamp(value, metadata.Min, metadata.Max);
            var newSliderValue = UnityGui.HorizontalSlider(
                displayValue,
                metadata.Min,
                metadata.Max,
                StyleProvider.SliderStyle,
                StyleProvider.SliderThumbStyle,
                UnityGui.ExpandWidth(true));

            // IMGUI 抽象没有暴露独立的滑条变更标记，只能比较返回值与传入值；
            // 在 Unity 浮点容差内相等时，视为本帧没有产生可观察的滑条值变化。
            var sliderChanged = true;
            if (UnityService.Approximately(displayValue, newSliderValue))
            {
                // 恢复夹取前的有效值，避免单纯重绘自动修正已有的越界配置。
                newSliderValue = value;
                sliderChanged = false;
            }

            if (!UnityService.Approximately(value, newSliderValue) && metadata.Step > 0f)
            {
                // 步进网格以 Min 而非 0 为起点；吸附后再次夹取，防止末端不足一个步长时舍入越界。
                newSliderValue = UnityService.Clamp(
                    UnityService.Round(
                        (newSliderValue - metadata.Min) / metadata.Step) * metadata.Step + metadata.Min,
                    metadata.Min,
                    metadata.Max);
            }

            var valueString = ValueProvider.GetValue(entry).ToString();

            string sliderString;
            if (sliderChanged)
                sliderString = newSliderValue.ToString();
            else
                // 保留无效暂存文本及原数值格式；用转换后的 float 回显会被误判为一次文本编辑。
                sliderString = valueString;

            var newValueString = UnityGui.TextField(sliderString, UnityGui.MinWidth(50f), UnityGui.ExpandWidth(false));

            if (valueString != newValueString)
                context.ChangeSink.SetConvertedValue(entry, newValueString, DelayApplyDuration);
        }
    }
}
