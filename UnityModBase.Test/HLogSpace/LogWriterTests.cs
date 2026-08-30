using UnityModBase.HLogSpace;

namespace UnityModBase.Test.HLogSpace
{
    public class LogWriterTests : IDisposable
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
        public void Dispose_WhenLogAccepted_WritesHeaderEntryAndFooter()
        {
            // Arrange
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "application.txt", LogLevel.Debug);
            var log = CreateLog(1, LogLevel.Info, "accepted");

            // Act
            writer.Log(log);
            writer.Dispose();

            // Assert
            var content = ReadOnlyLogFile(directory);
            Assert.Contains("LOG START |", content, StringComparison.Ordinal);
            Assert.Contains("| Initial minimum level: DEBUG", content, StringComparison.Ordinal);
            Assert.Contains(log.ToString(), content, StringComparison.Ordinal);
            Assert.Contains("LOG END   |", content, StringComparison.Ordinal);
        }

        [Fact]
        public void Log_WhenBelowConfiguredLevel_OmitsEntryButWritesHigherLevelEntry()
        {
            // Arrange
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "levels.log", LogLevel.Warning);

            // Act
            writer.Log(CreateLog(1, LogLevel.Info, "filtered"));
            writer.Log(CreateLog(2, LogLevel.Error, "included"));
            writer.Dispose();

            // Assert
            var content = ReadOnlyLogFile(directory);
            Assert.DoesNotContain("filtered", content, StringComparison.Ordinal);
            Assert.Contains("included", content, StringComparison.Ordinal);
        }

        [Fact]
        public void Write_WhenDisabled_OmitsEntry()
        {
            // Arrange
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "disabled.log", LogLevel.Debug)
            {
                Enable = false
            };

            // Act
            writer.Write(CreateLog(1, LogLevel.Error, "disabled-entry"));
            writer.Dispose();

            // Assert
            var content = ReadOnlyLogFile(directory);
            Assert.DoesNotContain("disabled-entry", content, StringComparison.Ordinal);
        }

        [Fact]
        public void Dispose_WhenLastLogWasRepeated_ForceFlushesRepeatSummary()
        {
            // Arrange
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "repeated.log", LogLevel.Debug);
            var log = CreateLog(1, LogLevel.Info, "repeated-entry");
            writer.Log(log);
            log.UpdateRepeat(log.Timestamp.AddSeconds(1));
            log.UpdateRepeat(log.Timestamp.AddSeconds(1));

            // Act
            writer.Dispose();

            // Assert
            var content = ReadOnlyLogFile(directory);
            Assert.Contains("repeated-entry [repeated x3", content, StringComparison.Ordinal);
        }

        [Fact]
        public void Log_WhenPreviousLogBecameRepeated_WritesSummaryBeforeNextEntry()
        {
            // Arrange
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "rollover.log", LogLevel.Debug);
            var repeated = CreateLog(1, LogLevel.Info, "repeated-entry");
            writer.Log(repeated);
            repeated.UpdateRepeat(repeated.Timestamp.AddSeconds(1));

            // Act
            writer.Log(CreateLog(2, LogLevel.Warning, "next-entry"));
            writer.Dispose();

            // Assert
            var content = ReadOnlyLogFile(directory);
            var summaryIndex = content.IndexOf("repeated-entry [repeated x2", StringComparison.Ordinal);
            var nextEntryIndex = content.IndexOf("[WARNING] [#2]", StringComparison.Ordinal);
            Assert.True(summaryIndex >= 0, "The repeat summary was not written.");
            Assert.True(nextEntryIndex > summaryIndex, "The next entry was written before the repeat summary.");
        }

        [Fact]
        public void Constructor_WhenDirectoryIsInvalid_ContainsFileSystemFailure()
        {
            // Act
            var exception = Record.Exception(() =>
            {
                using var writer = new LogWriter(null, "invalid.log", LogLevel.Info);
                writer.Log(CreateLog(1, LogLevel.Info, "ignored"));
                writer.Flush(true);
            });

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Log_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            using var writer = new LogWriter(CreateTempDirectory(), "null-log.log", LogLevel.Debug);

            var exception = Assert.Throws<ArgumentNullException>(() => writer.Log(null));

            Assert.Equal("log", exception.ParamName);
        }

        [Fact]
        public void Write_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            using var writer = new LogWriter(CreateTempDirectory(), "null-write.log", LogLevel.Debug);

            var exception = Assert.Throws<ArgumentNullException>(() => writer.Write(null));

            Assert.Equal("log", exception.ParamName);
        }

        [Fact]
        public void Log_WhenSameEntryIsLoggedTwice_WritesItOnlyOnce()
        {
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "duplicate.log", LogLevel.Debug);
            var log = CreateLog(1, LogLevel.Info, "duplicate-entry");

            writer.Log(log);
            writer.Log(log);
            writer.Dispose();

            var content = ReadOnlyLogFile(directory);
            Assert.Equal(1, content.Split(new[] { "duplicate-entry" }, StringSplitOptions.None).Length - 1);
        }

        [Fact]
        public void Flush_WhenRepeatDurationExceedsLimit_WritesSummaryWithoutForce()
        {
            var directory = CreateTempDirectory();
            var writer = new LogWriter(directory, "duration.log", LogLevel.Debug);
            var timestamp = DateTime.Now;
            var log = CreateLog(1, LogLevel.Info, "duration-entry", timestamp);
            writer.Log(log);
            log.UpdateRepeat(timestamp.AddSeconds(6));

            writer.Flush();
            writer.Dispose();

            Assert.Contains("duration-entry [repeated x2", ReadOnlyLogFile(directory), StringComparison.Ordinal);
        }

        private string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"UnityModBase.Test.{Guid.NewGuid():N}");
            _tempDirectories.Add(directory);
            return directory;
        }

        private static string ReadOnlyLogFile(string directory)
        {
            var path = Assert.Single(Directory.GetFiles(directory, "*.log"));
            return File.ReadAllText(path);
        }

        private static LogEntry CreateLog(int id, LogLevel level, string message, DateTime? timestamp = null)
        {
            return new LogEntry(
                id,
                timestamp ?? new DateTime(2026, 7, 4, 12, 0, 0).AddSeconds(id),
                7,
                10,
                "Scene",
                level,
                message,
                "File.cs",
                12,
                "Member",
                null);
        }
    }
}
