using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Editor
{
    public class EntryEditorTests
    {
        [Fact]
        public void Constructor_ValidDependencies_StoresUnityGui()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var registry = new ValueEditorRegistry();

            var editor = new EntryEditor(unityGui.Object, registry);

            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Equal(0f, editor.TrailingActionWidth);
        }

        [Fact]
        public void Draw_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var registry = new ValueEditorRegistry();
            var editor = new EntryEditor(unityGui.Object, registry);

            Assert.Equal("entry", Assert.Throws<ArgumentNullException>(() =>
                editor.Draw(null, new EditableGuiContext())).ParamName);
        }

        [Fact]
        public void Constructor_WhenDependencyIsNull_ThrowsArgumentNullException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var registry = new ValueEditorRegistry();

            Assert.Equal("unity", Assert.Throws<ArgumentNullException>(() =>
                new EntryEditor(null, registry)).ParamName);
            Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() =>
                new EntryEditor(unityGui.Object, (ValueEditorRegistry)null)).ParamName);
        }

        [Fact]
        public void Draw_WhenContextIsInvalid_ReturnsWithoutResolvingEditor()
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var valueEditor = new Mock<IValueEditor>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var registry = new ValueEditorRegistry();
            registry.RegisterEditor(valueEditor.Object);
            var editor = new EntryEditor(unityGui.Object, registry);

            editor.Draw(entry.Object, new InvalidContext());

            valueEditor.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
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
                .Setup(x => x.DrawValue(entry.Object, It.IsAny<EditableGuiContext>()))
                .Throws(new InvalidOperationException("value editor failed"));
            using var registry = new ValueEditorRegistry();
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
            var editor = new EntryEditor(unityGui.Object, registry);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                editor.Draw(entry.Object, new EditableGuiContext()));

            Assert.Equal("value editor failed", exception.Message);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
            valueEditor.Verify(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Never);
        }

        private sealed class InvalidContext : EditableGuiContext
        {
            internal InvalidContext()
                : base(false)
            {
            }
        }
    }
}
