using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigGUI
{
    public class GuiContextTests
    {
        [Fact]
        public void Constructor_CreatesValidContextWithIndependentStateObjects()
        {
            var context = new GuiContext();

            Assert.True(context.IsValid);
            Assert.NotSame(GuiContext.InvalidGuiContext, context);
            Assert.NotNull(context.ChangeSink);
            Assert.NotNull(context.Popup);
            Assert.Equal(string.Empty, context.ExpandedEnumKey);
            Assert.Equal(string.Empty, context.SelectedGroupKey);
            Assert.Equal(-1f, context.GroupButtonWidth);
            Assert.Equal(-1f, context.TrailingActionWidth);
            Assert.True(context.IsEntryLabelWidthDirty);
            Assert.True(context.IsGroupButtonWidthDirty);
            Assert.True(context.IsTrailingActionWidthDirty);
        }

        [Fact]
        public void InvalidGuiContext_IsMarkedInvalid()
        {
            Assert.False(GuiContext.InvalidGuiContext.IsValid);
        }

        [Fact]
        public void GetEntryLabelWidth_WhenGroupDoesNotExist_ReturnsAndCachesDefaultValue()
        {
            var context = new GuiContext();

            var initialValue = context.GetEntryLabelWidth("General", 12.5f);
            var cachedValue = context.GetEntryLabelWidth("General", 99f);

            Assert.Equal(12.5f, initialValue);
            Assert.Equal(12.5f, cachedValue);
        }

        [Fact]
        public void SetEntryLabelWidth_StoresWidthsIndependentlyForEachGroup()
        {
            var context = new GuiContext();

            context.SetEntryLabelWidth("General", 12.5f);
            context.SetEntryLabelWidth("Advanced", 24.5f);

            Assert.Equal(12.5f, context.GetEntryLabelWidth("General"));
            Assert.Equal(24.5f, context.GetEntryLabelWidth("Advanced"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetEntryLabelWidth_WhenKeyIsNullOrWhitespace_ThrowsArgumentException(string key)
        {
            var context = new GuiContext();

            var exception = Assert.Throws<ArgumentException>(() => context.SetEntryLabelWidth(key, 12.5f));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void SetLayoutDirtyFlags_WithDefaultArguments_MarksEveryLayoutValueDirty()
        {
            var context = new GuiContext
            {
                IsEntryLabelWidthDirty = false,
                IsGroupButtonWidthDirty = false,
                IsTrailingActionWidthDirty = false,
            };

            context.SetLayoutDirtyFlags();

            Assert.True(context.IsEntryLabelWidthDirty);
            Assert.True(context.IsGroupButtonWidthDirty);
            Assert.True(context.IsTrailingActionWidthDirty);
        }

        [Fact]
        public void SetLayoutDirtyFlags_WithExplicitArguments_AppliesEachFlagIndependently()
        {
            var context = new GuiContext();

            context.SetLayoutDirtyFlags(
                entryLabelWidthDirty: false,
                groupButtonWidthDirty: true,
                trailingActionWidthDirty: false);

            Assert.False(context.IsEntryLabelWidthDirty);
            Assert.True(context.IsGroupButtonWidthDirty);
            Assert.False(context.IsTrailingActionWidthDirty);
        }

        [Fact]
        public void SubscribeToastNotifications_WhenToastEditorIsNull_ThrowsArgumentNullException()
        {
            var context = new GuiContext();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                context.SubscribeToastNotifications(null));

            Assert.Equal("toastEditor", exception.ParamName);
        }

        [Fact]
        public void SubscribeToastNotifications_WhenEntryValueChanges_SetsChangedToast()
        {
            using var context = new GuiContext();
            var toastEditor = CreateToastEditor();
            var name = new Translator("名称", "Name");
            var entry = CreateEntry(name, "old", "new");
            context.SubscribeToastNotifications(toastEditor);

            context.ChangeSink.SetValue(entry.Object, "new");

            Assert.Equal(global::UnityModBase.HGuiSpace.Resource.TranslatorResource.Changed + name, toastEditor.Message);
        }

        [Fact]
        public void SubscribeToastNotifications_WhenEntryValueResets_SetsResetToast()
        {
            using var context = new GuiContext();
            var toastEditor = CreateToastEditor();
            var name = new Translator("名称", "Name");
            var entry = CreateResettableEntry(name);
            context.SubscribeToastNotifications(toastEditor);

            context.ChangeSink.ResetValue(entry.Object);

            Assert.Equal(global::UnityModBase.HGuiSpace.Resource.TranslatorResource.ResetDone + name, toastEditor.Message);
            entry.Verify(x => x.ResetValue(), Times.Once);
        }

        [Fact]
        public void SubscribeToastNotifications_CalledTwice_RoutesEventsOnlyToLatestEditor()
        {
            using var context = new GuiContext();
            var firstEditor = CreateToastEditor();
            var secondEditor = CreateToastEditor();
            var name = new Translator("名称", "Name");
            var entry = CreateEntry(name, "old", "new");
            context.SubscribeToastNotifications(firstEditor);
            context.SubscribeToastNotifications(secondEditor);

            context.ChangeSink.SetValue(entry.Object, "new");

            Assert.Null(firstEditor.Message);
            Assert.Equal(global::UnityModBase.HGuiSpace.Resource.TranslatorResource.Changed + name, secondEditor.Message);
        }

        [Fact]
        public void Dispose_AfterToastSubscription_StopsFutureNotifications()
        {
            var context = new GuiContext();
            var toastEditor = CreateToastEditor();
            var entry = CreateEntry(new Translator("名称", "Name"), "old", "new");
            context.SubscribeToastNotifications(toastEditor);

            context.Dispose();
            context.ChangeSink.SetValue(entry.Object, "new");

            Assert.Null(toastEditor.Message);
        }

        [Fact]
        public void Dispose_WhenDelayedEditExists_CommitsBeforeReleasingContext()
        {
            var context = new GuiContext();
            var entry = CreateEntry(new Translator("名称", "Name"), "old", "new");
            context.ChangeSink.SetValue(entry.Object, "new", delay: 10f);

            context.Dispose();
            context.ChangeSink.FlushValue(10f);

            entry.VerifySet(x => x.Value = "new", Times.Once);
            Assert.False(entry.Object.EditBuffer.IsUsing);
        }

        [Fact]
        public void Dispose_WhenPendingCommitThrows_StillUnsubscribesToastNotifications()
        {
            var context = new GuiContext();
            var toastEditor = CreateToastEditor();
            var failingEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var failingBuffer = new EntryEditBuffer();
            failingEntry.SetupGet(x => x.EditBuffer).Returns(failingBuffer);
            failingEntry.SetupGet(x => x.Value).Returns("old");
            failingEntry
                .SetupSet(x => x.Value = "new")
                .Throws(new InvalidOperationException("write failed"));
            var laterEntry = CreateEntry(new Translator("名称", "Name"), "old", "new");
            context.SubscribeToastNotifications(toastEditor);
            context.ChangeSink.SetValue(failingEntry.Object, "new", delay: 10f);

            context.Dispose();
            context.ChangeSink.SetValue(laterEntry.Object, "new");

            Assert.True(failingBuffer.IsUsing);
            Assert.Null(toastEditor.Message);
            failingEntry.VerifySet(x => x.Value = "new", Times.Once);
            laterEntry.VerifySet(x => x.Value = "new", Times.Once);
        }

        private static ToastEditor CreateToastEditor()
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityService.SetupGet(x => x.RealtimeSinceStartup).Returns(10f);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var style = new Mock<IStyleResource>(MockBehavior.Strict);
            return new ToastEditor(unityService.Object, unityGui.Object, style.Object);
        }

        private static Mock<IEntryBinding> CreateEntry(Translator name, object oldValue, object newValue)
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.SetupGet(x => x.Value).Returns(oldValue);
            entry.SetupSet(x => x.Value = newValue);
            entry.SetupGet(x => x.Name).Returns(name);
            return entry;
        }

        private static Mock<IResettableEntryBinding> CreateResettableEntry(Translator name)
        {
            var entry = new Mock<IResettableEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.Setup(x => x.ResetValue());
            entry.SetupGet(x => x.Name).Returns(name);
            return entry;
        }
    }
}
