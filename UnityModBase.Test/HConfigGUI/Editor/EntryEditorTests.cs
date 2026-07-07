using Moq;
using System;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HProvider;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class EntryEditorTests
    {
        [Fact]
        public void Constructor_ValidDependencies_StoresDependenciesWithoutContext()
        {
            var registry = new ValueEditorRegistry();
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            var editor = new EntryEditor(registry, unityGuiMock.Object);

            Assert.Same(registry, editor.Registry);
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Fact]
        public void Render_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(new ValueEditorRegistry(), unityGuiMock.Object);

            var exception = Assert.Throws<ArgumentNullException>(() => editor.Render(null, new GuiContext()));

            Assert.Equal("entry", exception.ParamName);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            var registry = new ValueEditorRegistry();

            var exception = Assert.Throws<ArgumentNullException>(() => new EntryEditor(registry, null));

            Assert.Equal("unity", exception.ParamName);
        }

        [Fact]
        public void Render_WhenContextIsInvalid_ReturnsWithoutDrawing()
        {
            var registry = new ValueEditorRegistry();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var valueEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            valueEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);
            registry.RegisterEditor(valueEditorMock.Object);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(registry, unityGuiMock.Object);

            editor.Render(entryMock.Object, GuiContext.InvalidGuiContext);

            unityGuiMock.VerifyNoOtherCalls();
            valueEditorMock.VerifyNoOtherCalls();
        }
    }
}
