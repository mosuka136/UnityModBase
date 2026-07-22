using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HConfigGUI
{
    public class GuiHostTests
    {
        [Fact]
        public void RegisterContext_WhenUserIsValid_AttachesProjectedContext()
        {
            // Arrange
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGui.Object);
            var sut = new TestGuiHost();
            sut.Configure(
                unityService.Object,
                new ToastEditor(unityService.Object, unityGui.Object, styleProvider));
            using var user = new UserContext("user", "User");

            // Act
            sut.RegisterContext(user);

            // Assert
            var context = Assert.IsType<GuiContext>(user.GetChildContext(TestGuiHost.ContextKey));
            Assert.Equal(user.UserId, context.UserData.Key);
            Assert.Empty(context.UserData.Children);
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void RegisterContext_WhenModuleKeyAlreadyExists_ThrowsAndPreservesExistingContext()
        {
            // Arrange
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGui.Object);
            var sut = new TestGuiHost();
            sut.Configure(
                unityService.Object,
                new ToastEditor(unityService.Object, unityGui.Object, styleProvider));
            using var user = new UserContext("user", "User");
            using var existing = new TrackingContext();
            user.AddChildContext(TestGuiHost.ContextKey, existing);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => sut.RegisterContext(user));

            // Assert
            Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
            Assert.Same(existing, user.GetChildContext(TestGuiHost.ContextKey));
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Update_WhenCurrentContextIsMissing_DoesNotAdvanceConfigEditor()
        {
            // Arrange
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGui.Object);
            using var userEditor = new UserEditor(unityService.Object, unityGui.Object, styleProvider);
            var sut = new TestGuiHost();
            sut.Configure(unityService.Object, userEditor);

            // Act
            sut.Update();

            // Assert
            Assert.Null(sut.CurrentContext);
            unityService.VerifyGet(x => x.UnscaledDeltaTime, Times.Never);
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
        }

        private sealed class TestGuiHost : GuiHost
        {
            public const string ContextKey = "HConfigGUI";

            public void Configure(IUnityProvider unityService, ToastEditor toastEditor)
            {
                UnityService = unityService;
                ToastEditor = toastEditor;
                GuiContextKey = ContextKey;
            }

            public void Configure(IUnityProvider unityService, UserEditor userEditor)
            {
                UnityService = unityService;
                UserEditor = userEditor;
                GuiContextKey = ContextKey;
            }
        }

        private sealed class TrackingContext : IUserContext
        {
            public void Dispose()
            {
            }
        }
    }
}
