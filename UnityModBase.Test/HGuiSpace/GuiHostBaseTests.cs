using System.Reflection;
using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HGuiSpace
{
    public class GuiHostBaseTests : IDisposable
    {
        // 用户移除事件没有公开读取或清空订阅者的接口；
        // 测试通过反射暂存并隔离进程级注册表和委托，结束后再原样恢复。
        private static readonly FieldInfo UserContextsField = typeof(UserManager)
            .GetField("_userContexts", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnUserRemovedField = typeof(UserManager)
            .GetField(nameof(UserManager.OnUserRemoved), BindingFlags.NonPublic | BindingFlags.Static);

        private readonly Dictionary<string, UserContext> _originalContexts;
        private readonly Action<string> _originalUserRemovedHandlers;

        public GuiHostBaseTests()
        {
            Assert.NotNull(UserContextsField);
            Assert.NotNull(OnUserRemovedField);
            var contexts = GetContexts();
            _originalContexts = contexts.ToDictionary(pair => pair.Key, pair => pair.Value);
            _originalUserRemovedHandlers = (Action<string>)OnUserRemovedField.GetValue(null);
            contexts.Clear();
            OnUserRemovedField.SetValue(null, null);
        }

        public void Dispose()
        {
            OnUserRemovedField.SetValue(null, null);
            var contexts = GetContexts();
            foreach (var context in contexts.Values)
                context.Dispose();
            contexts.Clear();
            foreach (var pair in _originalContexts)
                contexts.Add(pair.Key, pair.Value);
            OnUserRemovedField.SetValue(null, _originalUserRemovedHandlers);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetContext_WhenUserKeyIsNullOrEmpty_ThrowsArgumentNullException(string userKey)
        {
            var sut = new TestGuiHost("module");

            var exception = Assert.Throws<ArgumentNullException>(() => sut.GetContext(userKey));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void GetContext_WhenUserIsUnknown_ReturnsNull()
        {
            var sut = new TestGuiHost("module");

            var result = sut.GetContext($"missing-{Guid.NewGuid():N}");

            Assert.Null(result);
        }

        [Fact]
        public void GetContext_WhenUserHasNoModuleContext_ReturnsNull()
        {
            var user = CreateUser();
            var sut = new TestGuiHost("module");

            var result = sut.GetContext(user.UserId);

            Assert.Null(result);
        }

        [Fact]
        public void GetContext_WhenModuleContextExists_ReturnsSameInstance()
        {
            var user = CreateUser();
            var expected = new TrackingContext();
            user.AddChildContext("module", expected);
            var sut = new TestGuiHost("module");

            var result = sut.GetContext(user.UserId);

            Assert.Same(expected, result);
        }

        [Fact]
        public void Awake_WhenSelectedUserIsRemoved_SwitchesToRemainingDefaultContext()
        {
            // Arrange
            var defaultUser = CreateUser();
            var defaultContext = new TrackingContext();
            defaultUser.AddChildContext("module", defaultContext);
            var selectedUser = CreateUser();
            var selectedContext = new TrackingContext();
            selectedUser.AddChildContext("module", selectedContext);
            var sut = new TestGuiHost("module");
            sut.Awake();
            sut.Select(selectedUser.UserId, selectedContext);

            // Act
            UserManager.RemoveUser(selectedUser.UserId);

            // Assert
            Assert.Equal(defaultUser.UserId, sut.SelectedUserKey);
            Assert.Same(defaultContext, sut.CurrentContext);
            Assert.Equal(1, sut.Editor.SetStatusDirtyCallCount);
            Assert.Same(defaultContext, sut.Editor.LastContext);
        }

        [Fact]
        public void Awake_WhenDefaultUserHasNoModuleContext_ClearsSelectionWithoutTryingLaterUser()
        {
            var unavailableUser = CreateUser();
            var availableUser = CreateUser();
            var availableContext = new TrackingContext();
            availableUser.AddChildContext("module", availableContext);
            var selectedUser = CreateUser();
            var selectedContext = new TrackingContext();
            selectedUser.AddChildContext("module", selectedContext);
            var sut = new TestGuiHost("module");
            sut.Awake();
            sut.Select(selectedUser.UserId, selectedContext);

            UserManager.RemoveUser(selectedUser.UserId);

            Assert.Equal(unavailableUser.UserId, sut.GetDefaultUserKey());
            Assert.Equal(string.Empty, sut.SelectedUserKey);
            Assert.Null(sut.CurrentContext);
            Assert.NotEqual(availableUser.UserId, sut.SelectedUserKey);
            Assert.Equal(0, sut.Editor.SetStatusDirtyCallCount);
            Assert.Null(sut.Editor.LastContext);
        }

        [Fact]
        public void Awake_WhenUnselectedUserIsRemoved_PreservesSelectionAndContext()
        {
            // Arrange
            var selectedUser = CreateUser();
            var selectedContext = new TrackingContext();
            selectedUser.AddChildContext("module", selectedContext);
            var otherUser = CreateUser();
            otherUser.AddChildContext("module", new TrackingContext());
            var sut = new TestGuiHost("module");
            sut.Awake();
            sut.Select(selectedUser.UserId, selectedContext);

            // Act
            UserManager.RemoveUser(otherUser.UserId);

            // Assert
            Assert.Equal(selectedUser.UserId, sut.SelectedUserKey);
            Assert.Same(selectedContext, sut.CurrentContext);
            Assert.Equal(0, sut.Editor.SetStatusDirtyCallCount);
            Assert.Null(sut.Editor.LastContext);
        }

        [Fact]
        public void OnDestroy_WhenSelectedUserIsLaterRemoved_DoesNotChangeHostState()
        {
            // Arrange
            var selectedUser = CreateUser();
            var selectedContext = new TrackingContext();
            selectedUser.AddChildContext("module", selectedContext);
            var remainingUser = CreateUser();
            remainingUser.AddChildContext("module", new TrackingContext());
            var sut = new TestGuiHost("module");
            sut.Awake();
            sut.Select(selectedUser.UserId, selectedContext);

            // Act
            sut.DestroyForTest();
            UserManager.RemoveUser(selectedUser.UserId);

            // Assert
            Assert.Equal(selectedUser.UserId, sut.SelectedUserKey);
            Assert.Same(selectedContext, sut.CurrentContext);
            Assert.Equal(0, sut.Editor.SetStatusDirtyCallCount);
        }

        [Fact]
        public void DrawWindow_WhenUserEditorThrows_StillEndsArea()
        {
            var user = CreateUser();
            var context = new TrackingContext();
            user.AddChildContext("module", context);
            var expectedArea = new Rect(10f, 30f, -20f, -40f);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.Setup(x => x.BeginArea(expectedArea));
            unityGui.Setup(x => x.EndArea());
            var sut = new TestGuiHost("module");
            sut.ConfigureDrawing(unityGui.Object, new[] { user });
            sut.Select(user.UserId, context);
            sut.Editor.DrawException = new InvalidOperationException("draw failed");

            var exception = Assert.Throws<InvalidOperationException>(() => sut.DrawWindow(1));

            Assert.Equal("draw failed", exception.Message);
            unityGui.Verify(x => x.BeginArea(expectedArea), Times.Once);
            unityGui.Verify(x => x.EndArea(), Times.Once);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawWindow_WhenSelectionChangesBeforeEditorThrows_SwitchesContextAfterEndingArea()
        {
            var selectedUser = CreateUser();
            var selectedContext = new TrackingContext();
            selectedUser.AddChildContext("module", selectedContext);
            var nextUser = CreateUser();
            var nextContext = new TrackingContext();
            nextUser.AddChildContext("module", nextContext);
            var expectedArea = new Rect(10f, 30f, -20f, -40f);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.Setup(x => x.BeginArea(expectedArea));
            unityGui.Setup(x => x.EndArea());
            var sut = new TestGuiHost("module");
            sut.ConfigureDrawing(unityGui.Object, new[] { selectedUser, nextUser });
            sut.Select(selectedUser.UserId, selectedContext);
            sut.Editor.NextSelectedKey = nextUser.UserId;
            sut.Editor.DrawException = new InvalidOperationException("draw failed");

            var exception = Assert.Throws<InvalidOperationException>(() => sut.DrawWindow(1));

            Assert.Equal("draw failed", exception.Message);
            Assert.Equal(nextUser.UserId, sut.SelectedUserKey);
            Assert.Same(nextContext, sut.CurrentContext);
            Assert.Equal(1, sut.Editor.SetStatusDirtyCallCount);
            Assert.Same(nextContext, sut.Editor.LastContext);
            unityGui.Verify(x => x.BeginArea(expectedArea), Times.Once);
            unityGui.Verify(x => x.EndArea(), Times.Once);
            unityGui.VerifyNoOtherCalls();
        }

        private UserContext CreateUser()
        {
            var userId = $"gui-host-{Guid.NewGuid():N}";
            return UserManager.CreateUser(userId, "GUI Host User");
        }

        private static Dictionary<string, UserContext> GetContexts()
        {
            return (Dictionary<string, UserContext>)UserContextsField.GetValue(null);
        }

        private sealed class TestGuiHost : GuiHostBase
        {
            public TrackingUserEditor Editor { get; }

            public TestGuiHost(string guiContextKey)
            {
                GuiContextKey = guiContextKey;
                Editor = new TrackingUserEditor();
                UserEditor = Editor;
            }

            public void Select(string userId, IUserContext context)
            {
                _selectedUserKey = userId;
                CurrentContext = context;
            }

            public void ConfigureDrawing(IUnityGuiProvider unityGui, IEnumerable<UserContext> users)
            {
                UnityGui = unityGui;
                Users = users;
            }

            public void DestroyForTest()
            {
                OnDestroy();
            }
        }

        private sealed class TrackingUserEditor : UserEditorBase
        {
            public int SetStatusDirtyCallCount { get; private set; }
            public IUserContext LastContext { get; private set; }
            public string NextSelectedKey { get; set; }
            public Exception DrawException { get; set; }

            public TrackingUserEditor()
                : base(null, null)
            {
            }

            public override void SetStatusDirty(IUserContext context)
            {
                SetStatusDirtyCallCount++;
                LastContext = context;
            }

            public override void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
            {
                if (NextSelectedKey != null)
                    selectedKey = NextSelectedKey;
                if (DrawException != null)
                    throw DrawException;
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
