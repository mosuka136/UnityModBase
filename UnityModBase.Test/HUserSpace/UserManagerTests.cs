using System.Reflection;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HUserSpace
{
    public class UserManagerTests : IDisposable
    {
        private static readonly FieldInfo UserContextsField = typeof(UserManager)
            .GetField("_userContexts", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnUserRegisteredField = typeof(UserManager)
            .GetField(nameof(UserManager.OnUserRegistered), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnLogWriterRegisteredField = typeof(UserManager)
            .GetField(nameof(UserManager.OnLogWriterRegistered), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnConfigRegisteredField = typeof(UserManager)
            .GetField(nameof(UserManager.OnConfigRegistered), BindingFlags.NonPublic | BindingFlags.Static);

        private readonly Dictionary<string, UserContext> _originalContexts;
        private readonly Action<UserContext> _originalUserRegisteredHandlers;
        private readonly Action<UserContext> _originalLogWriterRegisteredHandlers;
        private readonly Action<UserContext> _originalConfigRegisteredHandlers;
        private readonly List<string> _tempDirectories = new List<string>();

        public UserManagerTests()
        {
            Assert.NotNull(UserContextsField);
            Assert.NotNull(OnUserRegisteredField);
            Assert.NotNull(OnLogWriterRegisteredField);
            Assert.NotNull(OnConfigRegisteredField);

            var contexts = GetContexts();
            _originalContexts = contexts.ToDictionary(pair => pair.Key, pair => pair.Value);
            _originalUserRegisteredHandlers = (Action<UserContext>)OnUserRegisteredField.GetValue(null);
            _originalLogWriterRegisteredHandlers = (Action<UserContext>)OnLogWriterRegisteredField.GetValue(null);
            _originalConfigRegisteredHandlers = (Action<UserContext>)OnConfigRegisteredField.GetValue(null);
            contexts.Clear();
            OnUserRegisteredField.SetValue(null, null);
            OnLogWriterRegisteredField.SetValue(null, null);
            OnConfigRegisteredField.SetValue(null, null);
        }

        public void Dispose()
        {
            var contexts = GetContexts();
            foreach (var context in contexts.Values)
            {
                if (!_originalContexts.Values.Contains(context))
                    context.Dispose();
            }

            contexts.Clear();
            foreach (var pair in _originalContexts)
                contexts.Add(pair.Key, pair.Value);
            OnUserRegisteredField.SetValue(null, _originalUserRegisteredHandlers);
            OnLogWriterRegisteredField.SetValue(null, _originalLogWriterRegisteredHandlers);
            OnConfigRegisteredField.SetValue(null, _originalConfigRegisteredHandlers);

            foreach (var directory in _tempDirectories)
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch
                {
                }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ContainsUser_WhenIdIsEmpty_ReturnsFalse(string userId)
        {
            // Act
            var contains = UserManager.ContainsUser(userId);

            // Assert
            Assert.False(contains);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetUser_WhenIdIsEmpty_ThrowsArgumentNullException(string userId)
        {
            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => UserManager.GetUser(userId));

            // Assert
            Assert.Equal("userId", exception.ParamName);
        }

        [Fact]
        public void CreateUser_WhenIdIsEmpty_ThrowsArgumentNullException()
        {
            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => UserManager.CreateUser(string.Empty, "Name"));

            // Assert
            Assert.Equal("userId", exception.ParamName);
            Assert.Empty(UserManager.UserIds);
            Assert.Empty(UserManager.UserContexts);
        }

        [Fact]
        public void CreateUser_WhenIdAlreadyExists_ReturnsExistingContext()
        {
            // Arrange
            var original = UserManager.CreateUser("user", "Original");

            // Act
            var duplicate = UserManager.CreateUser("user", "Replacement");

            // Assert
            Assert.Same(original, duplicate);
            Assert.Equal("Original", duplicate.Name);
            Assert.Single(UserManager.UserContexts);
        }

        [Fact]
        public void Register_WhenIdIsEmpty_GeneratesIdAndInitializesService()
        {
            // Act
            var context = UserManager.Register(null, "Generated");

            // Assert
            Assert.StartsWith("User_", context.UserId, StringComparison.Ordinal);
            Assert.Equal("Generated", context.Name);
            Assert.NotNull(context.Service);
            Assert.Equal(context.UserId, context.Service.UserId);
            Assert.Same(context, UserManager.GetUser(context.UserId));
        }

        [Fact]
        public void Register_WhenIdAlreadyExists_CreatesUniqueUserWithoutReplacingOriginal()
        {
            // Arrange
            var original = UserManager.Register("user", "Original");

            // Act
            var duplicate = UserManager.Register("user", "Duplicate");

            // Assert
            Assert.NotEqual(original.UserId, duplicate.UserId);
            Assert.StartsWith("user_", duplicate.UserId, StringComparison.Ordinal);
            Assert.Same(original, UserManager.GetUser("user"));
            Assert.Same(duplicate, UserManager.GetUser(duplicate.UserId));
            Assert.Equal(2, UserManager.UserContexts.Count());
        }

        [Fact]
        public void Register_WhenEventHandlerThrows_InvokesRemainingHandlersAndRegistersUser()
        {
            // Arrange
            UserContext received = null;
            UserManager.OnUserRegistered += _ => throw new InvalidOperationException("handler failure");
            UserManager.OnUserRegistered += context => received = context;

            // Act
            var result = UserManager.Register("user", "Name");

            // Assert
            Assert.Same(result, received);
            Assert.Same(result, UserManager.GetUser("user"));
        }

        [Fact]
        public void Register_WhenConfigRegistered_ForwardsCorrectContextAndInvokesRemainingHandlersAfterFailure()
        {
            // Arrange
            UserManager.Register("other", "Other");
            var expected = UserManager.Register("user", "Name");
            var failingHandlerCalled = false;
            UserContext received = null;
            UserManager.OnConfigRegistered += _ =>
            {
                failingHandlerCalled = true;
                throw new InvalidOperationException("handler failure");
            };
            UserManager.OnConfigRegistered += context => received = context;
            var configPath = Path.Combine(CreateTempDirectory(), "settings.cfg");

            // Act
            expected.Service.RegisterConfig(typeof(TestConfigManager), configPath);

            // Assert
            Assert.True(failingHandlerCalled);
            Assert.Same(expected, received);
            Assert.Equal(configPath, expected.Service.Config.FilePath);
        }

        [Fact]
        public void Register_WhenLogWriterRegistered_ForwardsCorrectContextAndInvokesRemainingHandlersAfterFailure()
        {
            // Arrange
            UserManager.Register("other", "Other");
            var expected = UserManager.Register("user", "Name");
            var failingHandlerCalled = false;
            UserContext received = null;
            UserManager.OnLogWriterRegistered += _ =>
            {
                failingHandlerCalled = true;
                throw new InvalidOperationException("handler failure");
            };
            UserManager.OnLogWriterRegistered += context => received = context;
            var logDirectory = CreateTempDirectory();

            // Act
            expected.Service.RegisterLog(logDirectory, "service.log", LogLevel.Debug);

            // Assert
            Assert.True(failingHandlerCalled);
            Assert.Same(expected, received);
            Assert.NotNull(expected.Service.LogWriter);
        }

        [Fact]
        public void RemoveUser_WhenUserExists_DisposesContextAndRemovesRegistration()
        {
            // Arrange
            var context = UserManager.CreateUser("user", "Name");
            var child = new TrackingContext();
            context.AddChildContext("child", child);

            // Act
            UserManager.RemoveUser("user");

            // Assert
            Assert.True(child.IsDisposed);
            Assert.False(UserManager.ContainsUser("user"));
            Assert.Same(UserContext.InvalidUserContext, UserManager.GetUser("user"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("missing")]
        public void RemoveUser_WhenIdIsEmptyOrUnknown_DoesNotChangeRegistrations(string userId)
        {
            // Arrange
            var existing = UserManager.CreateUser("existing", "Name");

            // Act
            UserManager.RemoveUser(userId);

            // Assert
            Assert.Same(existing, UserManager.GetUser("existing"));
            Assert.Single(UserManager.UserContexts);
        }

        [Fact]
        public void Dispose_DisposesContextsAndClearsRegistrationsAndHandlers()
        {
            // Arrange
            var context = UserManager.CreateUser("user", "Name");
            var child = new TrackingContext();
            context.AddChildContext("child", child);
            UserManager.OnUserRegistered += _ => { };
            UserManager.OnLogWriterRegistered += _ => { };
            UserManager.OnConfigRegistered += _ => { };

            // Act
            UserManager.Dispose();

            // Assert
            Assert.True(child.IsDisposed);
            Assert.Empty(UserManager.UserContexts);
            Assert.Null(OnUserRegisteredField.GetValue(null));
            Assert.Null(OnLogWriterRegisteredField.GetValue(null));
            Assert.Null(OnConfigRegisteredField.GetValue(null));
        }

        private string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"UnityModBase.Test.{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            _tempDirectories.Add(directory);
            return directory;
        }

        private static Dictionary<string, UserContext> GetContexts()
        {
            return (Dictionary<string, UserContext>)UserContextsField.GetValue(null);
        }

        private sealed class TrackingContext : IUserContext
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class TestConfigManager
        {
        }
    }
}
