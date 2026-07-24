using System.Reflection;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HUserSpace
{
    public class UserManagerTests : IDisposable
    {
        // UserManager 的注册表和事件均为进程级静态状态；
        // 测试通过反射建立独立快照，并在释放夹具时完整恢复。
        private static readonly FieldInfo UserContextsField = typeof(UserManager)
            .GetField("_userContexts", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnUserRegisteredField = typeof(UserManager)
            .GetField(nameof(UserManager.OnUserRegistered), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnUserRemovedField = typeof(UserManager)
            .GetField(nameof(UserManager.OnUserRemoved), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo OnConfigChangedField = typeof(UserManager)
            .GetField(nameof(UserManager.OnConfigChanged), BindingFlags.NonPublic | BindingFlags.Static);

        private readonly Dictionary<string, UserContext> _originalContexts;
        private readonly Action<UserContext> _originalUserRegisteredHandlers;
        private readonly Action<string> _originalUserRemovedHandlers;
        private readonly Action<UserContext> _originalConfigChangedHandlers;
        private readonly List<string> _tempDirectories = new List<string>();

        public UserManagerTests()
        {
            Assert.NotNull(UserContextsField);
            Assert.NotNull(OnUserRegisteredField);
            Assert.NotNull(OnUserRemovedField);
            Assert.NotNull(OnConfigChangedField);

            var contexts = GetContexts();
            _originalContexts = contexts.ToDictionary(pair => pair.Key, pair => pair.Value);
            _originalUserRegisteredHandlers = (Action<UserContext>)OnUserRegisteredField.GetValue(null);
            _originalUserRemovedHandlers = (Action<string>)OnUserRemovedField.GetValue(null);
            _originalConfigChangedHandlers = (Action<UserContext>)OnConfigChangedField.GetValue(null);
            contexts.Clear();
            OnUserRegisteredField.SetValue(null, null);
            OnUserRemovedField.SetValue(null, null);
            OnConfigChangedField.SetValue(null, null);
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
            OnUserRemovedField.SetValue(null, _originalUserRemovedHandlers);
            OnConfigChangedField.SetValue(null, _originalConfigChangedHandlers);

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
        public void GetUser_WhenIdIsUnknown_ReturnsNull()
        {
            // Act
            var result = UserManager.GetUser("missing");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void CreateUser_WhenIdIsEmpty_ThrowsArgumentNullException()
        {
            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => UserManager.CreateUser(string.Empty, CreateName("Name")));

            // Assert
            Assert.Equal("userId", exception.ParamName);
            Assert.Empty(UserManager.UserIds);
            Assert.Empty(UserManager.UserContexts);
        }

        [Fact]
        public void CreateUser_WhenIdAlreadyExists_ReturnsExistingContext()
        {
            // Arrange
            var originalName = CreateName("Original");
            var original = UserManager.CreateUser("user", originalName);

            // Act
            var duplicate = UserManager.CreateUser("user", CreateName("Replacement"));

            // Assert
            Assert.Same(original, duplicate);
            Assert.Same(originalName, duplicate.Name);
            Assert.Single(UserManager.UserContexts);
        }

        [Fact]
        public void CreateUser_WhenNameIsNull_ThrowsArgumentNullExceptionWithoutRegisteringUser()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => UserManager.CreateUser("user", null));

            Assert.Equal("name", exception.ParamName);
            Assert.Empty(UserManager.UserContexts);
        }

        [Fact]
        public void Register_WhenIdIsEmpty_GeneratesIdAndInitializesService()
        {
            var name = CreateName("Generated");

            // Act
            var context = UserManager.Register(null, name);

            // Assert
            Assert.StartsWith("User_", context.UserId, StringComparison.Ordinal);
            Assert.Same(name, context.Name);
            Assert.NotNull(context.Service);
            Assert.Equal(context.UserId, context.Service.UserId);
            Assert.Same(context, UserManager.GetUser(context.UserId));
        }

        [Fact]
        public void Register_WhenIdAlreadyExists_CreatesUniqueUserWithoutReplacingOriginal()
        {
            // Arrange
            var original = UserManager.Register("user", CreateName("Original"));

            // Act
            var duplicate = UserManager.Register("user", CreateName("Duplicate"));

            // Assert
            Assert.NotEqual(original.UserId, duplicate.UserId);
            Assert.StartsWith("user_", duplicate.UserId, StringComparison.Ordinal);
            Assert.Same(original, UserManager.GetUser("user"));
            Assert.Same(duplicate, UserManager.GetUser(duplicate.UserId));
            Assert.Equal(2, UserManager.UserContexts.Count());
        }

        [Fact]
        public void Register_WhenNameIsNull_ThrowsArgumentNullExceptionWithoutGeneratingUser()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => UserManager.Register(null, null));

            Assert.Equal("name", exception.ParamName);
            Assert.Empty(UserManager.UserContexts);
        }

        [Fact]
        public void Register_WhenEventHandlerThrows_InvokesRemainingHandlersAndRegistersUser()
        {
            // Arrange
            UserContext received = null;
            UserManager.OnUserRegistered += _ => throw new InvalidOperationException("handler failure");
            UserManager.OnUserRegistered += context => received = context;

            // Act
            var result = UserManager.Register("user", CreateName("Name"));

            // Assert
            Assert.Same(result, received);
            Assert.Same(result, UserManager.GetUser("user"));
        }

        [Fact]
        public void Register_WhenConfigChanges_ForwardsCorrectContextAndInvokesRemainingHandlersAfterFailure()
        {
            // Arrange
            UserManager.Register("other", CreateName("Other"));
            var expected = UserManager.Register("user", CreateName("Name"));
            var configPath = Path.Combine(CreateTempDirectory(), "settings.cfg");
            expected.Service.RegisterConfig(typeof(TestConfigManager), configPath);
            var failingHandlerCalled = false;
            UserContext received = null;
            UserManager.OnConfigChanged += _ =>
            {
                failingHandlerCalled = true;
                throw new InvalidOperationException("handler failure");
            };
            UserManager.OnConfigChanged += context => received = context;

            // Act
            expected.Service.Config.CreateTable("TestTable", new Translator("测试表", "Test Table"));

            // Assert
            Assert.True(failingHandlerCalled);
            Assert.Same(expected, received);
            Assert.True(expected.Service.Config.Sheet.Contains("TestTable"));
            Assert.Equal(configPath, expected.Service.Config.FilePath);
        }

        [Fact]
        public void RemoveUser_WhenUserExists_PublishesAfterDisposalAndRegistrationRemoval()
        {
            // Arrange
            var context = UserManager.CreateUser("user", CreateName("Name"));
            var child = new TrackingContext();
            context.AddChildContext("child", child);
            string removedUserId = null;
            var contextDisposedBeforeNotification = false;
            var registrationRemovedBeforeNotification = false;
            UserManager.OnUserRemoved += userId =>
            {
                removedUserId = userId;
                contextDisposedBeforeNotification = child.IsDisposed;
                registrationRemovedBeforeNotification = !UserManager.ContainsUser(userId);
            };

            // Act
            UserManager.RemoveUser("user");

            // Assert
            Assert.Equal("user", removedUserId);
            Assert.True(contextDisposedBeforeNotification);
            Assert.True(registrationRemovedBeforeNotification);
            Assert.True(child.IsDisposed);
            Assert.False(UserManager.ContainsUser("user"));
            Assert.Null(UserManager.GetUser("user"));
        }

        [Fact]
        public void RemoveUser_WhenRemovalHandlerThrows_InvokesRemainingHandlers()
        {
            // Arrange
            UserManager.CreateUser("user", CreateName("Name"));
            var failingHandlerCalled = false;
            string receivedUserId = null;
            UserManager.OnUserRemoved += _ =>
            {
                failingHandlerCalled = true;
                throw new InvalidOperationException("handler failure");
            };
            UserManager.OnUserRemoved += userId => receivedUserId = userId;

            // Act
            UserManager.RemoveUser("user");

            // Assert
            Assert.True(failingHandlerCalled);
            Assert.Equal("user", receivedUserId);
            Assert.False(UserManager.ContainsUser("user"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("missing")]
        public void RemoveUser_WhenIdIsEmptyOrUnknown_DoesNotChangeRegistrationsOrPublish(string userId)
        {
            // Arrange
            var existing = UserManager.CreateUser("existing", CreateName("Name"));
            var notificationCount = 0;
            UserManager.OnUserRemoved += _ => notificationCount++;

            // Act
            UserManager.RemoveUser(userId);

            // Assert
            Assert.Same(existing, UserManager.GetUser("existing"));
            Assert.Single(UserManager.UserContexts);
            Assert.Equal(0, notificationCount);
        }

        [Fact]
        public void Dispose_DisposesContextsAndClearsRegistrationsAndNonRemovalHandlers()
        {
            // Arrange
            var context = UserManager.CreateUser("user", CreateName("Name"));
            var child = new TrackingContext();
            context.AddChildContext("child", child);
            UserManager.OnUserRegistered += _ => { };
            UserManager.OnConfigChanged += _ => { };

            // Act
            UserManager.Dispose();

            // Assert
            Assert.True(child.IsDisposed);
            Assert.Empty(UserManager.UserContexts);
            Assert.Null(OnUserRegisteredField.GetValue(null));
            Assert.Null(OnConfigChangedField.GetValue(null));
        }

        [Fact]
        public void Dispose_WhenRemovalHandlerIsSubscribed_PreservesHandlerWithoutPublishing()
        {
            // Arrange
            UserManager.CreateUser("user", CreateName("Name"));
            var removalNotificationCount = 0;
            Action<string> removalHandler = _ => removalNotificationCount++;
            UserManager.OnUserRemoved += removalHandler;

            // Act
            UserManager.Dispose();

            // Assert
            Assert.Equal(0, removalNotificationCount);
            Assert.Same(removalHandler, OnUserRemovedField.GetValue(null));
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

        private static Translator CreateName(string english)
        {
            return new Translator($"测试-{english}", english);
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
