using System.Reflection;
using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HConfigGUI
{
    public class GuiHostTests
    {
        private const string ContextKey = "HConfigGUI";

        [Fact]
        public void RegisterContext_WhenUserIsValid_AttachesProjectedContext()
        {
            // Arrange
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGui.Object);
            var sut = new GuiHost();
            Configure(
                sut,
                unityService.Object,
                new ToastEditor(unityService.Object, unityGui.Object, styleProvider));
            using var user = new UserContext("user", new Translator("用户", "User"));

            // Act
            sut.RegisterContext(user);

            // Assert
            var context = Assert.IsType<GuiContext>(user.GetChildContext(ContextKey));
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
            var sut = new GuiHost();
            Configure(
                sut,
                unityService.Object,
                new ToastEditor(unityService.Object, unityGui.Object, styleProvider));
            using var user = new UserContext("user", new Translator("用户", "User"));
            using var existing = new TrackingContext();
            user.AddChildContext(ContextKey, existing);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => sut.RegisterContext(user));

            // Assert
            Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
            Assert.Same(existing, user.GetChildContext(ContextKey));
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
            var sut = new GuiHost();
            Configure(sut, unityService.Object, userEditor);

            // Act
            InvokeNonPublic(sut, "Update");

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
            UserManager.CreateUser(selectedUserId, new Translator("已选择", "Selected"));
            UserManager.CreateUser(remainingUserId, new Translator("保留", "Remaining"));
            var selectedContext = new TrackingContext();
            var sut = new GuiHost();
            ConfigureSelection(sut, selectedUserId, selectedContext);
            var removalHandler = CreateUserRemovalHandler(sut);
            UserManager.OnUserRemoved += removalHandler;

            try
            {
                // Act
                InvokeNonPublic(sut, "OnDestroy");
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

        [Fact]
        public void OnCurrentContextChanging_CommitsPendingEditAndClosesPopup()
        {
            var sut = new GuiHost();
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var userEditor = new UserEditor(
                unityService.Object,
                unityGui.Object,
                new StyleResource(null));
            Configure(sut, unityService.Object, userEditor);
            var currentContext = new GuiContext();
            var nextContext = new GuiContext();
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            var storedValue = "old";
            var closeCallCount = 0;
            entry.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entry.SetupGet(x => x.Value).Returns(() => storedValue);
            entry.SetupSet(x => x.Value = "new").Callback<object>(value => storedValue = (string)value);
            currentContext.ChangeSink.SetValue(entry.Object, "new", delay: 10f);
            currentContext.Popup.IsOpen = true;
            currentContext.Popup.Title = new Translator("标题", "Title");
            currentContext.Popup.DrawAction = () => { };
            currentContext.Popup.CloseAction = () => closeCallCount++;
            var hotkey = new Hotkey();
            var hotkeyEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            hotkeyEntry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            hotkeyEntry.SetupGet(x => x.Value).Returns(hotkey);
            userEditor.GroupEditor.HotkeyEditor.Session.BeginEdit(hotkeyEntry.Object);

            InvokeNonPublic(sut, "OnCurrentContextChanging", currentContext, nextContext);

            Assert.Equal("new", storedValue);
            Assert.False(editBuffer.IsUsing);
            Assert.Equal(1, closeCallCount);
            Assert.False(currentContext.Popup.IsOpen);
            Assert.Null(currentContext.Popup.Title);
            Assert.Null(currentContext.Popup.DrawAction);
            Assert.Null(currentContext.Popup.CloseAction);
            Assert.Null(userEditor.GroupEditor.HotkeyEditor.Session.Entry);
            Assert.Equal(HotkeyEditState.Idle, userEditor.GroupEditor.HotkeyEditor.Session.State);
            entry.VerifySet(x => x.Value = "new", Times.Once);
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void OnSelectedUserRemoved_WhenDefaultUserHasInvalidContext_ClearsSelection()
        {
            var selectedUserId = $"config-host-invalid-selected-{Guid.NewGuid():N}";
            var remainingUserId = $"config-host-invalid-remaining-{Guid.NewGuid():N}";
            UserManager.CreateUser(selectedUserId, new Translator("已选择", "Selected"));
            var remainingUser = UserManager.CreateUser(
                remainingUserId,
                new Translator("无效", "Invalid"));
            var invalidContext = new GuiContext(false);
            remainingUser.AddChildContext(ContextKey, invalidContext);
            var selectedContext = new TrackingContext();
            var sut = new GuiHost();
            ConfigureSelection(sut, selectedUserId, selectedContext);
            var removalHandler = CreateUserRemovalHandler(sut);
            UserManager.OnUserRemoved += removalHandler;

            try
            {
                UserManager.RemoveUser(selectedUserId);

                Assert.Same(remainingUser, UserManager.GetUser(remainingUserId));
                Assert.Same(
                    invalidContext,
                    remainingUser.GetChildContext(ContextKey));
                Assert.Equal(string.Empty, sut.SelectedUserKey);
                Assert.Null(sut.CurrentContext);
            }
            finally
            {
                UserManager.OnUserRemoved -= removalHandler;
                remainingUser.RemoveChildContext(ContextKey);
                UserManager.RemoveUser(selectedUserId);
                UserManager.RemoveUser(remainingUserId);
            }
        }

        private static Action<string> CreateUserRemovalHandler(GuiHostBase target)
        {
            var method = typeof(GuiHostBase).GetMethod(
                "OnUserRemoved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), target, method);
        }

        private static void Configure(GuiHost host, IUnityProvider unityService, ToastEditor toastEditor)
        {
            SetProperty(host, nameof(GuiHost.UnityService), unityService);
            SetProperty(host, nameof(GuiHost.ToastEditor), toastEditor);
            SetProperty(host, nameof(GuiHost.GuiContextKey), ContextKey);
        }

        private static void Configure(GuiHost host, IUnityProvider unityService, UserEditor userEditor)
        {
            SetProperty(host, nameof(GuiHost.UnityService), unityService);
            SetProperty(host, nameof(GuiHost.UserEditor), userEditor);
            SetProperty(host, nameof(GuiHost.GuiContextKey), ContextKey);
        }

        private static void ConfigureSelection(GuiHost host, string userId, IUserContext context)
        {
            SetProperty(host, nameof(GuiHost.GuiContextKey), ContextKey);
            SetField(host, "_selectedUserKey", userId);
            SetProperty(host, nameof(GuiHost.CurrentContext), context);
        }

        private static void SetProperty(GuiHost host, string propertyName, object value)
        {
            var property = typeof(GuiHostBase).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            property.SetValue(host, value);
        }

        private static void SetField(GuiHost host, string fieldName, object value)
        {
            var field = typeof(GuiHostBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(host, value);
        }

        private static void InvokeNonPublic(GuiHost host, string methodName, params object[] arguments)
        {
            var method = typeof(GuiHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(host, arguments);
        }

        private sealed class TrackingContext : IUserContext
        {
            public void Dispose()
            {
            }
        }
    }
}
