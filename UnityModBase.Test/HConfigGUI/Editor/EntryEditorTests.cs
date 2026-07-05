using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class EntryEditorTests
    {
        [Fact]
        public void EntryEditor_WhenConstructed_StoresDependenciesAndClearsStateOnGuiPipeEvents()
        {
            // Arrange
            var registry = new ValueEditorRegistry();
            var state = new GuiStateStore();
            var changeSink = state.ChangeSink;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.Key).Returns("EntryKey");
            var textKey = state.GetKey(entryMock.Object, "text");
            var boolKey = state.GetKey(entryMock.Object, "bool");
            state.SetText(textKey, "value");
            state.SetBool(boolKey, true);

            // Act
            var editor = new EntryEditor(registry, state, unityGuiMock.Object);
            GuiPipe.InvokeOnEntryEditFinished(entryMock.Object);
            state.SetText(textKey, "value");
            state.SetBool(boolKey, true);
            GuiPipe.InvokeOnEntryValueReset(entryMock.Object);

            // Assert
            Assert.Same(registry, editor.Registry);
            Assert.Same(state, editor.Context);
            Assert.Same(changeSink, editor.Context.ChangeSink);
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
            Assert.Equal(string.Empty, state.GetText(textKey));
            Assert.False(state.GetBool(boolKey));
        }

        [Fact]
        public void Render_WhenEntryIsNull_ReturnsWithoutInvokingGui()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(new ValueEditorRegistry(), new GuiStateStore(), unityGuiMock.Object);

            // Act
            editor.Render(null);

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Render_WhenUnityGuiIsNull_ReturnsWithoutDrawing()
        {
            // Arrange
            var registry = new ValueEditorRegistry();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var valueEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            valueEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);
            registry.RegisterEditor(valueEditorMock.Object);
            var editor = new EntryEditor(registry, new GuiStateStore(), null);

            // Act
            editor.Render(entryMock.Object);

            // Assert
            valueEditorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            valueEditorMock.VerifyNoOtherCalls();
        }
    }
}
