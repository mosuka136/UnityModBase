using System.Reflection;
using UnityModBase.BSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.BSpace
{
    public class BLogTests
    {
        [Theory]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Info)]
        [InlineData(LogLevel.Notice)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        public void Logger_WhenContextHasDatabase_ForwardsLevelMessageExceptionAndCaller(LogLevel level)
        {
            // Arrange
            using var scope = BLogStateScope.Create();
            var exception = new InvalidOperationException("failure");

            // Act
            switch (level)
            {
                case LogLevel.Debug:
                    BLog.Debug("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Info:
                    BLog.Info("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Notice:
                    BLog.Notice("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Warning:
                    BLog.Warn("message", "Member", "File.cs", 7);
                    break;
                case LogLevel.Error:
                    BLog.Error("message", exception, "Member", "File.cs", 7);
                    break;
            }

            // Assert
            var log = Assert.Single(scope.LogDatabase.Logs);
            Assert.Equal(level, log.Level);
            Assert.Equal("message", log.Message);
            Assert.Equal("Member", log.Member);
            Assert.Equal("File.cs", log.File);
            Assert.Equal(7, log.Line);
            Assert.Equal(level == LogLevel.Error ? exception : null, log.Exception);
        }

        [Fact]
        public void Logger_WhenContextIsNull_IsSafeNoOp()
        {
            // Arrange
            using var scope = BLogStateScope.CreateWithoutContext();

            // Act
            var exception = Record.Exception(() =>
            {
                BLog.Debug("debug");
                BLog.Info("info");
                BLog.Notice("notice");
                BLog.Warn("warning");
                BLog.Error("error", new InvalidOperationException("failure"));
            });

            // Assert
            Assert.Null(exception);
            Assert.Null(BService.Context);
        }

        private sealed class BLogStateScope : IDisposable
        {
            private static readonly PropertyInfo ContextProperty = typeof(BService).GetProperty(
                "Context",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            private readonly UserContext _originalContext;
            private readonly UserContext _testContext;

            private BLogStateScope(UserContext originalContext, UserContext testContext, LogDatabase logDatabase)
            {
                _originalContext = originalContext;
                _testContext = testContext;
                LogDatabase = logDatabase;
            }

            public LogDatabase LogDatabase { get; }

            public static BLogStateScope Create()
            {
                var logDatabase = new LogDatabase(null);
                var service = new UserService("BLogTests");
                typeof(UserService)
                    .GetProperty(nameof(UserService.LogDatabase), BindingFlags.Instance | BindingFlags.Public)
                    .SetValue(service, logDatabase);
                var context = new UserContext("BLogTests", "BLogTests")
                {
                    Service = service
                };
                var scope = new BLogStateScope(GetContext(), context, logDatabase);
                ContextProperty.SetValue(null, context);
                return scope;
            }

            public static BLogStateScope CreateWithoutContext()
            {
                var scope = new BLogStateScope(GetContext(), null, null);
                ContextProperty.SetValue(null, null);
                return scope;
            }

            public void Dispose()
            {
                _testContext?.Dispose();
                ContextProperty.SetValue(null, _originalContext);
            }

            private static UserContext GetContext()
            {
                Assert.NotNull(ContextProperty);
                return (UserContext)ContextProperty.GetValue(null);
            }
        }
    }
}
