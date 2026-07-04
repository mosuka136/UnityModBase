using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Resource
{
    public class LayoutResourceTests
    {
        [Fact]
        public void LayoutResource_WhenConstructedWithUnityGui_StoresUnityGui()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var resource = new LayoutResource(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, resource.UnityGui);
        }

        [Fact]
        public void GetEntryLabelWidth_WhenRootIsNull_ReturnsZero()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var resource = new LayoutResource(unityGuiMock.Object);

            // Act
            var result = resource.GetEntryLabelWidth(null);

            // Assert
            Assert.Equal(0f, result);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetGroupButtonWidth_WhenRootIsNull_ReturnsZero()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var resource = new LayoutResource(unityGuiMock.Object);

            // Act
            var result = resource.GetGroupButtonWidth(null);

            // Assert
            Assert.Equal(0f, result);
            unityGuiMock.VerifyNoOtherCalls();
        }

    }
}
