using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Resource
{
    public class StyleResourceTests
    {
        [Fact]
        public void StyleResource_WhenConstructedWithUnityGui_StoresUnityGui()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var resource = new StyleResource(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, resource.UnityGui);
        }

    }
}
