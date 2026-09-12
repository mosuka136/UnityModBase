using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Editor.ValueEditor
{
    public class NumberEditorTests
    {
        [Fact]
        public void NumberEditor_WhenConstructed_StoresUnityGuiProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new NumberEditor(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
            Assert.Equal(0.5f, editor.DelayApplyDuration);
        }

        [Theory]
        [InlineData(typeof(int), true)]
        [InlineData(typeof(bool), false)]
        [InlineData(typeof(char), false)]
        [InlineData(typeof(string), false)]
        public void CanEdit_WhenEntryHasDifferentValueTypesAndNoMetadata_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            if (expected)
                entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.Equal(expected, result);
            entryMock.VerifyGet(x => x.ValueType, Times.Once);
            entryMock.VerifyGet(x => x.Metadata, expected ? Times.Once() : Times.Never());
        }

        [Theory]
        [InlineData(typeof(string), "value")]
        [InlineData(typeof(bool), true)]
        [InlineData(typeof(char), 'A')]
        public void DrawValue_WhenEntryTypeIsNotEditable_ReturnsWithoutDrawing(Type valueType, object value)
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new NumberEditor(unityGuiMock.Object);
            var state = new EditableGuiContext();
            var changeSink = state.ChangeSink;
            var entryMock = CreateEntry("NonEditableEntry", valueType, value);
            var originalValue = entryMock.Object.Value;

            // Act
            editor.DrawValue(entryMock.Object, state);
            changeSink.FlushValue(1.0f);

            // Assert
            Assert.Equal(originalValue, entryMock.Object.Value);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenTextFieldReturnsSameValue_DoesNotQueueChange()
        {
            // Arrange
            const int currentValue = 123;
            const string currentValueText = "123";
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValueText, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)))
                .Returns(currentValueText);
            var editor = new NumberEditor(unityGuiMock.Object);
            var state = new EditableGuiContext();
            var changeSink = state.ChangeSink;
            var entryMock = CreateEntry("SameNumberEntry", typeof(int), currentValue);
            // Act
            editor.DrawValue(entryMock.Object, state);
            changeSink.FlushValue(1.0f);

            // Assert
            Assert.False(entryMock.Object.EditBuffer.IsUsing);
            Assert.Equal(currentValue, entryMock.Object.Value);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.TextField(currentValueText, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenTextFieldReturnsDifferentValue_QueuesConvertedChange()
        {
            // Arrange
            const int currentValue = 123;
            const int newValue = 456;
            const string currentValueText = "123";
            const string newValueText = "456";
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValueText, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)))
                .Returns(newValueText);
            var editor = new NumberEditor(unityGuiMock.Object)
            {
                DelayApplyDuration = 0.5f,
            };
            var state = new EditableGuiContext();
            var changeSink = state.ChangeSink;
            var entryMock = CreateEntry("ChangedNumberEntry", typeof(int), currentValue);
            // Act
            editor.DrawValue(entryMock.Object, state);
            changeSink.FlushValue(0.4f);
            var valueBeforeDelayExpires = entryMock.Object.Value;
            var bufferedValueBeforeDelayExpires = ValueProvider.GetValue(entryMock.Object);
            changeSink.FlushValue(0.1f);

            // Assert
            Assert.Equal(newValue, bufferedValueBeforeDelayExpires);
            Assert.Equal(currentValue, valueBeforeDelayExpires);
            Assert.Equal(newValue, entryMock.Object.Value);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.TextField(currentValueText, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenInputIsInvalid_UsesBufferedTextWithoutChangingEntry()
        {
            const int currentValue = 123;
            const string currentValueText = "123";
            const string invalidValueText = "invalid";
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .SetupSequence(x => x.TextField(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()))
                .Returns(invalidValueText)
                .Returns(invalidValueText);
            var editor = new NumberEditor(unityGuiMock.Object);
            var context = new EditableGuiContext();
            var entryMock = CreateEntry("InvalidNumberEntry", typeof(int), currentValue);

            editor.DrawValue(entryMock.Object, context);

            Assert.Equal(invalidValueText, ValueProvider.GetValue(entryMock.Object));
            Assert.Equal(currentValue, ValueProvider.GetValidValue(entryMock.Object));

            editor.DrawValue(entryMock.Object, context);
            context.ChangeSink.FlushValue(0.5f);

            Assert.Equal(currentValue, entryMock.Object.Value);
            unityGuiMock.Verify(
                x => x.TextField(currentValueText, It.IsAny<GUILayoutOption[]>()),
                Times.Once);
            unityGuiMock.Verify(
                x => x.TextField(invalidValueText, It.IsAny<GUILayoutOption[]>()),
                Times.Once);
        }

        [Fact]
        public void DrawValue_WhenTupleElementHasSuggestedWidth_UsesFixedWidthOption()
        {
            // Arrange：元组元素绑定带建议宽度时，文本框用固定宽度代替弹性宽度，使同列各行元素等宽对齐。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.Width(72f)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()))
                .Returns("42");
            var editor = new NumberEditor(unityGuiMock.Object);
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof((int, string)));
            parentMock.SetupProperty(x => x.Value, (42, "a"));
            var element = new TupleElementBinding(parentMock.Object, 0) { SuggestedWidth = 72f };

            // Act
            editor.DrawValue(element, new EditableGuiContext());

            // Assert：布局约束来自建议宽度而非弹性扩展，回显值不变不产生提交。
            unityGuiMock.Verify(x => x.Width(72f), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(It.IsAny<bool>()), Times.Never);
            unityGuiMock.Verify(x => x.TextField("42", It.IsAny<GUILayoutOption[]>()), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenEntryIsDualValueSlot_SetsTooltipOnTextField()
        {
            // Arrange
            const int currentValue = 123;
            var description = new Translator("数值说明", "Number description");
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValue.ToString(), It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue.ToString());
            unityGuiMock.Setup(x => x.SetLastControlTooltip(description.ToString()));
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));
            parentMock.SetupProperty(x => x.Value, new EntryValue<int, string>(currentValue, "value"));
            parentMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            var slot = new DualValueSlotBinding(parentMock.Object, 0, description: description);
            var editor = new NumberEditor(unityGuiMock.Object);

            // Act
            editor.DrawValue(slot, new EditableGuiContext());

            // Assert
            unityGuiMock.Verify(x => x.SetLastControlTooltip(description.ToString()), Times.Once);
        }

        private static NumberEditor CreateEditor()
        {
            return new NumberEditor(new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object);
        }

        private static Mock<IEntryBinding> CreateEntry(string key, Type valueType, object value)
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            entryMock.SetupProperty(x => x.Value, value);
            return entryMock;
        }
    }
}
