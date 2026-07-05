using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.Test.HConfigGUI
{
    public class EntryEditBufferTests
    {
        [Fact]
        public void SetValue_WhenMultipleSourcesExist_ReturnsLatestEntry()
        {
            var buffer = new EntryEditBuffer();

            buffer.SetValue("text", "first", true);
            buffer.SetValue("second", true);

            var result = buffer.GetLatestValue();

            Assert.Equal("second", result.Value);
            Assert.True(result.IsValid);
            Assert.True(buffer.IsUsing);
        }

        [Fact]
        public void Commit_WhenLatestEntryIsValid_UpdatesEntryAndClearsBuffer()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry("old");
            buffer.SetValue("new", true);

            var result = buffer.Commit(entry.Object);

            Assert.True(result);
            Assert.Equal("new", entry.Object.Value);
            Assert.False(buffer.IsUsing);
            Assert.True(buffer.GetLatestValue().IsEmpty);
        }

        [Fact]
        public void Commit_WhenLatestEntryIsInvalid_DiscardsChangesAndClearsBuffer()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(10);
            buffer.SetValue(20, true);
            buffer.SetValue("invalid", false);

            var result = buffer.Commit(entry.Object);

            Assert.False(result);
            Assert.Equal(10, entry.Object.Value);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void Commit_WhenTransformedValueMatchesCurrentValue_DoesNotSetEntry()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(10);
            buffer.SetValue("10", true);

            var result = buffer.Commit(entry.Object, value => int.Parse((string)value));

            Assert.False(result);
            entry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void Clear_WhenBufferContainsValues_ResetsAllState()
        {
            var buffer = new EntryEditBuffer();
            buffer.SetValue("field", 42, true);
            buffer.SetValue(99, true);

            buffer.Clear();

            Assert.False(buffer.IsUsing);
            Assert.Equal(0, buffer.Seq);
            Assert.True(buffer.Value.IsEmpty);
            Assert.True(buffer.GetValue("field").IsEmpty);
        }

        private static Mock<IEntryBinding> CreateEntry(object value)
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupProperty(x => x.Value, value);
            return entry;
        }
    }
}
