using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    /// <summary>
    /// 为带 <see cref="UiSliderMetadata"/> 的数值配置组合滑条与文本框输入。
    /// 拖动值按最小值起点和正步长吸附；未拖动时保留当前文本和夹取前的有效值，不会因浮点格式化改写配置。
    /// 文本输入的实际写回仍遵循数值编辑器的延迟转换规则。
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

            var value = ValueProvider.GetValidValue<float>(entry);
            var displayValue = UnityService.Clamp(value, metadata.Min, metadata.Max);
            var newSliderValue = UnityGui.HorizontalSlider(
                displayValue,
                metadata.Min,
                metadata.Max,
                StyleProvider.SliderStyle,
                StyleProvider.SliderThumbStyle,
                UnityGui.ExpandWidth(true));

            // 当前提供器只返回滑条值，需与本帧传入的显示值作近似比较来识别用户是否拖动。
            var sliderChanged = true;
            if (UnityService.Approximately(displayValue, newSliderValue))
            {
                // 未拖动时恢复夹取前的有效值，避免界面重绘自动修正已有的越界配置。
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
