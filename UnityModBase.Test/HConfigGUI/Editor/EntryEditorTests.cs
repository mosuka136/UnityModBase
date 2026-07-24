using Moq;
using System;
using UnityEngine;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

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

            var exception = Assert.Throws<ArgumentNullException>(() => editor.Draw(null, new GuiContext()));

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

            editor.Draw(entryMock.Object, GuiContext.InvalidGuiContext);

            unityGuiMock.VerifyNoOtherCalls();
            valueEditorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenValueEditorThrows_StillEndsHorizontalAndSkipsExtraArea()
        {
            var name = new Translator("名称", "Name");
            var description = new Translator("说明", "Description");
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.Name).Returns(name);
            entry.SetupGet(x => x.Description).Returns(description);
            var valueEditor = new Mock<IValueEditor>(MockBehavior.Strict);
            valueEditor.Setup(x => x.CanEdit(entry.Object)).Returns(true);
            valueEditor
                .Setup(x => x.DrawValue(entry.Object, It.IsAny<GuiContext>()))
                .Throws(new InvalidOperationException("value editor failed"));
            var registry = new ValueEditorRegistry();
            registry.RegisterEditor(valueEditor.Object);
            GUILayoutOption width = null;
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.GetContent(name, description)).Returns((GUIContent)null);
            unityGui.Setup(x => x.Width(0f)).Returns(width);
            unityGui.Setup(x => x.Label(
                (GUIContent)null,
                It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], width))));
            unityGui.Setup(x => x.EndHorizontal());
            var editor = new EntryEditor(registry, unityGui.Object);
            var context = new GuiContext();

            var exception = Assert.Throws<InvalidOperationException>(() => editor.Draw(entry.Object, context));

            Assert.Equal("value editor failed", exception.Message);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
            valueEditor.Verify(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<GuiContext>()), Times.Never);
        }
    }
}
