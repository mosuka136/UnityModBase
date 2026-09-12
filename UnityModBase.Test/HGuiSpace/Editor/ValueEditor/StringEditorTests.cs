using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Editor.ValueEditor
{
    public class StringEditorTests
    {
        [Fact]
        public void StringEditor_WhenConstructed_StoresUnityGuiProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new StringEditor(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
            Assert.Equal(0.5f, editor.DelayApplyDuration);
        }

        [Fact]
        public void CanEdit_WhenEntryValueTypeIsStringAndMetadataIsNull_ReturnsTrue()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(string));
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.True(result);
            entryMock.VerifyGet(x => x.ValueType, Times.Once);
            entryMock.VerifyGet(x => x.Metadata, Times.Once);
        }

        [Fact]
        public void CanEdit_WhenEntryValueTypeIsNotString_ReturnsFalse()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(int));

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.False(result);
            entryMock.VerifyGet(x => x.ValueType, Times.Once);
        }

        [Fact]
        public void DrawValue_WhenTextFieldReturnsSameValue_DoesNotQueueChange()
        {
            // Arrange
            const string currentValue = "value";
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValue, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)))
                .Returns(currentValue);
            var editor = new StringEditor(unityGuiMock.Object);
            var state = new EditableGuiContext();
            var changeSink = state.ChangeSink;
            var entryMock = CreateEntry("SameEntry", currentValue);

            // Act
            editor.DrawValue(entryMock.Object, state);
            changeSink.FlushValue(1.0f);

            // Assert
            Assert.False(entryMock.Object.EditBuffer.IsUsing);
            Assert.Equal(currentValue, entryMock.Object.Value);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.TextField(currentValue, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenTextFieldReturnsDifferentValue_UpdatesStateAndQueuesDelayedChange()
        {
            // Arrange
            const string currentValue = "old";
            const string newValue = "new";
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValue, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)))
                .Returns(newValue);
            var editor = new StringEditor(unityGuiMock.Object)
            {
                DelayApplyDuration = 0.5f,
            };
            var state = new EditableGuiContext();
            var changeSink = state.ChangeSink;
            var entryMock = CreateEntry("ChangedEntry", currentValue);

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
            unityGuiMock.Verify(x => x.TextField(currentValue, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenEntryIsDualValueSlot_SetsTooltipOnTextField()
        {
            // Arrange
            const string currentValue = "value";
            var description = new Translator("字符串说明", "String description");
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValue, It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue);
            unityGuiMock.Setup(x => x.SetLastControlTooltip(description.ToString()));
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<string, int>));
            parentMock.SetupProperty(x => x.Value, new EntryValue<string, int>(currentValue, 1));
            parentMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            var slot = new DualValueSlotBinding(parentMock.Object, 0, description: description);
            var editor = new StringEditor(unityGuiMock.Object);

            // Act
            editor.DrawValue(slot, new EditableGuiContext());

            // Assert
            unityGuiMock.Verify(x => x.SetLastControlTooltip(description.ToString()), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenTupleElementHasSuggestedWidth_UsesFixedWidthTextField()
        {
            // Arrange：元组元素绑定带建议宽度时，文本框用固定宽度代替弹性宽度，使同列各行元素等宽对齐；
            // 元素不是双元素槽位，不补设悬停提示。
            const string currentValue = "a";
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.Width(56f)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.TextField(currentValue, It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue);
            var editor = new StringEditor(unityGuiMock.Object);
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof((int, string)));
            parentMock.SetupProperty(x => x.Value, (1, currentValue));
            var element = new TupleElementBinding(parentMock.Object, 1) { SuggestedWidth = 56f };

            // Act
            editor.DrawValue(element, new EditableGuiContext());

            // Assert：布局约束来自建议宽度而非弹性扩展，回显值不变不产生提交。
            unityGuiMock.Verify(x => x.Width(56f), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(It.IsAny<bool>()), Times.Never);
            unityGuiMock.Verify(x => x.SetLastControlTooltip(It.IsAny<string>()), Times.Never);
            unityGuiMock.Verify(x => x.TextField(currentValue, It.IsAny<GUILayoutOption[]>()), Times.Once);
        }

        private static StringEditor CreateEditor()
        {
            return new StringEditor(new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object);
        }

        private static Mock<IEntryBinding> CreateEntry(string key, string value)
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupProperty(x => x.Value, value);
            return entryMock;
        }
    }
}
