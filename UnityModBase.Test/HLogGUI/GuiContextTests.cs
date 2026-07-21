using System.Reflection;
using Moq;
using UnityModBase.HGuiSpace;
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
            using var database = new LogDatabase(null);
            var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            var editor = CreateUserEditor();

            Assert.Equal("logDatabase", Assert.Throws<ArgumentNullException>(() =>
                context.RegisterLogHandlers(null, editor)).ParamName);
            Assert.Equal("userEditor", Assert.Throws<ArgumentNullException>(() =>
                context.RegisterLogHandlers(database, null)).ParamName);
        }

        [Fact]
        public void RegisterLogHandlers_WhenReplacementDependencyIsInvalid_PreservesExistingRegistration()
        {
            using var firstDatabase = new LogDatabase(null);
            using var secondDatabase = new LogDatabase(null);
            using var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            context.RegisterLogHandlers(firstDatabase, CreateUserEditor());

            Assert.Throws<ArgumentNullException>(() =>
                context.RegisterLogHandlers(secondDatabase, null));
            firstDatabase.AddLog(CreateLog(1));
            secondDatabase.AddLog(CreateLog(2));

            Assert.Equal(new[] { 1 }, context.UserData.SortedGroup.Select(entry => entry.Entry.Id));
        }

        [Fact]
        public void RegisterLogHandlers_WhenUserDataIsMissing_ThrowsBeforeSubscribing()
        {
            using var database = new LogDatabase(null);
            var context = new GuiContext();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                context.RegisterLogHandlers(database, CreateUserEditor()));
            database.AddLog(CreateLog(1));

            Assert.Equal("UserData must be assigned before registering log handlers.", exception.Message);
            Assert.Null(context.UserData);
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

            var binding = Assert.Single(context.UserData.SortedGroup);
            Assert.Same(log, binding.Entry);
            Assert.True(context.IsColumnWidthDirty);
        }

        [Fact]
        public void RegisterLogHandlers_WhenDatabaseAlreadyContainsLogs_LoadsConsistentSnapshot()
        {
            using var database = new LogDatabase(null);
            database.AddLog(CreateLog(1));
            using var context = new GuiContext
            {
                UserData = new GroupBinding(),
                IsColumnWidthDirty = false,
            };

            context.RegisterLogHandlers(database, CreateUserEditor());

            Assert.Equal(new[] { 1 }, context.UserData.SortedGroup.Select(entry => entry.Entry.Id));
            Assert.True(context.IsColumnWidthDirty);
        }

        [Fact]
        public void ColumnWidthMeasurement_WhenBackgroundUpdateArrives_DoesNotClearNewDirtyVersion()
        {
            var context = new GuiContext
            {
                IsColumnWidthDirty = false,
            };
            context.IsColumnWidthDirty = true;
            int measuredVersion = context.CaptureColumnWidthVersion();

            context.IsColumnWidthDirty = true;
            context.CompleteColumnWidthMeasurement(measuredVersion);

            Assert.True(context.IsColumnWidthDirty);
            context.CompleteColumnWidthMeasurement(context.CaptureColumnWidthVersion());
            Assert.False(context.IsColumnWidthDirty);
        }

        [Fact]
        public void RegisterLogHandlers_WhenLogIsRepeated_UpdatesExistingBindingWithoutAddingAnother()
        {
            using var database = new LogDatabase(null);
            using var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            context.RegisterLogHandlers(database, CreateUserEditor());
            var original = CreateLog(1, "repeated");

            database.AddLog(original);
            database.AddLog(CreateLog(2, "repeated"));

            var binding = Assert.Single(context.UserData.SortedGroup);
            Assert.Same(original, binding.Entry);
            Assert.Equal("2", binding.RepeatCount);
            Assert.True(binding.IsRepeated);
            Assert.True(context.UserData.HasRepeatedEntry);
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

            Assert.Equal(new[] { 1, 3 }, context.UserData.SortedGroup.Select(entry => entry.Entry.Id));
        }

        [Fact]
        public void RegisterLogHandlers_CalledAgain_MigratesToastSubscriptionsToNewEditor()
        {
            using var firstDatabase = new LogDatabase(null);
            using var secondDatabase = new LogDatabase(null);
            var firstEditor = CreateUserEditor();
            var secondEditor = CreateUserEditor();
            var context = new GuiContext
            {
                UserData = new GroupBinding(),
            };
            context.RegisterLogHandlers(firstDatabase, firstEditor);
            context.SubscribeToastNotifications(new ToastEditor(null, null, null));

            context.RegisterLogHandlers(secondDatabase, secondEditor);

            Assert.Equal(0, GetCopiedHandlerCount(firstEditor));
            Assert.Equal(2, GetCopiedHandlerCount(secondEditor));
            context.Dispose();
            Assert.Equal(0, GetCopiedHandlerCount(secondEditor));
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

            Assert.Equal(new[] { 1 }, context.UserData.SortedGroup.Select(entry => entry.Entry.Id));
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

            var binding = Assert.Single(context.UserData.SortedGroup);
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

        private static int GetCopiedHandlerCount(UserEditor editor)
        {
            var groupHandler = typeof(GroupEditor)
                .GetField(nameof(GroupEditor.OnLogCopied), BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(editor.GroupEditor) as Delegate;
            var entryHandler = typeof(EntryEditor)
                .GetField(nameof(EntryEditor.OnEntryCopied), BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(editor.GroupEditor.EntryEditor) as Delegate;
            return (groupHandler?.GetInvocationList().Length ?? 0) +
                (entryHandler?.GetInvocationList().Length ?? 0);
        }

        private static LogEntry CreateLog(int id, string message = null)
        {
            return new LogEntry(
                id,
                new DateTime(2026, 7, 15, 12, 0, 0).AddSeconds(id),
                3,
                10,
                "Scene",
                LogLevel.Info,
                message ?? $"message-{id}",
                "File.cs",
                12,
                "Member",
                null);
        }
    }
}
