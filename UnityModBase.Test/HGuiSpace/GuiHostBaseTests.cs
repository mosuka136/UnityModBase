using UnityModBase.HGuiSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HGuiSpace
{
    public class GuiHostBaseTests : IDisposable
    {
        private readonly List<string> _createdUserIds = new List<string>();

        public void Dispose()
        {
            foreach (var userId in _createdUserIds)
                UserManager.RemoveUser(userId);
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

        private UserContext CreateUser()
        {
            var userId = $"gui-host-{Guid.NewGuid():N}";
            _createdUserIds.Add(userId);
            return UserManager.CreateUser(userId, "GUI Host User");
        }

        private sealed class TestGuiHost : GuiHostBase
        {
            public TestGuiHost(string guiContextKey)
            {
                GuiContextKey = guiContextKey;
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
