using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using Moq;
using UnityEngine;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class ToastEditorTests
    {
        [Fact]
        public void ToastEditor_WhenConstructed_StoresDependencies()
        {
            // Arrange
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGuiMock.Object);

            // Act
            var editor = new ToastEditor(unityServiceMock.Object, unityGuiMock.Object, styleProvider);

            // Assert
            Assert.Same(unityServiceMock.Object, editor.UnityService);
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
            Assert.Same(styleProvider, editor.StyleProvider);
        }

        [Fact]
        public void SetToast_WhenCalled_StoresMessageAndCalculatesEndTime()
        {
            // Arrange
            const float realtimeSinceStartup = 10f;
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityServiceMock.SetupGet(x => x.RealtimeSinceStartup).Returns(realtimeSinceStartup);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new ToastEditor(unityServiceMock.Object, unityGuiMock.Object, new StyleResource(unityGuiMock.Object))
            {
                Duration = 2.5f,
            };

            // Act
            editor.SetToast("Toast message");

            // Assert
            Assert.Equal("Toast message", editor.Message);
            Assert.Equal(12.5f, editor.EndTime);
            unityServiceMock.VerifyGet(x => x.RealtimeSinceStartup, Times.Once);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void DrawToast_WhenMessageIsNullOrEmpty_ReturnsWithoutDrawing(string message)
        {
            // Arrange
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new ToastEditor(unityServiceMock.Object, unityGuiMock.Object, new StyleResource(unityGuiMock.Object))
            {
                Message = message,
            };

            // Act
            editor.DrawToast(new Rect(0f, 0f, 200f, 100f));

            // Assert
            Assert.Equal(message, editor.Message);
            unityServiceMock.VerifyNoOtherCalls();
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawToast_WhenToastHasExpired_ClearsMessageAndReturns()
        {
            // Arrange
            const float currentTime = 5f;
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityServiceMock.SetupGet(x => x.RealtimeSinceStartup).Returns(currentTime);
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new ToastEditor(unityServiceMock.Object, unityGuiMock.Object, new StyleResource(unityGuiMock.Object))
            {
                Message = "Expired toast",
                EndTime = currentTime,
            };

            // Act
            editor.DrawToast(new Rect(0f, 0f, 200f, 100f));

            // Assert
            Assert.Null(editor.Message);
            unityServiceMock.VerifyGet(x => x.RealtimeSinceStartup, Times.Once);
            unityGuiMock.VerifyNoOtherCalls();
        }

    }
}
