using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor.ValueEditor
{
    public class UnsupportedEditorTests
    {
        [Fact]
        public void UnsupportedEditor_WhenConstructed_InitializesUnityGuiProvider()
        {
            // Arrange

            // Act
            var editor = new UnsupportedEditor();

            // Assert
            Assert.NotNull(editor.UnityGui);
            Assert.IsType<UnityGuiProvider>(editor.UnityGui);
        }

        [Fact]
        public void CanEdit_WhenEntryIsNull_ReturnsFalse()
        {
            // Arrange
            var editor = new UnsupportedEditor();

            // Act
            var result = editor.CanEdit(null);

            // Assert
            Assert.False(result);
        }

    }
}
