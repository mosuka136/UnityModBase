using System.Collections;
using Moq;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigTableTests
    {
        [Fact]
        public void Constructor_InvalidTableName_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new ConfigTable("invalid-key", new Translator(), new Translator()));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void Constructor_ValidArguments_InitializesTableMetadata()
        {
            var name = new Translator("名称", "Name");
            var description = new Translator("说明", "Description");

            var table = new ConfigTable("Table", name, description);

            Assert.Equal("Table", table.Key);
            Assert.Same(name, table.Name);
            Assert.Same(description, table.Description);
            Assert.Empty(table.Entries);
        }

        [Fact]
        public void Constructor_NullMetadata_UsesEmptyTranslators()
        {
            var table = new ConfigTable("Table", null, null);

            Assert.Equal(string.Empty, table.Name.Chinese);
            Assert.Equal(string.Empty, table.Name.English);
            Assert.Equal(string.Empty, table.Description.Chinese);
            Assert.Equal(string.Empty, table.Description.English);
        }

        [Fact]
        public void Add_NullEntry_ThrowsArgumentNullException()
        {
            var table = CreateTable();

            var exception = Assert.Throws<ArgumentNullException>(() => table.Add(null));

            Assert.Equal("entry", exception.ParamName);
            Assert.Empty(table.Entries);
        }

        [Fact]
        public void Add_WhenEntryBelongsToDifferentTable_ThrowsArgumentException()
        {
            var table = CreateTable();
            var foreignEntry = CreateEntryMock(tableKey: "OtherTable", key: "Entry").Object;

            var exception = Assert.Throws<ArgumentException>(() => table.Add(foreignEntry));

            Assert.Equal("entry", exception.ParamName);
            Assert.Empty(table.Entries);
        }

        [Fact]
        public void Add_WhenEntryAlreadyExists_ThrowsInvalidOperationException()
        {
            var table = CreateTable();
            var entry = CreateEntryMock(tableKey: "Table", key: "Entry").Object;
            table.Add(entry);
            var duplicate = CreateEntryMock(tableKey: "Table", key: "Entry").Object;

            var exception = Assert.Throws<InvalidOperationException>(() => table.Add(duplicate));

            Assert.Contains("Table.Entry", exception.Message);
            Assert.Equal(new[] { entry }, table.Entries);
        }

        [Fact]
        public void Add_EntriesPreserveOrderForGenericAndNonGenericEnumeration()
        {
            var table = CreateTable();
            var first = CreateEntryMock(tableKey: "Table", key: "First").Object;
            var second = CreateEntryMock(tableKey: "Table", key: "Second").Object;

            table.Add(first);
            table.Add(second);

            Assert.Equal(new[] { first, second }, table.Entries);
            Assert.Equal(new[] { first, second }, table.ToArray());
            Assert.Equal(new object[] { first, second }, ((IEnumerable)table).Cast<object>().ToArray());
        }

        [Fact]
        public void Contains_WhenEntryMatchesTableAndKey_ReturnsTrue()
        {
            var table = CreateTable();
            var entry = CreateEntryMock(tableKey: "Table", key: "Entry").Object;
            table.Add(entry);

            var lookup = CreateEntryMock(tableKey: "Table", key: "Entry").Object;

            Assert.True(table.Contains(lookup));
        }

        [Fact]
        public void Contains_WhenKeyIsNotInTable_ReturnsFalse()
        {
            var table = CreateTable();
            table.Add(CreateEntryMock(tableKey: "Table", key: "Entry").Object);

            var lookup = CreateEntryMock(tableKey: "Table", key: "Missing").Object;

            Assert.False(table.Contains(lookup));
        }

        [Fact]
        public void Contains_WhenEntryBelongsToDifferentTable_ReturnsFalse()
        {
            var table = CreateTable();
            table.Add(CreateEntryMock(tableKey: "Table", key: "Entry").Object);

            var lookup = CreateEntryMock(tableKey: "OtherTable", key: "Entry").Object;

            Assert.False(table.Contains(lookup));
        }

        [Fact]
        public void Contains_WhenEntryIsNull_ReturnsFalse()
        {
            var table = CreateTable();

            Assert.False(table.Contains(null));
        }

        private static Mock<IConfigEntry> CreateEntryMock(string tableKey, string key)
        {
            var entryMock = new Mock<IConfigEntry>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.TableKey).Returns(tableKey);
            entryMock.SetupGet(x => x.Key).Returns(key);
            return entryMock;
        }

        private static ConfigTable CreateTable()
        {
            return new ConfigTable("Table", new Translator(), new Translator());
        }
    }
}
