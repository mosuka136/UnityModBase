using UnityModBase.HLogGUI;
using UnityModBase.HLogSpace;

namespace UnityModBase.Test.HLogGUI
{
    public class EntryListBindingTests
    {
        [Fact]
        public void AddEntry_WhenEntryIsNull_ThrowsArgumentNullExceptionWithoutChangingCollection()
        {
            // Arrange
            var list = new GroupBinding();

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => list.AddEntry(null));

            // Assert
            Assert.Equal("entry", exception.ParamName);
            Assert.Empty(list.SortedGroup);
            Assert.False(list.HasExceptionEntry);
            Assert.False(list.HasRepeatedEntry);
        }

        [Fact]
        public void AddEntry_WhenEntryIsNew_MakesItVisibleInSortedGroup()
        {
            // Arrange
            var list = new GroupBinding();
            var entry = new EntryBinding(CreateEntry(id: 5, message: "new entry"));

            // Act
            list.AddEntry(entry);

            // Assert
            Assert.Same(entry, Assert.Single(list.SortedGroup));
        }

        [Fact]
        public void AddEntry_WhenEntryHasExceptionAndRepeatCount_SetsFlags()
        {
            // Arrange
            var list = new GroupBinding();
            var logEntry = CreateEntry(exception: new InvalidOperationException("boom"));
            logEntry.UpdateRepeat(new DateTime(2026, 6, 13, 2, 0, 0));

            // Act
            list.AddEntry(new EntryBinding(logEntry));

            // Assert
            Assert.True(list.HasExceptionEntry);
            Assert.True(list.HasRepeatedEntry);
        }

        [Fact]
        public void AddEntry_WhenEquivalentUpdatedEntryAlreadyExists_KeepsOriginalBindingAndRefreshesSort()
        {
            // Arrange
            var list = new GroupBinding();
            var original = CreateEntry(id: 1, message: "repeated");
            var other = CreateEntry(id: 2, message: "other");
            var originalBinding = new EntryBinding(original);
            var otherBinding = new EntryBinding(other);
            list.SortOrder = EntryContentType.RepeatCount;
            list.AddEntry(originalBinding);
            list.AddEntry(otherBinding);
            Assert.Equal(new[] { 1, 2 }, list.SortedGroup.Select(entry => entry.Entry.Id));

            // Act
            original.UpdateRepeat(new DateTime(2026, 6, 13, 1, 0, 2));
            list.AddEntry(new EntryBinding(original));

            // Assert
            Assert.Collection(
                list.SortedGroup,
                entry => Assert.Same(otherBinding, entry),
                entry => Assert.Same(originalBinding, entry));
            Assert.Equal("2", originalBinding.RepeatCount);
            Assert.True(list.HasRepeatedEntry);
        }

        [Fact]
        public void Entries_WhenSortOrderIsTimestamp_ReturnsAscendingOrDescendingTimestampOrder()
        {
            // Arrange
            var list = new GroupBinding();
            var middle = new EntryBinding(CreateEntry(id: 1, timestamp: new DateTime(2026, 6, 13, 12, 0, 0), message: "middle"));
            var late = new EntryBinding(CreateEntry(id: 2, timestamp: new DateTime(2026, 6, 13, 13, 0, 0), message: "late"));
            var early = new EntryBinding(CreateEntry(id: 3, timestamp: new DateTime(2026, 6, 13, 11, 0, 0), message: "early"));
            list.AddEntry(middle);
            list.AddEntry(late);
            list.AddEntry(early);

            // Act
            list.SortOrder = EntryContentType.Timestamp;
            var ascending = list.SortedGroup.Select(x => x.Message).ToArray();
            list.IsSortDescending = true;
            var descending = list.SortedGroup.Select(x => x.Message).ToArray();

            // Assert
            Assert.Equal(new[] { "early", "middle", "late" }, ascending);
            Assert.Equal(new[] { "late", "middle", "early" }, descending);
        }

        [Fact]
        public void Entries_WhenSortValuesMatch_UsesEntryIdAsTieBreaker()
        {
            // Arrange
            var lowerId = new EntryBinding(CreateEntry(id: 1, file: "Lower.cs"));
            var higherId = new EntryBinding(CreateEntry(id: 2, file: "Higher.cs"));
            var list = new GroupBinding
            {
                SortOrder = EntryContentType.Message,
            };
            list.AddEntry(higherId);
            list.AddEntry(lowerId);

            // Act
            var result = list.SortedGroup.Select(entry => entry.Entry.Id).ToArray();

            // Assert
            Assert.Equal(new[] { 1, 2 }, result);
        }

        private static LogEntry CreateEntry(
            int id = 1,
            DateTime? timestamp = null,
            int threadId = 2,
            int frame = 3,
            string scene = "Scene",
            LogLevel level = LogLevel.Info,
            string message = "Message",
            string file = @"C:\Code\File.cs",
            int line = 10,
            string member = "Member",
            Exception exception = null)
        {
            return new LogEntry(
                id,
                timestamp ?? new DateTime(2026, 6, 13, 0, 0, 0),
                threadId,
                frame,
                scene,
                level,
                message,
                file,
                line,
                member,
                exception);
        }
    }
}
