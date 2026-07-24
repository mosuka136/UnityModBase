using UnityModBase.HTranslatorSpace;
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
            var exception = Assert.Throws<ArgumentException>(() => new UserContext(userId, CreateName()));

            Assert.Equal("userId", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenNameIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new UserContext("user", null));

            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenNameIsProvided_PreservesTranslatorInstance()
        {
            var name = new Translator("用户", "User");

            using var context = new UserContext("user", name);

            Assert.Equal("user", context.UserId);
            Assert.Same(name, context.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddContext_WhenKeyIsInvalid_ThrowsArgumentException(string key)
        {
            // Arrange
            using var context = new UserContext("user", CreateName());
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
            using var context = new UserContext("user", CreateName());

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => context.AddChildContext("child", null));

            // Assert
            Assert.Equal("context", exception.ParamName);
            Assert.Null(context.GetChildContext("child"));
        }

        [Fact]
        public void AddChildContext_WhenContextIsValid_StoresSameInstance()
        {
            using var context = new UserContext("user", CreateName());
            using var child = new TrackingContext();

            context.AddChildContext("child", child);

            Assert.Same(child, context.GetChildContext("child"));
        }

        [Fact]
        public void AddChildContext_WhenKeyAlreadyExists_PreservesOriginalOwnership()
        {
            // Arrange
            using var context = new UserContext("user", CreateName());
            var original = new TrackingContext();
            using var replacement = new TrackingContext();

            // Act
            context.AddChildContext("child", original);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                context.AddChildContext("child", replacement));

            // Assert
            Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
            Assert.Same(original, context.GetChildContext("child"));

            context.Dispose();

            Assert.True(original.IsDisposed);
            Assert.False(replacement.IsDisposed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetContext_WhenKeyIsInvalid_ThrowsArgumentException(string key)
        {
            // Arrange
            using var context = new UserContext("user", CreateName());

            // Act
            var exception = Assert.Throws<ArgumentException>(() => context.GetChildContext(key));

            // Assert
            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void GetContext_WhenKeyIsMissing_ReturnsNull()
        {
            // Arrange
            using var context = new UserContext("user", CreateName());

            // Act
            var result = context.GetChildContext("missing");

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RemoveContext_WhenKeyIsNullOrWhitespace_ThrowsArgumentException(string key)
        {
            using var context = new UserContext("user", CreateName());

            var exception = Assert.Throws<ArgumentException>(() => context.RemoveChildContext(key));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void RemoveContext_WhenKeyExists_RemovesOnlySelectedContext()
        {
            using var context = new UserContext("user", CreateName());
            using var removed = new TrackingContext();
            using var retained = new TrackingContext();
            context.AddChildContext("removed", removed);
            context.AddChildContext("retained", retained);

            context.RemoveChildContext("removed");

            Assert.Null(context.GetChildContext("removed"));
            Assert.Same(retained, context.GetChildContext("retained"));
            Assert.False(removed.IsDisposed);
        }

        [Fact]
        public void Dispose_WhenServiceAndChildrenExist_DisposesAndClearsAllResources()
        {
            // Arrange
            var context = new UserContext("user", CreateName())
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
            Assert.Null(context.GetChildContext("first"));
            Assert.Null(context.GetChildContext("second"));
        }

        [Fact]
        public void Dispose_WhenChildThrows_ContinuesDisposingRemainingChildren()
        {
            var context = new UserContext("user", CreateName());
            var remaining = new TrackingContext();
            context.AddChildContext("throwing", new ThrowingContext());
            context.AddChildContext("remaining", remaining);

            var exception = Record.Exception(context.Dispose);

            Assert.Null(exception);
            Assert.True(remaining.IsDisposed);
            Assert.Null(context.Service.LogDatabase);
            Assert.Null(context.GetChildContext("throwing"));
            Assert.Null(context.GetChildContext("remaining"));
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

        private static Translator CreateName()
        {
            return new Translator("用户", "User");
        }
    }
}
