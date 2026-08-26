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
    /// <summary>
    /// 值编辑器选择依赖全进程共享的静态注册表；每个测试开始前先释放并清空，
    /// 保证 Draw 选中的编辑器一定是本测试注册的编辑器。
    /// </summary>
    public class EntryEditorTests
    {
        public EntryEditorTests()
        {
            ValueEditorRegistry.Dispose();
        }

        [Fact]
        public void Constructor_ValidUnityGui_StoresDependencyWithoutContext()
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            var editor = new EntryEditor(unityGuiMock.Object);

            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Fact]
        public void Render_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(unityGuiMock.Object);

            var exception = Assert.Throws<ArgumentNullException>(() => editor.Draw(null, new GuiContext()));

            Assert.Equal("entry", exception.ParamName);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new EntryEditor(null));

            Assert.Equal("unity", exception.ParamName);
        }

        [Fact]
        public void Render_WhenContextIsInvalid_ReturnsWithoutDrawing()
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var valueEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            valueEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);
            ValueEditorRegistry.RegisterEditor(valueEditorMock.Object);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(unityGuiMock.Object);

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
            ValueEditorRegistry.RegisterEditor(valueEditor.Object);
            GUILayoutOption width = null;
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.GetContent(name, description)).Returns((GUIContent)null);
            unityGui.Setup(x => x.Width(0f)).Returns(width);
            unityGui.Setup(x => x.Label(
                (GUIContent)null,
                It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], width))));
            unityGui.Setup(x => x.EndHorizontal());
            var editor = new EntryEditor(unityGui.Object);
            var context = new GuiContext();

            var exception = Assert.Throws<InvalidOperationException>(() => editor.Draw(entry.Object, context));

            Assert.Equal("value editor failed", exception.Message);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
            valueEditor.Verify(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<GuiContext>()), Times.Never);
        }
    }
}
