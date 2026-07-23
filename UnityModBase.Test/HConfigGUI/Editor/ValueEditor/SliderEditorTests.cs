using Moq;
using UnityEngine;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.Test.HConfigGUI.Editor.ValueEditor
{
    public class SliderEditorTests
    {
        [Fact]
        public void CanEdit_WhenEntryIsPrimitiveAndHasSliderMetadata_ReturnsTrue()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = CreateEntry(typeof(int), 5, new UiSliderMetadata(0f, 10f, 1f));

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanEdit_WhenEntryIsPrimitiveAndMetadataIsNotSlider_ReturnsFalse()
        {
            // Arrange
            var editor = CreateEditor();
            var metadataMock = new Mock<IUiMetadata>(MockBehavior.Strict);
            var entryMock = CreateEntry(typeof(int), 5, metadataMock.Object);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanEdit_WhenEntryTypeIsNotEditable_ReturnsFalse()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = CreateEntry(typeof(bool), true, new UiSliderMetadata(0f, 1f, 1f));

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(typeof(string), "value")]
        [InlineData(typeof(bool), true)]
        [InlineData(typeof(char), 'A')]
        public void DrawValue_WhenEntryTypeIsNotSupported_ReturnsWithoutDrawing(Type valueType, object value)
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new SliderEditor(unityGuiMock.Object, new Mock<IUnityProvider>(MockBehavior.Strict).Object, new StyleResource(null));
            var entryMock = CreateEntry(valueType, value, new UiSliderMetadata(0f, 10f, 1f));
            var state = new GuiStateStore();
            var changeSink = state.ChangeSink;

            // Act
            editor.DrawValue(entryMock.Object, state);
            changeSink.FlushValue(1.0f);

            // Assert
            Assert.Equal(value, entryMock.Object.Value);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenMetadataIsMissing_DrawsInvalidMetadataLabelAndReturns()
        {
            // Arrange
            var invalidMetadataText = TranslatorResource.InvalidSliderMetadata.ToString();
            var content = new GUIContent(invalidMetadataText);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.GetContent(invalidMetadataText)).Returns(content);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.Label(content, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)));
            var editor = new SliderEditor(unityGuiMock.Object, new Mock<IUnityProvider>(MockBehavior.Strict).Object, new StyleResource(null));
            var entryMock = CreateEntry(typeof(int), 5, null);
            var state = new GuiStateStore();

            // Act
            editor.DrawValue(entryMock.Object, state);

            // Assert
            unityGuiMock.Verify(x => x.GetContent(invalidMetadataText), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Label(content, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenSliderIsUnchanged_PreservesInvalidBufferedText()
        {
            // Arrange
            const int currentValue = 5;
            const string invalidText = "invalid";
            var metadata = new UiSliderMetadata(0f, 10f, 1f);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateDrawableEditor(unityGuiMock);
            var entryMock = CreateEntry(typeof(int), currentValue, metadata);
            var context = new GuiStateStore();
            context.ChangeSink.SetConvertedValue(entryMock.Object, invalidText, editor.DelayApplyDuration);
            unityGuiMock
                .Setup(x => x.HorizontalSlider(
                    currentValue,
                    metadata.Min,
                    metadata.Max,
                    editor.StyleProvider.SliderStyle,
                    editor.StyleProvider.SliderThumbStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue);
            unityGuiMock
                .Setup(x => x.TextField(invalidText, It.IsAny<GUILayoutOption[]>()))
                .Returns(invalidText);

            // Act
            editor.DrawValue(entryMock.Object, context);

            // Assert
            Assert.Equal(invalidText, ValueProvider.GetValue(entryMock.Object));
            Assert.Equal(currentValue, ValueProvider.GetValidValue(entryMock.Object));
            Assert.Equal(currentValue, entryMock.Object.Value);
        }

        [Fact]
        public void DrawValue_WhenOutOfRangeSliderIsUnchanged_PreservesOriginalValue()
        {
            // Arrange
            const int currentValue = 15;
            const float displayValue = 10f;
            var metadata = new UiSliderMetadata(0f, 10f, 1f);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateDrawableEditor(unityGuiMock);
            var entryMock = CreateEntry(typeof(int), currentValue, metadata);
            var context = new GuiStateStore();
            unityGuiMock
                .Setup(x => x.HorizontalSlider(
                    displayValue,
                    metadata.Min,
                    metadata.Max,
                    editor.StyleProvider.SliderStyle,
                    editor.StyleProvider.SliderThumbStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(displayValue);
            unityGuiMock
                .Setup(x => x.TextField(currentValue.ToString(), It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue.ToString());

            // Act
            editor.DrawValue(entryMock.Object, context);
            context.ChangeSink.FlushValue(editor.DelayApplyDuration);

            // Assert
            Assert.False(entryMock.Object.EditBuffer.IsUsing);
            Assert.Equal(currentValue, entryMock.Object.Value);
        }

        [Fact]
        public void DrawValue_WhenOnlyTextChangesBeyondRange_AppliesWithoutSliderClampingAfterDelay()
        {
            // Arrange
            const int currentValue = 5;
            const int typedValue = 15;
            var metadata = new UiSliderMetadata(0f, 10f, 1f);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateDrawableEditor(unityGuiMock);
            var entryMock = CreateEntry(typeof(int), currentValue, metadata);
            var context = new GuiStateStore();
            unityGuiMock
                .Setup(x => x.HorizontalSlider(
                    currentValue,
                    metadata.Min,
                    metadata.Max,
                    editor.StyleProvider.SliderStyle,
                    editor.StyleProvider.SliderThumbStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue);
            unityGuiMock
                .Setup(x => x.TextField(currentValue.ToString(), It.IsAny<GUILayoutOption[]>()))
                .Returns(typedValue.ToString());

            // Act
            editor.DrawValue(entryMock.Object, context);
            context.ChangeSink.FlushValue(0.4f);
            var valueBeforeDelayExpires = entryMock.Object.Value;
            var bufferedValueBeforeDelayExpires = ValueProvider.GetValue(entryMock.Object);
            context.ChangeSink.FlushValue(0.1f);

            // Assert
            Assert.Equal(typedValue, bufferedValueBeforeDelayExpires);
            Assert.Equal(currentValue, valueBeforeDelayExpires);
            Assert.Equal(typedValue, entryMock.Object.Value);
        }

        [Fact]
        public void DrawValue_WhenSliderMoves_SnapsFromMinimumAndAppliesAfterDelay()
        {
            // Arrange
            const int currentValue = 2;
            const float sliderValue = 4.2f;
            const float snappedValue = 5f;
            var metadata = new UiSliderMetadata(1f, 10f, 2f);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateDrawableEditor(unityGuiMock);
            var entryMock = CreateEntry(typeof(int), currentValue, metadata);
            var context = new GuiStateStore();
            unityGuiMock
                .Setup(x => x.HorizontalSlider(
                    currentValue,
                    metadata.Min,
                    metadata.Max,
                    editor.StyleProvider.SliderStyle,
                    editor.StyleProvider.SliderThumbStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(sliderValue);
            unityGuiMock
                .Setup(x => x.TextField(snappedValue.ToString(), It.IsAny<GUILayoutOption[]>()))
                .Returns(snappedValue.ToString());

            // Act
            editor.DrawValue(entryMock.Object, context);
            context.ChangeSink.FlushValue(0.4f);
            var valueBeforeDelayExpires = entryMock.Object.Value;
            var bufferedValueBeforeDelayExpires = ValueProvider.GetValue(entryMock.Object);
            context.ChangeSink.FlushValue(0.1f);

            // Assert
            Assert.Equal((int)snappedValue, bufferedValueBeforeDelayExpires);
            Assert.Equal(currentValue, valueBeforeDelayExpires);
            Assert.Equal((int)snappedValue, entryMock.Object.Value);
        }

        [Fact]
        public void DrawValue_WhenSnappedValueExceedsMaximum_ClampsToMaximum()
        {
            // Arrange
            const int currentValue = 4;
            const float sliderValue = 10f;
            var metadata = new UiSliderMetadata(1f, 10f, 6f);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateDrawableEditor(unityGuiMock);
            var entryMock = CreateEntry(typeof(int), currentValue, metadata);
            var context = new GuiStateStore();
            unityGuiMock
                .Setup(x => x.HorizontalSlider(
                    currentValue,
                    metadata.Min,
                    metadata.Max,
                    editor.StyleProvider.SliderStyle,
                    editor.StyleProvider.SliderThumbStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(sliderValue);
            unityGuiMock
                .Setup(x => x.TextField(metadata.Max.ToString(), It.IsAny<GUILayoutOption[]>()))
                .Returns(metadata.Max.ToString());

            // Act
            editor.DrawValue(entryMock.Object, context);
            context.ChangeSink.FlushValue(editor.DelayApplyDuration);

            // Assert
            Assert.Equal((int)metadata.Max, entryMock.Object.Value);
        }

        [Theory]
        [InlineData(-1f)]
        [InlineData(0f)]
        public void DrawValue_WhenStepIsNotPositive_UsesContinuousSliderValue(float step)
        {
            // Arrange
            const float currentValue = 2f;
            const float sliderValue = 4.25f;
            var metadata = new UiSliderMetadata(0f, 10f, step);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateDrawableEditor(unityGuiMock);
            var entryMock = CreateEntry(typeof(float), currentValue, metadata);
            var context = new GuiStateStore();
            unityGuiMock
                .Setup(x => x.HorizontalSlider(
                    currentValue,
                    metadata.Min,
                    metadata.Max,
                    editor.StyleProvider.SliderStyle,
                    editor.StyleProvider.SliderThumbStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(sliderValue);
            unityGuiMock
                .Setup(x => x.TextField(sliderValue.ToString(), It.IsAny<GUILayoutOption[]>()))
                .Returns(sliderValue.ToString());

            // Act
            editor.DrawValue(entryMock.Object, context);
            context.ChangeSink.FlushValue(editor.DelayApplyDuration);

            // Assert
            Assert.Equal(sliderValue, entryMock.Object.Value);
        }

        private static SliderEditor CreateEditor()
        {
            return new SliderEditor(
                new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object,
                new Mock<IUnityProvider>(MockBehavior.Strict).Object,
                new StyleResource(null));
        }

        private static SliderEditor CreateDrawableEditor(Mock<IUnityGuiProvider> unityGuiMock)
        {
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock.Setup(x => x.MinWidth(50f)).Returns((GUILayoutOption)null);
            unityGuiMock.Setup(x => x.ExpandWidth(false)).Returns((GUILayoutOption)null);

            var styleResource = new StyleResource(null);
            var styleResourceType = typeof(StyleResource);
            var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            // 测试进程没有 Unity 原生 GUI 运行时：绕过 GUIStyle 构造函数创建仅供引用传递的占位对象，
            // 并禁止终结器访问未初始化的原生指针，再将其注入样式缓存以避免触发延迟创建。
            var sliderStyle = (GUIStyle)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            var sliderThumbStyle = (GUIStyle)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(sliderStyle);
            GC.SuppressFinalize(sliderThumbStyle);
            styleResourceType.GetField("_sliderStyle", bindingFlags).SetValue(styleResource, sliderStyle);
            styleResourceType.GetField("_sliderThumbStyle", bindingFlags).SetValue(styleResource, sliderThumbStyle);

            return new SliderEditor(
                unityGuiMock.Object,
                UnityProvider.Instance,
                styleResource);
        }

        private static Mock<IEntryBinding> CreateEntry(Type valueType, object value, IUiMetadata metadata, string key = "SliderEntry")
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            entryMock.SetupProperty(x => x.Value, value);
            entryMock.SetupGet(x => x.Metadata).Returns(metadata);
            return entryMock;
        }
    }
}
