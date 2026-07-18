using System.Reflection;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HUserSpace
{
    public class UserServiceTests : IDisposable
    {
        private readonly List<string> _tempDirectories = new List<string>();

        public void Dispose()
        {
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
        [InlineData("   ")]
        public void Constructor_WhenUserIdIsNullOrWhitespace_ThrowsArgumentException(string userId)
        {
            var exception = Assert.Throws<ArgumentException>(() => new UserService(userId));

            Assert.Equal("userId", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RegisterLog_WhenDirectoryIsNullOrWhitespace_ThrowsArgumentException(string directory)
        {
            using var service = new UserService("user");

            var exception = Assert.Throws<ArgumentException>(() =>
                service.RegisterLog(directory, "service.log", LogLevel.Info));

            Assert.Equal("directory", exception.ParamName);
            Assert.Null(service.LogWriter);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RegisterLog_WhenFileNameIsNullOrWhitespace_ThrowsArgumentException(string fileName)
        {
            using var service = new UserService("user");

            var exception = Assert.Throws<ArgumentException>(() =>
                service.RegisterLog(CreateTempDirectory(), fileName, LogLevel.Info));

            Assert.Equal("fileName", exception.ParamName);
            Assert.Null(service.LogWriter);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void RegisterConfig_WhenPathIsNullOrEmpty_ThrowsArgumentException(string path)
        {
            using var service = new UserService("user");

            var exception = Assert.Throws<ArgumentException>(() =>
                service.RegisterConfig(typeof(TestConfigManager), path));

            Assert.Equal("configFilePath", exception.ParamName);
            Assert.Null(service.Config);
        }

        [Fact]
        public void RegisterConfig_WhenManagerTypeIsNull_ThrowsArgumentNullException()
        {
            using var service = new UserService("user");
            var path = Path.Combine(CreateTempDirectory(), "settings.cfg");

            var exception = Assert.Throws<ArgumentNullException>(() =>
                service.RegisterConfig(null, path));

            Assert.Equal("configManagerType", exception.ParamName);
            Assert.Null(service.Config);
        }

        [Fact]
        public void RegisterConfig_WithType_InitializesConfigAndManagerType()
        {
            // Arrange
            using var service = new UserService("user");
            var path = Path.Combine(CreateTempDirectory(), "settings.cfg");

            // Act
            service.RegisterConfig(typeof(TestConfigManager), path);

            // Assert
            Assert.Equal(typeof(TestConfigManager), service.ConfigManagerType);
            Assert.NotNull(service.Config);
            Assert.Equal(path, service.Config.FilePath);
        }

        [Fact]
        public void RegisterConfig_WithGenericType_InitializesConfigAndManagerType()
        {
            // Arrange
            using var service = new UserService("user");
            var path = Path.Combine(CreateTempDirectory(), "generic.cfg");

            // Act
            service.RegisterConfig<TestConfigManager>(path);

            // Assert
            Assert.Equal(typeof(TestConfigManager), service.ConfigManagerType);
            Assert.Equal(path, service.Config.FilePath);
        }

        [Fact]
        public void RegisterLog_WhenDatabaseAlreadyContainsLogs_WritesExistingAndFutureLogs()
        {
            // Arrange
            var directory = CreateTempDirectory();
            using var service = new UserService("user");
            var database = new LogDatabase(null);
            SetLogDatabase(service, database);
            database.AddLog(CreateLog(1, "existing"));

            // Act
            service.RegisterLog(directory, "service.log", LogLevel.Debug);
            database.AddLog(CreateLog(2, "future"));
            database.AddLog(CreateLog(3, "existing"));
            service.Dispose();

            // Assert
            var path = Assert.Single(Directory.GetFiles(directory, "*.log"));
            var content = File.ReadAllText(path);
            Assert.Contains("existing", content, StringComparison.Ordinal);
            Assert.Contains("future", content, StringComparison.Ordinal);
            Assert.Contains("Info x2 | existing", content, StringComparison.Ordinal);
            Assert.Null(service.LogDatabase);
            Assert.Null(service.LogWriter);
            Assert.Null(service.Config);
        }

        [Fact]
        public void RegisterLog_CalledTwice_WritesFutureLogsOnlyToLatestWriter()
        {
            // Arrange
            var firstDirectory = CreateTempDirectory();
            var secondDirectory = CreateTempDirectory();
            using var service = new UserService("user");
            var database = new LogDatabase(null);
            SetLogDatabase(service, database);

            // Act
            service.RegisterLog(firstDirectory, "service.log", LogLevel.Debug);
            service.RegisterLog(secondDirectory, "service.log", LogLevel.Debug);
            database.AddLog(CreateLog(1, "future"));
            service.Dispose();

            // Assert
            Assert.DoesNotContain("future", ReadAllLogs(firstDirectory), StringComparison.Ordinal);
            Assert.Contains("future", ReadAllLogs(secondDirectory), StringComparison.Ordinal);
        }

        [Fact]
        public void OnConfigChanged_SubscribedBeforeConfigRegistration_ForwardsLaterChanges()
        {
            using var service = new UserService("user");
            var path = Path.Combine(CreateTempDirectory(), "settings.cfg");
            var invocationCount = 0;
            service.OnConfigChanged += () => invocationCount++;

            service.RegisterConfig(typeof(TestConfigManager), path);
            service.Config.CreateTable("TestTable", new Translator("测试表", "Test Table"));

            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void OnConfigChanged_WhenHandlerThrows_InvokesRemainingHandlersWithUpdatedConfig()
        {
            using var service = new UserService("user");
            var path = Path.Combine(CreateTempDirectory(), "settings.cfg");
            service.RegisterConfig(typeof(TestConfigManager), path);
            var failingHandlerCalled = false;
            var remainingHandlerCalled = false;
            service.OnConfigChanged += () =>
            {
                failingHandlerCalled = true;
                throw new InvalidOperationException("handler failure");
            };
            service.OnConfigChanged += () =>
                remainingHandlerCalled = service.Config.Sheet.Contains("TestTable");

            var exception = Record.Exception(() =>
                service.Config.CreateTable("TestTable", new Translator("测试表", "Test Table")));

            Assert.Null(exception);
            Assert.True(failingHandlerCalled);
            Assert.True(remainingHandlerCalled);
        }

        private string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"UnityModBase.Test.{Guid.NewGuid():N}");
            _tempDirectories.Add(directory);
            return directory;
        }

        private static void SetLogDatabase(UserService service, LogDatabase database)
        {
            var property = typeof(UserService).GetProperty(nameof(UserService.LogDatabase), BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            service.LogDatabase?.Dispose();
            property.SetValue(service, database);
        }

        private static LogEntry CreateLog(int id, string message)
        {
            return new LogEntry(
                id,
                new DateTime(2026, 7, 4, 12, 0, 0).AddSeconds(id),
                7,
                10,
                "Scene",
                LogLevel.Info,
                message,
                "File.cs",
                12,
                "Member",
                null);
        }

        private static string ReadAllLogs(string directory)
        {
            if (!Directory.Exists(directory))
                return string.Empty;

            return string.Join(
                Environment.NewLine,
                Directory.GetFiles(directory, "*.log")
                    .Select(File.ReadAllText));
        }

        private sealed class TestConfigManager
        {
        }
    }
}
