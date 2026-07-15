using Moq;
using UnityModBase.HLogGUI;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;

namespace UnityModBase.Test.HLogGUI
{
    public class GuiContextTests
    {
        [Fact]
        public void RegisterLogHandlers_WhenDependencyIsNull_ThrowsMatchingArgumentNullException()
        {
            var context = new GuiContext();
            var editor = CreateUserEditor();

            Assert.Equal("logDatabase", Assert.Throws<ArgumentNullException>(() =>
                context.RegisterLogHandlers(null, editor)).ParamName);
            Assert.Equal("userEditor", Assert.Throws<ArgumentNullException>(() =>
                context.RegisterLogHandlers(new LogDatabase(null), null)).ParamName);
        }

        [Fact]
        public void SubscribeToastNotifications_BeforeRegisteringEditor_ThrowsInvalidOperationException()
        {
            var context = new GuiContext();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                context.SubscribeToastNotifications(null));

            Assert.Equal("UserEditor must be registered before subscribing toast notifications.", exception.Message);
        }

        [Fact]
        public void RegisterLogHandlers_WhenLogIsAdded_AddsBindingAndMarksWidthDirty()
        {
            using var database = new LogDatabase(null);
            using var context = new GuiContext
            {
                UserData = new GroupBinding(),
                IsColumnWidthDirty = false,
            };
            context.RegisterLogHandlers(database, CreateUserEditor());
            var log = CreateLog(1);

            database.AddLog(log);

            var binding = Assert.Single(context.UserData.OriginalGroup);
            Assert.Same(log, binding.Entry);
            Assert.True(context.IsColumnWidthDirty);
        }

        [Fact]
        public void RegisterLogHandlers_CalledAgain_DetachesAddedHandlerFromPreviousDatabase()
        {
            using var firstDatabase = new LogDatabase(null);
            using var secondDatabase = new LogDatabase(null);
            using var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            context.RegisterLogHandlers(firstDatabase, CreateUserEditor());
            firstDatabase.AddLog(CreateLog(1));

            context.RegisterLogHandlers(secondDatabase, CreateUserEditor());
            firstDatabase.AddLog(CreateLog(2));
            secondDatabase.AddLog(CreateLog(3));

            Assert.Equal(new[] { 1, 3 }, context.UserData.OriginalGroup.Select(entry => entry.Entry.Id));
        }

        [Fact]
        public void Dispose_WhenCalledTwice_DetachesAddedHandlerAndIsIdempotent()
        {
            using var database = new LogDatabase(null);
            var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            context.RegisterLogHandlers(database, CreateUserEditor());
            database.AddLog(CreateLog(1));

            context.Dispose();
            context.Dispose();
            database.AddLog(CreateLog(2));

            Assert.Equal(new[] { 1 }, context.UserData.OriginalGroup.Select(entry => entry.Entry.Id));
        }

        [Fact]
        public void Dispose_WhenDatabaseLaterRemovesOldestLog_DoesNotMutateDetachedUserData()
        {
            using var database = new LogDatabase(null);
            var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            context.RegisterLogHandlers(database, CreateUserEditor());
            var firstLog = CreateLog(1);
            database.AddLog(firstLog);

            context.Dispose();
            for (var id = 2; id <= LogDatabase.MaxLogCount + 1; id++)
                database.AddLog(CreateLog(id));

            var binding = Assert.Single(context.UserData.OriginalGroup);
            Assert.Same(firstLog, binding.Entry);
        }

        private static UserEditor CreateUserEditor()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            return new UserEditor(
                unityService.Object,
                unityGui.Object,
                new StyleResource(unityGui.Object));
        }

        private static LogEntry CreateLog(int id)
        {
            return new LogEntry(
                id,
                new DateTime(2026, 7, 15, 12, 0, 0).AddSeconds(id),
                3,
                10,
                "Scene",
                LogLevel.Info,
                $"message-{id}",
                "File.cs",
                12,
                "Member",
                null);
        }
    }
}
