using UnityModBase.HLogSpace;

namespace UnityModBase.Test.HLogSpace
{
    public class LogDatabaseTests
    {
        [Fact]
        public void AddLog_WhenLogIsNull_ThrowsAndDoesNotChangeDatabaseOrRaiseEvent()
        {
            // Arrange
            using var database = new LogDatabase(null);
            var addedCount = 0;
            database.OnLogAdded += _ => addedCount++;

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => database.AddLog((LogEntry)null));

            // Assert
            Assert.Equal("log", exception.ParamName);
            Assert.Empty(database.Logs);
            Assert.Equal(0, addedCount);
        }

        [Fact]
        public void AddLog_WhenHandlerThrows_InvokesRemainingHandlersAndStoresLog()
        {
            // Arrange
            using var database = new LogDatabase(null);
            var log = CreateLog(1, "message");
            LogEntry received = null;
            database.OnLogAdded += _ => throw new InvalidOperationException("handler failure");
            database.OnLogAdded += entry => received = entry;

            // Act
            database.AddLog(log);

            // Assert
            Assert.Same(log, Assert.Single(database.Logs));
            Assert.Same(log, received);
        }

        [Fact]
        public void Entries_WhenDatabaseChanges_PreservesCapturedSnapshot()
        {
            using var database = new LogDatabase(null);
            database.AddLog(CreateLog(1, "first"));
            var snapshot = database.Logs;

            database.AddLog(CreateLog(2, "second"));

            Assert.Single(snapshot);
            Assert.Equal(2, database.Logs.Count);
        }

        [Fact]
        public void AddLog_WhenEquivalentLogExists_UpdatesOriginalAndRaisesRepeatedEvent()
        {
            // Arrange
            using var database = new LogDatabase(null);
            var original = CreateLog(1, "repeated");
            var repeated = CreateLog(2, "repeated", original.Timestamp.AddSeconds(2));
            LogEntry received = null;
            database.OnLogRepeated += _ => throw new InvalidOperationException("handler failure");
            database.OnLogRepeated += entry => received = entry;
            database.AddLog(original);

            // Act
            database.AddLog(repeated);

            // Assert
            Assert.Same(original, Assert.Single(database.Logs));
            Assert.Equal(2, original.RepeatCount);
            Assert.Equal(repeated.Timestamp, original.LastRepeatTime);
            Assert.Same(original, received);
        }

        [Fact]
        public void AddLog_WhenCapacityExceeded_RemovesOldestAndRaisesRemovedEvent()
        {
            // Arrange
            using var database = new LogDatabase(null);
            LogEntry removed = null;
            database.OnLogRemoved += _ => throw new InvalidOperationException("handler failure");
            database.OnLogRemoved += entry => removed = entry;
            for (var id = 1; id <= LogDatabase.MaxLogCount; id++)
                database.AddLog(CreateLog(id, $"message-{id}"));

            // Act
            database.AddLog(CreateLog(LogDatabase.MaxLogCount + 1, "overflow"));

            // Assert
            Assert.Equal(LogDatabase.MaxLogCount, database.Logs.Count);
            Assert.Equal(1, removed.Id);
            Assert.Equal(2, database.Logs.First().Id);
            Assert.Equal(LogDatabase.MaxLogCount + 1, database.Logs.Last().Id);
        }

        [Fact]
        public void AddLog_WithDetails_AssignsSequenceAndNullProviderFallbacks()
        {
            // Arrange
            using var database = new LogDatabase(null);
            var exception = new InvalidOperationException("failure");

            // Act
            database.AddLog(LogLevel.Error, "message", exception, "Run", "Code.cs", 42);

            // Assert
            var log = Assert.Single(database.Logs);
            Assert.Equal(1, database.Seq);
            Assert.Equal(1, log.Id);
            Assert.Equal(Environment.CurrentManagedThreadId, log.ThreadId);
            Assert.Equal(0, log.Frame);
            Assert.Equal("?", log.Scene);
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.Equal("message", log.Message);
            Assert.Equal("Run", log.Member);
            Assert.Equal("Code.cs", log.File);
            Assert.Equal(42, log.Line);
            Assert.Same(exception, log.Exception);
        }

        [Fact]
        public void AddLog_WithDetailsCalledConcurrently_AssignsUniqueCompleteSequenceIds()
        {
            // Arrange
            using var database = new LogDatabase(null);
            const int logCount = 200;

            // Act
            Parallel.For(0, logCount, index =>
                database.AddLog(LogLevel.Info, $"message-{index}", null, "Run", "Code.cs", index));

            // Assert
            var logs = database.Logs;
            var ids = logs.Select(log => log.Id).OrderBy(id => id).ToArray();
            Assert.Equal(logCount, database.Seq);
            Assert.Equal(logCount, logs.Count);
            Assert.Equal(logCount, ids.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, logCount), ids);
        }

        [Fact]
        public void AddLog_WhenEquivalentLogsAreAddedConcurrently_MergesEveryOccurrence()
        {
            // Arrange
            using var database = new LogDatabase(null);
            const int logCount = 200;
            var addedCount = 0;
            var repeatedCount = 0;
            database.OnLogAdded += _ => addedCount++;
            database.OnLogRepeated += _ => repeatedCount++;

            // Act
            Parallel.For(0, logCount, index =>
                database.AddLog(CreateLog(index + 1, "repeated")));

            // Assert
            var log = Assert.Single(database.Logs);
            Assert.Equal(logCount, log.RepeatCount);
            Assert.Equal(1, addedCount);
            Assert.Equal(logCount - 1, repeatedCount);
        }

        [Fact]
        public async Task AddLog_WhenEarlierNotificationIsBlocked_DoesNotCommitLaterLogOutOfOrder()
        {
            using var database = new LogDatabase(null);
            using var firstHandlerEntered = new ManualResetEventSlim(false);
            using var releaseFirstHandler = new ManualResetEventSlim(false);
            using var secondAddStarted = new ManualResetEventSlim(false);
            var notificationIds = new List<int>();
            database.OnLogAdded += log =>
            {
                notificationIds.Add(log.Id);
                if (log.Id == 1)
                {
                    firstHandlerEntered.Set();
                    releaseFirstHandler.Wait(TimeSpan.FromSeconds(5));
                }
            };

            var firstAdd = Task.Run(() => database.AddLog(CreateLog(1, "first")));
            Assert.True(firstHandlerEntered.Wait(TimeSpan.FromSeconds(5)));
            var secondAdd = Task.Run(() =>
            {
                secondAddStarted.Set();
                database.AddLog(CreateLog(2, "second"));
            });
            Assert.True(secondAddStarted.Wait(TimeSpan.FromSeconds(5)));

            await Task.Delay(100);
            var countWhileFirstNotificationBlocked = database.Logs.Count;
            releaseFirstHandler.Set();
            await Task.WhenAll(firstAdd, secondAdd);

            Assert.Equal(1, countWhileFirstNotificationBlocked);
            Assert.Equal(new[] { 1, 2 }, notificationIds);
            Assert.Equal(new[] { 1, 2 }, database.Logs.Select(log => log.Id));
        }

        [Fact]
        public async Task SubscribeWithSnapshot_WhenInitializationIsBlocked_DeliversLaterLogAfterSnapshot()
        {
            using var database = new LogDatabase(null);
            using var initializerEntered = new ManualResetEventSlim(false);
            using var releaseInitializer = new ManualResetEventSlim(false);
            database.AddLog(CreateLog(1, "existing"));
            IReadOnlyList<LogEntry> snapshot = null;
            var addedIds = new List<int>();

            var subscribe = Task.Run(() => database.SubscribeWithSnapshot(
                logs =>
                {
                    snapshot = logs;
                    initializerEntered.Set();
                    releaseInitializer.Wait(TimeSpan.FromSeconds(5));
                },
                log => addedIds.Add(log.Id),
                null,
                null));
            Assert.True(initializerEntered.Wait(TimeSpan.FromSeconds(5)));

            var add = Task.Run(() => database.AddLog(CreateLog(2, "future")));
            await Task.Delay(100);
            var countWhileInitializing = database.Logs.Count;
            releaseInitializer.Set();
            await Task.WhenAll(subscribe, add);

            Assert.Equal(1, countWhileInitializing);
            Assert.Equal(new[] { 1 }, snapshot.Select(log => log.Id));
            Assert.Equal(new[] { 2 }, addedIds);
            Assert.Equal(new[] { 1, 2 }, database.Logs.Select(log => log.Id));
        }

        [Fact]
        public void AddLog_WhenHandlerReentersDatabase_PreservesOrderForEverySubscriber()
        {
            using var database = new LogDatabase(null);
            var notifications = new List<string>();
            database.OnLogAdded += log =>
            {
                notifications.Add($"first:{log.Id}");
                if (log.Id == 1)
                    database.AddLog(CreateLog(2, "nested"));
            };
            database.OnLogAdded += log => notifications.Add($"second:{log.Id}");

            database.AddLog(CreateLog(1, "outer"));

            Assert.Equal(
                new[] { "first:1", "second:1", "first:2", "second:2" },
                notifications);
        }

        [Theory]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Info)]
        [InlineData(LogLevel.Notice)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        public void ConvenienceLogger_WhenCalled_AddsExpectedLevel(LogLevel level)
        {
            // Arrange
            using var database = new LogDatabase(null);
            var exception = new InvalidOperationException("failure");

            // Act
            switch (level)
            {
                case LogLevel.Debug:
                    database.Debug("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Info:
                    database.Info("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Notice:
                    database.Notice("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Warning:
                    database.Warn("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Error:
                    database.Error("message", exception, "Member", "File.cs", 7);
                    break;
            }

            // Assert
            var log = Assert.Single(database.Logs);
            Assert.Equal(level, log.Level);
            Assert.Equal("message", log.Message);
            Assert.Equal(level == LogLevel.Error ? exception : null, log.Exception);
        }

        [Fact]
        public void Dispose_WhenLogsAndHandlersExist_RemovesAllLogsResetsSequenceAndClearsHandlers()
        {
            // Arrange
            var database = new LogDatabase(null);
            var removedIds = new List<int>();
            var addedCount = 0;
            var repeatedCount = 0;
            database.OnLogRemoved += _ => throw new InvalidOperationException("handler failure");
            database.OnLogRemoved += log => removedIds.Add(log.Id);
            database.OnLogAdded += _ => addedCount++;
            database.OnLogRepeated += _ => repeatedCount++;
            database.Info("first", "Member", "File.cs", 1);
            database.Info("second", "Member", "File.cs", 2);

            // Act
            database.Dispose();
            database.AddLog(CreateLog(3, "after-dispose"));
            database.AddLog(CreateLog(4, "second-after-dispose"));

            // Assert
            Assert.Equal(new[] { 1, 2 }, removedIds);
            Assert.Equal(0, database.Seq);
            Assert.Equal(2, database.Logs.Count);
            Assert.Equal(2, addedCount);
            Assert.Equal(0, repeatedCount);
        }

        private static LogEntry CreateLog(int id, string message, DateTime? timestamp = null)
        {
            return new LogEntry(
                id,
                timestamp ?? new DateTime(2026, 7, 4, 12, 0, 0).AddSeconds(id),
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
    }
}
