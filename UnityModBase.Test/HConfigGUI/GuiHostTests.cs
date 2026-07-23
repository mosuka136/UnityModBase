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

        [Fact]
        public void OnDestroy_WhenBaseRemovalSubscriptionExists_UnsubscribesIt()
        {
            // Arrange
            var selectedUserId = $"config-host-selected-{Guid.NewGuid():N}";
            var remainingUserId = $"config-host-remaining-{Guid.NewGuid():N}";
            UserManager.CreateUser(selectedUserId, "Selected");
            UserManager.CreateUser(remainingUserId, "Remaining");
            var selectedContext = new TrackingContext();
            var sut = new TestGuiHost();
            sut.ConfigureSelection(selectedUserId, selectedContext);
            var removalHandler = CreateUserRemovalHandler(sut);
            UserManager.OnUserRemoved += removalHandler;

            try
            {
                // Act
                sut.DestroyForTest();
                UserManager.RemoveUser(selectedUserId);

                // Assert
                Assert.Equal(selectedUserId, sut.SelectedUserKey);
                Assert.Same(selectedContext, sut.CurrentContext);
            }
            finally
            {
                UserManager.OnUserRemoved -= removalHandler;
                UserManager.RemoveUser(selectedUserId);
                UserManager.RemoveUser(remainingUserId);
            }
        }

        private static Action<string> CreateUserRemovalHandler(GuiHostBase target)
        {
            var method = typeof(GuiHostBase).GetMethod(
                "OnUserRemoved",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), target, method);
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

            public void ConfigureSelection(string userId, IUserContext context)
            {
                GuiContextKey = ContextKey;
                _selectedUserKey = userId;
                CurrentContext = context;
            }

            public void DestroyForTest()
            {
                OnDestroy();
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
