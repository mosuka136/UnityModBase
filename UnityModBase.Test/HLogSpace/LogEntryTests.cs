using UnityModBase.HLogSpace;

namespace UnityModBase.Test.HLogSpace
{
    public class LogEntryTests
    {
        [Fact]
        public void Constructor_WhenOptionalStringsAreNull_UsesFallbackValues()
        {
            // Arrange
            var exception = new InvalidOperationException("boom");
            var timestamp = new DateTime(2026, 6, 13, 1, 2, 3, 4);

            // Act
            var entry = new LogEntry(7, timestamp, 3, 12, null, LogLevel.Warning, null, null, 45, null, exception);

            // Assert
            Assert.Equal(7, entry.Id);
            Assert.Equal(timestamp, entry.Timestamp);
            Assert.Equal(3, entry.ThreadId);
            Assert.Equal(12, entry.Frame);
            Assert.Equal("?", entry.Scene);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal(string.Empty, entry.Message);
            Assert.Equal(string.Empty, entry.File);
            Assert.Equal(45, entry.Line);
            Assert.Equal(string.Empty, entry.Member);
            Assert.Same(exception, entry.Exception);
            Assert.Equal(timestamp, entry.LastRepeatTime);
            Assert.Equal(1, entry.RepeatCount);
            Assert.False(entry.IsRepeated);
        }

        [Fact]
        public void Constructor_WhenAllValuesProvided_AssignsOriginalValues()
        {
            // Arrange
            var exception = new Exception("failure");
            var timestamp = new DateTime(2026, 6, 13, 12, 34, 56, 789);

            // Act
            var entry = new LogEntry(11, timestamp, 8, 99, "BattleScene", LogLevel.Error, "Something happened", @"C:\Logs\Game.cs", 123, "Run", exception);

            // Assert
            Assert.Equal(11, entry.Id);
            Assert.Equal(timestamp, entry.Timestamp);
            Assert.Equal(8, entry.ThreadId);
            Assert.Equal(99, entry.Frame);
            Assert.Equal("BattleScene", entry.Scene);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("Something happened", entry.Message);
            Assert.Equal(@"C:\Logs\Game.cs", entry.File);
            Assert.Equal(123, entry.Line);
            Assert.Equal("Run", entry.Member);
            Assert.Same(exception, entry.Exception);
        }

        [Fact]
        public void ToString_WhenFileMemberAndExceptionPresent_IncludesLocationAndException()
        {
            // Arrange
            var exception = new InvalidOperationException("boom");
            var entry = new LogEntry(5, new DateTime(2026, 6, 13, 1, 2, 3, 4), 2, 77, "Town", LogLevel.Notice, "Hello", @"C:\Code\Player.cs", 18, "Update", exception);

            // Act
            var result = entry.ToString();

            // Assert
            var expected =
                "[2026-06-13 01:02:03.004] [NOTICE] [#5] [T2] [F77] [Scene:Town] Hello" + Environment.NewLine +
                "    at Update (Player.cs:18)" + Environment.NewLine +
                "    Exception: " + exception;
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("", @"C:\Code\Player.cs", 18)]
        [InlineData("Update", "", 18)]
        [InlineData("Update", @"C:\Code\Player.cs", 0)]
        public void ToString_WhenLocationInfoIncomplete_OmitsLocationSuffix(string member, string file, int line)
        {
            // Arrange
            var entry = new LogEntry(9, new DateTime(2026, 6, 13, 9, 8, 7, 6), 4, 15, "Menu", LogLevel.Info, "Ready", file, line, member, null);

            // Act
            var result = entry.ToString();

            // Assert
            Assert.Equal("[2026-06-13 09:08:07.006] [INFO] [#9] [T4] [F15] [Scene:Menu] Ready", result);
        }

        [Fact]
        public void ToString_WhenExceptionIsNull_ReturnsSingleLineText()
        {
            // Arrange
            var entry = new LogEntry(1, new DateTime(2026, 6, 13, 10, 11, 12, 13), 6, 20, "Map", LogLevel.Debug, "Trace", @"C:\Temp\Trace.cs", 3, "Tick", null);

            // Act
            var result = entry.ToString();

            // Assert
            var expected =
                "[2026-06-13 10:11:12.013] [DEBUG] [#1] [T6] [F20] [Scene:Map] Trace" + Environment.NewLine +
                "    at Tick (Trace.cs:3)";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToString_WhenMessageIsMultilineAndRepeated_IndentsContinuationAndShowsSummary()
        {
            // Arrange
            var entry = new LogEntry(
                3,
                new DateTime(2026, 6, 13, 10, 11, 12, 13),
                6,
                20,
                "Map",
                LogLevel.Warning,
                "First line\nSecond line",
                string.Empty,
                0,
                string.Empty,
                null)
            {
                RepeatCount = 4,
                LastRepeatTime = new DateTime(2026, 6, 13, 10, 11, 15, 16)
            };

            // Act
            var result = entry.ToString();

            // Assert
            var expected =
                "[2026-06-13 10:11:12.013] [WARNING] [#3] [T6] [F20] [Scene:Map] First line" + Environment.NewLine +
                "    Second line [repeated x4, last at 2026-06-13 10:11:15.016]";
            Assert.Equal(expected, result);
        }
    }
}
