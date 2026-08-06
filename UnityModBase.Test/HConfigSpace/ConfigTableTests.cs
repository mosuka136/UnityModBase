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
            var fileTable = new ConfigFileTable("Table", new Translator());

            var exception = Assert.Throws<ArgumentException>(() =>
                new ConfigTable("invalid-key", fileTable, new Translator(), new Translator()));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void Constructor_ValidArguments_InitializesTableAndSynchronizesMetadata()
        {
            var fileTable = new ConfigFileTable("Table", new Translator());
            var name = new Translator("名称", "Name");
            var description = new Translator("说明", "Description");

            var table = new ConfigTable("Table", fileTable, name, description);

            Assert.Equal("Table", table.Key);
            Assert.Same(name, table.Name);
            Assert.Same(description, table.Description);
            Assert.Empty(table.Table);
            Assert.Same(name, fileTable.Name);
            Assert.Same(description, fileTable.Description);
        }

        [Fact]
        public void Constructor_NullMetadata_UsesEmptyTranslators()
        {
            var fileTable = new ConfigFileTable("Table", new Translator());

            var table = new ConfigTable("Table", fileTable, null, null);

            Assert.Equal(string.Empty, table.Name.Chinese);
            Assert.Equal(string.Empty, table.Name.English);
            Assert.Equal(string.Empty, table.Description.Chinese);
            Assert.Equal(string.Empty, table.Description.English);
            Assert.Same(table.Name, fileTable.Name);
            Assert.Same(table.Description, fileTable.Description);
        }

        [Fact]
        public void Add_NullEntry_IsIgnored()
        {
            var table = CreateTable();

            table.Add(null);

            Assert.Empty(table.Table);
        }

        [Fact]
        public void Add_EntriesPreserveOrderForGenericAndNonGenericEnumeration()
        {
            var table = CreateTable();
            var first = new Mock<IConfigEntry>().Object;
            var second = new Mock<IConfigEntry>().Object;

            table.Add(first);
            table.Add(second);

            Assert.Equal(new[] { first, second }, table.Table);
            Assert.Equal(new[] { first, second }, table.ToArray());
            Assert.Equal(new object[] { first, second }, ((IEnumerable)table).Cast<object>().ToArray());
        }

        private static ConfigTable CreateTable()
        {
            var fileTable = new ConfigFileTable("Table", new Translator());
            return new ConfigTable("Table", fileTable, new Translator(), new Translator());
        }
    }
}
