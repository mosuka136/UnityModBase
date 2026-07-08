using System;
using UnityModBase.HConfigGUI;

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
            Assert.Equal(-1f, context.ResetButtonWidth);
            Assert.True(context.IsEntryLabelWidthDirty);
            Assert.True(context.IsGroupButtonWidthDirty);
            Assert.True(context.IsResetButtonWidthDirty);
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
                IsResetButtonWidthDirty = false,
            };

            context.SetLayoutDirtyFlags();

            Assert.True(context.IsEntryLabelWidthDirty);
            Assert.True(context.IsGroupButtonWidthDirty);
            Assert.True(context.IsResetButtonWidthDirty);
        }

        [Fact]
        public void SetLayoutDirtyFlags_WithExplicitArguments_AppliesEachFlagIndependently()
        {
            var context = new GuiContext();

            context.SetLayoutDirtyFlags(
                entryLabelWidthDirty: false,
                groupButtonWidthDirty: true,
                resetButtonWidthDirty: false);

            Assert.False(context.IsEntryLabelWidthDirty);
            Assert.True(context.IsGroupButtonWidthDirty);
            Assert.False(context.IsResetButtonWidthDirty);
        }
    }
}
