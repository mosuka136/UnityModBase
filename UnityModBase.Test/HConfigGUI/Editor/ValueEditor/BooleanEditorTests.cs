using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HConfigSpace;
using UnityModBase.HProvider;
using Moq;
using UnityEngine;

namespace UnityModBase.Test.HConfigGUI.Editor.ValueEditor
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
            editor.DrawValue(entryMock.Object, new GuiStateStore());

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
            editor.DrawValue(entryMock.Object, new GuiStateStore());

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
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(false)).Returns(compactWidth);
            unityGuiMock
                .Setup(x => x.Toggle(
                    currentValue,
                    TranslatorResource.On.ToString(),
                    It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], compactWidth))))
                .Returns(currentValue);
            var parentEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentEntry.SetupGet(x => x.ValueType).Returns(typeof(ConfigEntryValue<bool, int>));
            parentEntry.SetupProperty(x => x.Value, new ConfigEntryValue<bool, int>(currentValue, 50));
            parentEntry.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            var slot = new DualValueSlotBinding(parentEntry.Object, 0);
            var editor = new BooleanEditor(unityGuiMock.Object);

            // Act
            editor.DrawValue(slot, new GuiStateStore());

            // Assert
            unityGuiMock.Verify(x => x.ExpandWidth(false), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Never);
            unityGuiMock.Verify(x => x.Toggle(
                currentValue,
                TranslatorResource.On.ToString(),
                It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], compactWidth))), Times.Once);
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
