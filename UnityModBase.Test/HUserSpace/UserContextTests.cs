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
        public void AddContext_WhenKeyIsInvalid_ThrowsArgumentException(string key)
        {
            // Arrange
            using var context = new UserContext("user", "User");
            using var child = new TrackingContext();

            // Act
            var exception = Assert.Throws<ArgumentException>(() => context.AddChildContext(key, child));

            // Assert
            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void AddContext_WhenContextIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            using var context = new UserContext("user", "User");

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => context.AddChildContext("child", null));

            // Assert
            Assert.Equal("context", exception.ParamName);
            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("child"));
        }

        [Fact]
        public void AddContext_WhenKeyAlreadyExists_PreservesOriginalContext()
        {
            // Arrange
            using var context = new UserContext("user", "User");
            using var original = new TrackingContext();
            using var replacement = new TrackingContext();

            // Act
            var firstResult = context.AddChildContext("child", original);
            var secondResult = context.AddChildContext("child", replacement);

            // Assert
            Assert.True(firstResult);
            Assert.False(secondResult);
            Assert.Same(original, context.GetChildContext("child"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetContext_WhenKeyIsInvalid_ThrowsArgumentException(string key)
        {
            // Arrange
            using var context = new UserContext("user", "User");

            // Act
            var exception = Assert.Throws<ArgumentException>(() => context.GetChildContext(key));

            // Assert
            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void GetContext_WhenKeyIsMissing_ReturnsInvalidContext()
        {
            // Arrange
            using var context = new UserContext("user", "User");

            // Act
            var result = context.GetChildContext("missing");

            // Assert
            Assert.Same(UserContext.InvalidUserContext, result);
            Assert.False(((UserContext)result).IsValid);
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
            context.AddChildContext("first", first);
            context.AddChildContext("second", second);

            // Act
            context.Dispose();

            // Assert
            Assert.True(first.IsDisposed);
            Assert.True(second.IsDisposed);
            Assert.Null(context.Service.LogDatabase);
            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("first"));
            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("second"));
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
