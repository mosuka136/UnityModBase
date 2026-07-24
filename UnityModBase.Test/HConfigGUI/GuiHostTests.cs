using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
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
            using var user = new UserContext("user", new Translator("用户", "User"));

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
            using var user = new UserContext("user", new Translator("用户", "User"));
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
            UserManager.CreateUser(selectedUserId, new Translator("已选择", "Selected"));
            UserManager.CreateUser(remainingUserId, new Translator("保留", "Remaining"));
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

        [Fact]
        public void OnCurrentContextChanging_CommitsPendingEditAndClosesPopup()
        {
            var sut = new TestGuiHost();
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var userEditor = new UserEditor(
                unityService.Object,
                unityGui.Object,
                new StyleResource(null));
            sut.Configure(unityService.Object, userEditor);
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
            var hotkey = new Hotkey(UnityProvider.Instance);
            var hotkeyEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            hotkeyEntry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            hotkeyEntry.SetupGet(x => x.Value).Returns(hotkey);
            userEditor.GroupEditor.HotkeyEditor.Session.BeginEdit(hotkeyEntry.Object);

            sut.ChangeContextForTest(currentContext, nextContext);

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
            remainingUser.AddChildContext(TestGuiHost.ContextKey, GuiContext.InvalidGuiContext);
            var selectedContext = new TrackingContext();
            var sut = new TestGuiHost();
            sut.ConfigureSelection(selectedUserId, selectedContext);
            var removalHandler = CreateUserRemovalHandler(sut);
            UserManager.OnUserRemoved += removalHandler;

            try
            {
                UserManager.RemoveUser(selectedUserId);

                Assert.Same(remainingUser, UserManager.GetUser(remainingUserId));
                Assert.Same(
                    GuiContext.InvalidGuiContext,
                    remainingUser.GetChildContext(TestGuiHost.ContextKey));
                Assert.Equal(string.Empty, sut.SelectedUserKey);
                Assert.Null(sut.CurrentContext);
            }
            finally
            {
                UserManager.OnUserRemoved -= removalHandler;
                remainingUser.RemoveChildContext(TestGuiHost.ContextKey);
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

            public void ChangeContextForTest(IUserContext currentContext, IUserContext nextContext)
            {
                OnCurrentContextChanging(currentContext, nextContext);
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
