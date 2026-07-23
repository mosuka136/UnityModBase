using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI;
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
            sut.OnGUI();

            // Assert
            Assert.Null(sut.CurrentContext);
            Assert.Equal(originalBounds, sut.WindowRect);
            Assert.False(sut.IsVisible);
        }

        [Fact]
        public void OnDestroy_WhenBaseRemovalSubscriptionExists_UnsubscribesIt()
        {
            // Arrange
            var selectedUserId = $"log-host-selected-{Guid.NewGuid():N}";
            var remainingUserId = $"log-host-remaining-{Guid.NewGuid():N}";
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

        private static void SetHasDraggedWindowSinceOpen(GuiHost sut, bool value)
        {
            var property = typeof(GuiHost).GetProperty(nameof(GuiHost.HasDraggedWindowSinceOpen));
            Assert.NotNull(property);
            property.SetValue(sut, value);
        }

        private sealed class TestGuiHost : GuiHost
        {
            public void ConfigureSelection(string userId, IUserContext context)
            {
                GuiContextKey = nameof(HLogGUI);
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
