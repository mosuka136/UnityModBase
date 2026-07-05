using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HProvider;
using UnityModBase.HConfigGUI.Resource;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class UserEditorTests
    {
        [Fact]
        public void Constructor_ValidDependencies_InitializesPropertiesAndGroupEditor()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var guiContext = new GuiContext();
            var styleResource = new StyleResource(null);
            var layoutResource = new LayoutResource(unityGui.Object);

            // Act
            var editor = new UserEditor(
                unityProvider.Object,
                unityGui.Object,
                styleResource,
                layoutResource,
                guiContext);

            // Assert
            Assert.Same(unityProvider.Object, editor.UnityService);
            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Same(styleResource, editor.StyleProvider);
            Assert.NotNull(editor.GroupEditor);
            Assert.Same(unityProvider.Object, editor.GroupEditor.UnityService);
            Assert.Same(unityGui.Object, editor.GroupEditor.UnityGui);
            Assert.Same(guiContext.GuiStateStore, editor.GroupEditor.GuiStateStore);
            Assert.Same(guiContext.ChangeSink, editor.GroupEditor.ChangeSink);
            Assert.Same(styleResource, editor.GroupEditor.StyleProvider);
            Assert.Same(layoutResource, editor.GroupEditor.LayoutProvider);
        }


        [Fact]
        public void Update_DelayedEntryExists_FlushesPendingValueThroughGroupEditor()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var guiContext = new GuiContext();
            var editor = new UserEditor(
                unityProvider.Object,
                unityGui.Object,
                new StyleResource(null),
                new LayoutResource(unityGui.Object),
                guiContext);
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            object currentValue = "old";
            entry.SetupGet(x => x.Value).Returns(() => currentValue);
            entry.SetupSet(x => x.Value = It.IsAny<object>())
                .Callback<object>(value => currentValue = value);
            editor.GroupEditor.ChangeSink.SetValue(entry.Object, "new", delay: 0.25f);

            // Act
            editor.Update(0.25f);

            // Assert
            Assert.Equal("new", currentValue);
            entry.VerifySet(x => x.Value = It.Is<object>(value => Equals(value, "new")), Times.Once);
        }
    }
}
