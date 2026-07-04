using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HUserSpace
{
    public class UserContextTests
    {
        [Fact]
        public void Constructor_WhenNameIsNull_UsesEmptyName()
        {
            // Act
            using var context = new UserContext("user", null);

            // Assert
            Assert.Equal("user", context.UserId);
            Assert.Equal(string.Empty, context.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddContext_WhenKeyIsInvalid_ReturnsFalse(string key)
        {
            // Arrange
            using var context = new UserContext("user", "User");
            using var child = new TrackingContext();

            // Act
            var result = context.AddContext(key, child);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddContext_WhenContextIsNull_ReturnsFalse()
        {
            // Arrange
            using var context = new UserContext("user", "User");

            // Act
            var result = context.AddContext("child", null);

            // Assert
            Assert.False(result);
            Assert.Null(context.GetContext("child"));
        }

        [Fact]
        public void AddContext_WhenKeyAlreadyExists_PreservesOriginalContext()
        {
            // Arrange
            using var context = new UserContext("user", "User");
            using var original = new TrackingContext();
            using var replacement = new TrackingContext();

            // Act
            var firstResult = context.AddContext("child", original);
            var secondResult = context.AddContext("child", replacement);

            // Assert
            Assert.True(firstResult);
            Assert.False(secondResult);
            Assert.Same(original, context.GetContext("child"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("missing")]
        public void GetContext_WhenKeyIsInvalidOrMissing_ReturnsNull(string key)
        {
            // Arrange
            using var context = new UserContext("user", "User");

            // Act
            var result = context.GetContext(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Dispose_WhenServiceAndChildrenExist_DisposesAndClearsAllResources()
        {
            // Arrange
            var context = new UserContext("user", "User")
            {
                Service = new UserService("user")
            };
            var first = new TrackingContext();
            var second = new TrackingContext();
            context.AddContext("first", first);
            context.AddContext("second", second);

            // Act
            context.Dispose();

            // Assert
            Assert.True(first.IsDisposed);
            Assert.True(second.IsDisposed);
            Assert.Null(context.Service.LogDatabase);
            Assert.Null(context.GetContext("first"));
            Assert.Null(context.GetContext("second"));
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
