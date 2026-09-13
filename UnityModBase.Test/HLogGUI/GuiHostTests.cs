using System.Reflection;
using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HLogGUI
{
    public class GuiHostTests
    {
        [Fact]
        public void Hide_WhenAlreadyHidden_LeavesWindowHidden()
        {
            // Arrange
            var sut = new GuiHost();

            // Act
            sut.Hide();

            // Assert
            Assert.False(sut.IsVisible);
            Assert.False(sut.HasDraggedWindowSinceOpen);
        }

        [Fact]
        public void Hide_WhenVisible_ClearsVisibilityAndDraggedState()
        {
            // Arrange
            var sut = new GuiHost();
            SetUnityService(sut);
            sut.ToggleVisibility();
            SetHasDraggedWindowSinceOpen(sut, true);

            // Act
            sut.Hide();

            // Assert
            Assert.False(sut.IsVisible);
            Assert.False(sut.HasDraggedWindowSinceOpen);
        }

        [Fact]
        public void ToggleVisibility_WhenHidden_ShowsWindow()
        {
            // Arrange
            var sut = new GuiHost();
            SetUnityService(sut);

            // Act
            sut.ToggleVisibility();

            // Assert
            Assert.True(sut.IsVisible);
        }

        [Fact]
        public void ToggleVisibility_WhenVisible_HidesWindowAndClearsDraggedState()
        {
            // Arrange
            var sut = new GuiHost();
            SetUnityService(sut);
            sut.ToggleVisibility();
            SetHasDraggedWindowSinceOpen(sut, true);

            // Act
            sut.ToggleVisibility();

            // Assert
            Assert.False(sut.IsVisible);
            Assert.False(sut.HasDraggedWindowSinceOpen);
        }

        [Fact]
        public void TryApplyMeasuredColumnWidth_WhenCurrentContextIsMissing_LeavesWindowBoundsUnchanged()
        {
            // OnGUI 本体现在引用 Event.type（引擎内部调用），在无 Unity 运行时的测试进程中无法被调用；
            // “上下文缺失时跳过列宽自适应”的行为改为在 TryApplyMeasuredColumnWidth 层验证。
            var sut = new GuiHost();
            var originalBounds = new Rect(10f, 20f, 300f, 400f);
            SetProperty(sut, nameof(GuiHost.WindowRect), originalBounds);

            InvokeNonPublic(sut, "TryApplyMeasuredColumnWidth");

            Assert.Null(sut.CurrentContext);
            Assert.Equal(originalBounds, sut.WindowRect);
            Assert.False(sut.IsVisible);
        }

        [Fact]
        public void TryApplyMeasuredColumnWidth_WhenColumnWidthUnmeasured_LeavesWindowWidthUnchanged()
        {
            var sut = new GuiHost();
            var originalBounds = new Rect(10f, 20f, 300f, 400f);
            SetProperty(sut, nameof(GuiHost.WindowRect), originalBounds);
            SetProperty(sut, nameof(GuiHost.CurrentContext), new GuiContext());

            InvokeNonPublic(sut, "TryApplyMeasuredColumnWidth");

            Assert.Equal(originalBounds, sut.WindowRect);
        }

        [Theory]
        [InlineData(1500f, 1500f)]
        [InlineData(2400f, 1728f)]
        [InlineData(240f, 960f)]
        public void TryApplyMeasuredColumnWidth_WhenColumnWidthMeasured_ClampsWidthIntoScreenRange(
            float measuredWidth,
            float expectedWidth)
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityService
                .Setup(x => x.Clamp(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()))
                .Returns((float value, float min, float max) => Math.Max(min, Math.Min(max, value)));
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.ScreenWidth).Returns(1920f);
            unityGui.SetupGet(x => x.ScreenHeight).Returns(1080f);
            var sut = new GuiHost();
            SetProperty(sut, nameof(GuiHost.UnityService), unityService.Object);
            SetProperty(sut, nameof(GuiHost.UnityGui), unityGui.Object);
            SetProperty(sut, nameof(GuiHost.WindowRect), new Rect(10f, 20f, 300f, 400f));
            SetProperty(sut, nameof(GuiHost.CurrentContext), new GuiContext { TotalColumnWidth = measuredWidth });

            InvokeNonPublic(sut, "TryApplyMeasuredColumnWidth");

            // 只改宽度：位置与高度保持不变。
            Assert.Equal(expectedWidth, sut.WindowRect.width);
            Assert.Equal(10f, sut.WindowRect.x);
            Assert.Equal(20f, sut.WindowRect.y);
            Assert.Equal(400f, sut.WindowRect.height);
            unityService.Verify(x => x.Clamp(measuredWidth, 960f, 1728f), Times.Once);
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyGet(x => x.ScreenWidth, Times.Exactly(2));
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void OnSelectedUserRemoved_WhenDefaultUserHasWrongContextType_ClearsSelection()
        {
            var selectedUserId = $"log-host-invalid-selected-{Guid.NewGuid():N}";
            var remainingUserId = $"log-host-invalid-remaining-{Guid.NewGuid():N}";
            UserManager.CreateUser(selectedUserId, new Translator("已选择", "Selected"));
            var remainingUser = UserManager.CreateUser(
                remainingUserId,
                new Translator("错误类型", "Wrong Type"));
            var wrongContext = new TrackingContext();
            remainingUser.AddChildContext(nameof(HLogGUI), wrongContext);
            var selectedContext = new TrackingContext();
            var sut = new GuiHost();
            ConfigureSelection(sut, selectedUserId, selectedContext);
            var removalHandler = CreateUserRemovalHandler(sut);
            UserManager.OnUserRemoved += removalHandler;

            try
            {
                UserManager.RemoveUser(selectedUserId);

                Assert.Same(remainingUser, UserManager.GetUser(remainingUserId));
                Assert.Same(wrongContext, remainingUser.GetChildContext(nameof(HLogGUI)));
                Assert.Equal(string.Empty, sut.SelectedUserKey);
                Assert.Null(sut.CurrentContext);
            }
            finally
            {
                UserManager.OnUserRemoved -= removalHandler;
                UserManager.RemoveUser(selectedUserId);
                UserManager.RemoveUser(remainingUserId);
            }
        }

        [Fact]
        public void OnDestroy_WhenBaseRemovalSubscriptionExists_UnsubscribesIt()
        {
            // Arrange
            var selectedUserId = $"log-host-selected-{Guid.NewGuid():N}";
            var remainingUserId = $"log-host-remaining-{Guid.NewGuid():N}";
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

        private static Action<string> CreateUserRemovalHandler(GuiHostBase target)
        {
            var method = typeof(GuiHostBase).GetMethod(
                "OnUserRemoved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), target, method);
        }

        private static void SetHasDraggedWindowSinceOpen(GuiHost sut, bool value)
        {
            var property = typeof(GuiHost).GetProperty(nameof(GuiHost.HasDraggedWindowSinceOpen));
            Assert.NotNull(property);
            property.SetValue(sut, value);
        }

        // ToggleVisibility 显示分支会读取 UnityService.FrameCount 记录打开帧，
        // 调用显隐切换的测试必须先注入带帧号的运行时服务替身。
        private static void SetUnityService(GuiHost host, int frameCount = 1)
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityService.SetupGet(x => x.FrameCount).Returns(frameCount);
            SetProperty(host, nameof(GuiHost.UnityService), unityService.Object);
        }

        private static void ConfigureSelection(GuiHost host, string userId, IUserContext context)
        {
            SetProperty(host, "GuiContextKey", nameof(HLogGUI));
            SetField(host, "_selectedUserKey", userId);
            SetProperty(host, "CurrentContext", context);
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

        private static void InvokeNonPublic(GuiHost host, string methodName)
        {
            var method = typeof(GuiHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(host, null);
        }

        private sealed class TrackingContext : IUserContext
        {
            public void Dispose()
            {
            }
        }
    }
}
