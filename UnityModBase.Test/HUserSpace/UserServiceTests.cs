using System.Reflection;
using UnityModBase.HLogSpace;
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
