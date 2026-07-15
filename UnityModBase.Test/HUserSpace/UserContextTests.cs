using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HUserSpace
{
    public class UserContextTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WhenUserIdIsNullOrWhitespace_ThrowsArgumentException(string userId)
        {
            var exception = Assert.Throws<ArgumentException>(() => new UserContext(userId, "User"));

            Assert.Equal("userId", exception.ParamName);
        }

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
        public void AddContext_WhenAddingSelf_ThrowsArgumentException()
        {
            using var context = new UserContext("user", "User");

            var exception = Assert.Throws<ArgumentException>(() =>
                context.AddChildContext("self", context));

            Assert.Equal("context", exception.ParamName);
            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("self"));
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RemoveContext_WhenKeyIsNullOrWhitespace_ThrowsArgumentException(string key)
        {
            using var context = new UserContext("user", "User");

            var exception = Assert.Throws<ArgumentException>(() => context.RemoveChildContext(key));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void RemoveContext_WhenKeyExists_RemovesOnlySelectedContext()
        {
            using var context = new UserContext("user", "User");
            using var removed = new TrackingContext();
            using var retained = new TrackingContext();
            context.AddChildContext("removed", removed);
            context.AddChildContext("retained", retained);

            context.RemoveChildContext("removed");

            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("removed"));
            Assert.Same(retained, context.GetChildContext("retained"));
            Assert.False(removed.IsDisposed);
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

        [Fact]
        public void Dispose_WhenChildThrows_ContinuesDisposingRemainingChildren()
        {
            var context = new UserContext("user", "User");
            var remaining = new TrackingContext();
            context.AddChildContext("throwing", new ThrowingContext());
            context.AddChildContext("remaining", remaining);

            var exception = Record.Exception(context.Dispose);

            Assert.Null(exception);
            Assert.True(remaining.IsDisposed);
            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("throwing"));
            Assert.Same(UserContext.InvalidUserContext, context.GetChildContext("remaining"));
        }

        private sealed class TrackingContext : IUserContext
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class ThrowingContext : IUserContext
        {
            public void Dispose()
            {
                throw new InvalidOperationException("dispose failure");
            }
        }
    }
}
