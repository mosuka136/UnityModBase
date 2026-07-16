using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    /// <summary>
    /// 为带 <see cref="UiSliderMetadata"/> 的数值配置组合滑条与文本框输入。
    /// 滑条只限制显示位置，不会仅因绘制而覆盖原有越界配置值；实际写回仍遵循数值编辑器的延迟转换规则。
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
        public StyleResource StyleProvider { get; }

        /// <summary>
        /// 创建带文本输入的滑条编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制滑条和文本框的 IMGUI 提供器。</param>
        /// <param name="unityService">提供夹取、吸附和近似比较的 Unity 服务。</param>
        /// <param name="styleProvider">提供滑条样式的资源。</param>
        public SliderEditor(IUnityGuiProvider unityGui, IUnityProvider unityService, StyleResource styleProvider)
            : base(unityGui)
        {
            UnityService = unityService;
            StyleProvider = styleProvider;
        }

        /// <inheritdoc/>
        public override bool CanEdit(IEntryBinding entry)
        {
            return base.CanEdit(entry) && entry.Metadata is UiSliderMetadata;
        }

        /// <inheritdoc/>
        public override void DrawValue(IEntryBinding entry, GuiContext context)
        {
            if (!entry.ValueType.IsPrimitive || entry.ValueType == typeof(bool) || entry.ValueType == typeof(char))
                return;

            var metadata = entry.Metadata as UiSliderMetadata;
            if (metadata == null)
            {
                UnityGui.Label(UnityGui.GetContent(TranslatorResource.InvalidSliderMetadata), UnityGui.ExpandWidth(true));
                return;
            }

            var valueString = ValueProvider.GetValue(entry).ToString();
            var value = ValueProvider.GetValidValue<float>(entry);
            var displayValue = UnityService.Clamp(value, metadata.Min, metadata.Max);
            var newSliderValue = UnityGui.HorizontalSlider(
                displayValue,
                metadata.Min,
                metadata.Max,
                StyleProvider.SliderStyle,
                StyleProvider.SliderThumbStyle,
                UnityGui.ExpandWidth(true));

            // 显示值会被夹在滑条范围内；如果用户未拖动滑条，应保留原始越界值，避免单纯绘制就改写配置。
            if (UnityService.Approximately(displayValue, newSliderValue))
                newSliderValue = value;

            if (!UnityService.Approximately(value, newSliderValue) && metadata.Step > 0f)
                newSliderValue = UnityService.Round(newSliderValue / metadata.Step) * metadata.Step;

            var sliderString = newSliderValue.ToString();
            var newValueString = UnityGui.TextField(sliderString, UnityGui.MinWidth(50f), UnityGui.ExpandWidth(false));

            if (valueString != newValueString)
                context.ChangeSink.SetConvertedValue(entry, newValueString, DelayApplyDuration);
        }
    }
}
