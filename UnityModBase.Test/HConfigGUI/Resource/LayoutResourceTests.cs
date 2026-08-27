using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Resource
{
    public class GroupEditorLayoutTests
    {
        [Fact]
        public void GetEntryLabelWidth_WhenRootIsNull_ReturnsZero()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateEditor(unityGuiMock);

            // Act
            var result = editor.GetEntryLabelWidth(null);

            // Assert
            Assert.Equal(0f, result);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetGroupButtonWidth_WhenRootIsNull_ReturnsZero()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateEditor(unityGuiMock);

            // Act
            var result = editor.GetGroupButtonWidth(null);

            // Assert
            Assert.Equal(0f, result);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetEntryLabelWidth_WhenRootHasNoEntries_ReturnsLabelPadding()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateEditor(unityGuiMock);
            var root = new GroupBinding("Root", null, null);

            // Act
            var result = editor.GetEntryLabelWidth(root);

            // Assert
            Assert.Equal(10f, result);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetGroupButtonWidth_WhenRootHasNoChildGroups_ReturnsButtonPadding()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = CreateEditor(unityGuiMock);
            var root = new GroupBinding("Root", null, null);

            // Act
            var result = editor.GetGroupButtonWidth(root);

            // Assert
            Assert.Equal(60f, result);
            unityGuiMock.VerifyNoOtherCalls();
        }

        private static GroupEditor CreateEditor(Mock<IUnityGuiProvider> unityGuiMock)
        {
            var unityProviderMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            return new GroupEditor(
                unityProviderMock.Object,
                unityGuiMock.Object,
                new StyleResource(null));
        }
    }
}
