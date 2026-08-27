using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HProvider;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class UserEditorTests
    {
        [Fact]
        public void Constructor_ValidDependencies_InitializesPropertiesAndContextFreeGroupEditor()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleResource = new StyleResource(null);

            // Act
            var editor = new UserEditor(
                unityProvider.Object,
                unityGui.Object,
                styleResource);

            // Assert
            Assert.Same(unityProvider.Object, editor.UnityService);
            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.NotNull(editor.GroupEditor);
            Assert.Same(unityProvider.Object, editor.GroupEditor.UnityService);
            Assert.Same(unityGui.Object, editor.GroupEditor.UnityGui);
            Assert.Same(styleResource, editor.GroupEditor.StyleProvider);
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
                new StyleResource(null));
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            object currentValue = "old";
            entry.SetupGet(x => x.Value).Returns(() => currentValue);
            entry.SetupSet(x => x.Value = It.IsAny<object>())
                .Callback<object>(value => currentValue = value);
            guiContext.ChangeSink.SetValue(entry.Object, "new", delay: 0.25f);

            // Act
            editor.Update(guiContext, 0.25f);

            // Assert
            Assert.Equal("new", currentValue);
            entry.VerifySet(x => x.Value = It.Is<object>(value => Equals(value, "new")), Times.Once);
        }
    }
}
