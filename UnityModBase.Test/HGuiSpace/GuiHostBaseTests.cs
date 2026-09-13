using System.Reflection;
using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
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
            sut.AwakeForTest();
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
            sut.AwakeForTest();
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
            sut.AwakeForTest();
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
        public void Awake_WhenLastSelectedUserIsRemoved_ClearsSelectionAndContext()
        {
            var selectedUser = CreateUser();
            var selectedContext = new TrackingContext();
            selectedUser.AddChildContext("module", selectedContext);
            var sut = new TestGuiHost("module");
            sut.AwakeForTest();
            sut.Select(selectedUser.UserId, selectedContext);

            UserManager.RemoveUser(selectedUser.UserId);

            Assert.True(selectedContext.IsDisposed);
            Assert.Equal(string.Empty, sut.SelectedUserKey);
            Assert.Null(sut.CurrentContext);
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
            sut.AwakeForTest();
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

        [Fact]
        public void ShouldDrawVisibleWindow_WhenHidden_ReturnsFalseEvenOnLayout()
        {
            var sut = new TestGuiHost("module");

            Assert.False(sut.ShouldDrawVisibleWindowForTest(EventType.Layout));
        }

        [Fact]
        public void ShouldDrawVisibleWindow_AfterShow_WaitsForLayoutThenAllowsLaterEvents()
        {
            var sut = CreateHostWithFrameProvider();
            sut.ToggleVisibility();

            Assert.False(sut.ShouldDrawVisibleWindowForTest(null));
            Assert.False(sut.ShouldDrawVisibleWindowForTest(EventType.KeyDown));
            Assert.False(sut.ShouldDrawVisibleWindowForTest(EventType.Repaint));
            Assert.True(sut.ShouldDrawVisibleWindowForTest(EventType.Layout));
            Assert.True(sut.ShouldDrawVisibleWindowForTest(EventType.Repaint));
            Assert.True(sut.ShouldDrawVisibleWindowForTest(EventType.KeyDown));
        }

        [Fact]
        public void ShouldDrawVisibleWindow_AfterHideAndShowAgain_WaitsForLayout()
        {
            var sut = CreateHostWithFrameProvider();
            sut.ToggleVisibility();
            Assert.True(sut.ShouldDrawVisibleWindowForTest(EventType.Layout));

            sut.Hide();
            sut.ToggleVisibility();

            Assert.False(sut.ShouldDrawVisibleWindowForTest(EventType.Repaint));
            Assert.True(sut.ShouldDrawVisibleWindowForTest(EventType.Layout));
        }

        [Fact]
        public void ApplyReturnedWindowRect_WhenFirstLayoutAfterShowReturnsJitteredPosition_KeepsRequestedRect()
        {
            var sut = CreateHostWithFrameProvider();
            var requested = new Rect(100f, 200f, 300f, 400f);
            sut.SetWindowRect(requested);
            sut.ToggleVisibility();
            sut.ShouldDrawVisibleWindowForTest(EventType.Layout);

            // 首次 Layout 当趟的返回矩形被跳过一次，之后的抖动不再出现于真实流程。
            sut.ApplyReturnedWindowRectForTest(requested, new Rect(0f, 0f, 300f, 400f));

            Assert.Equal(requested, sut.WindowRect);
            Assert.False(sut.HasDraggedWindowSinceOpen);
        }

        [Fact]
        public void ApplyReturnedWindowRect_WhenWindowMoves_UpdatesRectAndMarksDragged()
        {
            var sut = new TestGuiHost("module");
            var requested = new Rect(100f, 200f, 300f, 400f);
            sut.SetWindowRect(requested);
            var dragged = new Rect(110f, 210f, 300f, 400f);

            sut.ApplyReturnedWindowRectForTest(requested, dragged);

            Assert.Equal(dragged, sut.WindowRect);
            Assert.True(sut.HasDraggedWindowSinceOpen);
        }

        [Fact]
        public void TryAutoHideOnFocusLost_OnShownFrame_DoesNotHide()
        {
            var unityService = CreateFrameProvider(12);
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(unityService.Object);
            sut.SetWindowRect(new Rect(100f, 100f, 200f, 200f));
            sut.ToggleVisibility();

            var hidden = sut.TryAutoHideOnFocusLostForTest(EventType.MouseDown, Vector2.zero);

            Assert.False(hidden);
            Assert.True(sut.IsVisible);
            // 显隐切换记录一次帧号，自动隐藏判定再读一次；打开当帧两次读数相同。
            unityService.VerifyGet(x => x.FrameCount, Times.Exactly(2));
        }

        [Fact]
        public void TryAutoHideOnFocusLost_OnLaterFrameOutsideWindow_Hides()
        {
            var unityService = CreateFrameProvider(12);
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(unityService.Object);
            sut.SetWindowRect(new Rect(100f, 100f, 200f, 200f));
            sut.ToggleVisibility();
            unityService.SetupGet(x => x.FrameCount).Returns(13);

            var hidden = sut.TryAutoHideOnFocusLostForTest(EventType.MouseDown, Vector2.zero);

            Assert.True(hidden);
            Assert.False(sut.IsVisible);
        }

        [Fact]
        public void TryAutoHideOnFocusLost_WhenCurrentEventIsNull_DoesNotHide()
        {
            var unityService = CreateFrameProvider(12);
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(unityService.Object);
            sut.SetWindowRect(new Rect(100f, 100f, 200f, 200f));
            sut.ToggleVisibility();
            unityService.SetupGet(x => x.FrameCount).Returns(13);

            var hidden = sut.TryAutoHideOnFocusLostForTest(null, Vector2.zero);

            Assert.False(hidden);
            Assert.True(sut.IsVisible);
        }

        [Fact]
        public void TryAutoHideOnFocusLost_WhenNonMouseDownEventOutsideWindow_DoesNotHide()
        {
            var unityService = CreateFrameProvider(12);
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(unityService.Object);
            sut.SetWindowRect(new Rect(100f, 100f, 200f, 200f));
            sut.ToggleVisibility();
            unityService.SetupGet(x => x.FrameCount).Returns(13);

            var hidden = sut.TryAutoHideOnFocusLostForTest(EventType.Repaint, Vector2.zero);

            Assert.False(hidden);
            Assert.True(sut.IsVisible);
        }

        [Fact]
        public void TryAutoHideOnFocusLost_WhenMouseDownInsideWindow_DoesNotHide()
        {
            var unityService = CreateFrameProvider(12);
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(unityService.Object);
            sut.SetWindowRect(new Rect(100f, 100f, 200f, 200f));
            sut.ToggleVisibility();
            unityService.SetupGet(x => x.FrameCount).Returns(13);

            var hidden = sut.TryAutoHideOnFocusLostForTest(EventType.MouseDown, new Vector2(150f, 150f));

            Assert.False(hidden);
            Assert.True(sut.IsVisible);
        }

        [Fact]
        public void TryAutoHideOnFocusLost_AfterWindowDrag_DoesNotHideEvenOnLaterFrame()
        {
            var unityService = CreateFrameProvider(12);
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(unityService.Object);
            sut.SetWindowRect(new Rect(100f, 100f, 200f, 200f));
            sut.ToggleVisibility();
            unityService.SetupGet(x => x.FrameCount).Returns(13);
            sut.SetDraggedForTest(true);

            var hidden = sut.TryAutoHideOnFocusLostForTest(EventType.MouseDown, Vector2.zero);

            Assert.False(hidden);
            Assert.True(sut.IsVisible);
        }

        [Fact]
        public void ApplyReturnedWindowRect_WhenPendingResizeExists_PrefersResizeResultOverReturnedRect()
        {
            var sut = new TestGuiHost("module");
            var requested = new Rect(100f, 200f, 300f, 400f);
            var pending = new Rect(100f, 200f, 500f, 600f);
            sut.SetWindowRect(requested);
            SetPendingWindowResizeRect(sut, pending);

            // GUI.Window 返回的仍是拉伸前的请求矩形；写回应采用窗口回调内算出的拉伸结果。
            sut.ApplyReturnedWindowRectForTest(requested, requested);

            Assert.Equal(pending, sut.WindowRect);

            // 暂存结果已被消费：再次调用按普通返回矩形处理，位置相同则保持不变。
            sut.ApplyReturnedWindowRectForTest(pending, pending);
            Assert.Equal(pending, sut.WindowRect);
        }

        private UserContext CreateUser()
        {
            var userId = $"gui-host-{Guid.NewGuid():N}";
            return UserManager.CreateUser(userId, new Translator("GUI Host User", "GUI Host User"));
        }

        // ToggleVisibility 显示分支会读取 UnityService.FrameCount 记录打开帧，
        // 调用显隐切换的测试必须先注入带帧号的运行时服务替身。
        private static Mock<IUnityProvider> CreateFrameProvider(int frameCount)
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityService.SetupGet(x => x.FrameCount).Returns(frameCount);
            return unityService;
        }

        private static TestGuiHost CreateHostWithFrameProvider()
        {
            var sut = new TestGuiHost("module");
            sut.SetUnityServiceForTest(CreateFrameProvider(1).Object);
            return sut;
        }

        private static void SetPendingWindowResizeRect(GuiHostBase host, Rect? rect)
        {
            var field = typeof(GuiHostBase).GetField(
                "_pendingWindowResizeRect",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(host, rect);
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

            public void AwakeForTest()
            {
                Awake();
            }

            public bool ShouldDrawVisibleWindowForTest(EventType? eventType)
            {
                return ShouldDrawVisibleWindow(eventType);
            }

            public void ApplyReturnedWindowRectForTest(Rect requested, Rect returned)
            {
                ApplyReturnedWindowRect(requested, returned);
            }

            public bool TryAutoHideOnFocusLostForTest(EventType? eventType, Vector2 mousePosition)
            {
                return TryAutoHideOnFocusLost(eventType, mousePosition);
            }

            public void SetWindowRect(Rect rect)
            {
                WindowRect = rect;
            }

            public void SetDraggedForTest(bool value)
            {
                HasDraggedWindowSinceOpen = value;
            }

            public void SetUnityServiceForTest(IUnityProvider unityService)
            {
                UnityService = unityService;
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
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
