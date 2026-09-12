using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HConfigSpace;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using Moq;
using UnityEngine;

namespace UnityModBase.Test.HGuiSpace.Editor.ValueEditor
{
    public class BooleanEditorTests
    {
        [Fact]
        public void BooleanEditor_WhenConstructed_StoresUnityGuiProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new BooleanEditor(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Fact]
        public void CanEdit_WhenEntryValueTypeIsBooleanAndMetadataIsNull_ReturnsTrue()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(bool));
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.True(result);
            entryMock.VerifyGet(x => x.ValueType, Times.Once);
            entryMock.VerifyGet(x => x.Metadata, Times.Once);
        }

        [Fact]
        public void CanEdit_WhenEntryValueTypeIsNotBoolean_ReturnsFalse()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(string));

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.False(result);
            entryMock.VerifyGet(x => x.ValueType, Times.Once);
        }

        [Fact]
        public void DrawValue_WhenToggleReturnsSameValue_DoesNotChangeEntry()
        {
            // Arrange
            const bool currentValue = true;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.Toggle(currentValue, TranslatorResource.On.ToString(), It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)))
                .Returns(currentValue);
            var editor = new BooleanEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("BooleanEntry", currentValue);

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            Assert.Equal(currentValue, entryMock.Object.Value);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Toggle(currentValue, TranslatorResource.On.ToString(), It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenToggleReturnsDifferentValue_UpdatesEntry()
        {
            // Arrange
            const bool currentValue = false;
            const bool newValue = true;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.Toggle(currentValue, TranslatorResource.Off.ToString(), It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)))
                .Returns(newValue);
            var editor = new BooleanEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("BooleanEntry", currentValue);

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            Assert.Equal(newValue, entryMock.Object.Value);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Toggle(currentValue, TranslatorResource.Off.ToString(), It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == null)), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenEntryIsDualValueSlot_DoesNotExpandToggleWidth()
        {
            // Arrange：复合条目由父编辑器占满值区域，布尔槽位只应占用 toggle 内容宽度。
            const bool currentValue = true;
            GUILayoutOption compactWidth = null;
            var description = new Translator("开关说明", "Toggle description");
            var content = new GUIContent(TranslatorResource.On, description);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(false)).Returns(compactWidth);
            unityGuiMock
                .Setup(x => x.GetContent(TranslatorResource.On.ToString(), description.ToString()))
                .Returns(content);
            unityGuiMock
                .Setup(x => x.Toggle(
                    currentValue,
                    content,
                    It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], compactWidth))))
                .Returns(currentValue);
            var parentEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentEntry.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<bool, int>));
            parentEntry.SetupProperty(x => x.Value, new EntryValue<bool, int>(currentValue, 50));
            parentEntry.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            var slot = new DualValueSlotBinding(parentEntry.Object, 0, description: description);
            var editor = new BooleanEditor(unityGuiMock.Object);

            // Act
            editor.DrawValue(slot, new EditableGuiContext());

            // Assert
            unityGuiMock.Verify(x => x.ExpandWidth(false), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Never);
            unityGuiMock.Verify(x => x.Toggle(
                currentValue,
                content,
                It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], compactWidth))), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenTupleElementHasSuggestedWidth_UsesFixedWidthToggle()
        {
            // Arrange：元组元素绑定带建议宽度时，开关用固定宽度代替弹性宽度，使同列各行元素等宽对齐；
            // 元素没有独立名称标签，不走带悬停提示的内容重载。
            const bool currentValue = false;
            var offLabel = TranslatorResource.Off.ToString();
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.Width(64f)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.Toggle(currentValue, offLabel, It.IsAny<GUILayoutOption[]>()))
                .Returns(currentValue);
            var editor = new BooleanEditor(unityGuiMock.Object);
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof((bool, int)));
            parentMock.SetupProperty(x => x.Value, (currentValue, 50));
            var element = new TupleElementBinding(parentMock.Object, 0) { SuggestedWidth = 64f };

            // Act
            editor.DrawValue(element, new EditableGuiContext());

            // Assert：布局约束来自建议宽度而非弹性扩展，回显值不变不产生提交。
            unityGuiMock.Verify(x => x.Width(64f), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(It.IsAny<bool>()), Times.Never);
            unityGuiMock.Verify(x => x.GetContent(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            unityGuiMock.Verify(x => x.Toggle(currentValue, offLabel, It.IsAny<GUILayoutOption[]>()), Times.Once);
        }

        private static BooleanEditor CreateEditor()
        {
            return new BooleanEditor(new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object);
        }

        private static Mock<IEntryBinding> CreateEntry(string key, object value)
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupProperty(x => x.Value, value);
            return entryMock;
        }
    }
}
