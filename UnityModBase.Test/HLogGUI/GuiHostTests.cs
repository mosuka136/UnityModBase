using System.Reflection;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI;
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
            sut.ToggleVisibility();
            SetHasDraggedWindowSinceOpen(sut, true);

            // Act
            sut.ToggleVisibility();

            // Assert
            Assert.False(sut.IsVisible);
            Assert.False(sut.HasDraggedWindowSinceOpen);
        }

        [Fact]
        public void OnGUI_WhenCurrentContextIsMissing_LeavesWindowBoundsUnchanged()
        {
            // Arrange
            var sut = new GuiHost();
            var originalBounds = sut.WindowRect;

            // Act
            InvokeNonPublic(sut, "OnGUI");

            // Assert
            Assert.Null(sut.CurrentContext);
            Assert.Equal(originalBounds, sut.WindowRect);
            Assert.False(sut.IsVisible);
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
