using System.Collections;
using System.Collections.Generic;
using UnityModBase.HConfigSpace;
using UnityModBase.HControlSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HEntrySpace
{
    public class TableSheetBaseTests
    {
        [Fact]
        public void DomainModels_InheritSharedTableAndSheetBases()
        {
            Assert.True(typeof(TableBase<IConfigEntry>).IsAssignableFrom(typeof(ConfigTable)));
            Assert.True(typeof(TableBase<IControlEntry>).IsAssignableFrom(typeof(ControlTable)));
            Assert.True(typeof(SheetBase<string, ConfigTable>).IsAssignableFrom(typeof(ConfigSheet)));
            Assert.True(typeof(SheetBase<string, ControlTable>).IsAssignableFrom(typeof(ControlSheet)));
        }

        [Fact]
        public void TableConstructor_InvalidKey_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new TestTable("invalid-key", new Translator(), new Translator()));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void TableConstructor_NullMetadata_UsesEmptyTranslators()
        {
            var table = new TestTable("Table", null, null);

            Assert.Equal(string.Empty, table.Name.Chinese);
            Assert.Equal(string.Empty, table.Description.English);
            Assert.Empty(table.Entries);
            Assert.Equal(0, table.Count);
        }

        [Fact]
        public void Table_AddContainsAndEnumeration_PreserveDeclarationOrder()
        {
            var table = new TestTable("Table", new Translator(), new Translator());
            var first = new TestEntry("Table", "First");
            var second = new TestEntry("Table", "Second");

            table.AddPublic(first);
            table.AddPublic(second);

            Assert.Equal(2, table.Count);
            Assert.True(table.Contains("First"));
            Assert.False(table.Contains("Missing"));
            Assert.Equal(new[] { first, second }, table.Entries);
            Assert.Equal(new[] { first, second }, table.ToArray());
            Assert.Equal(new object[] { first, second }, ((IEnumerable)table).Cast<object>());
            Assert.Throws<NotSupportedException>(() => ((ICollection<TestEntry>)table.Entries).Add(first));
        }

        [Fact]
        public void Table_AddInvalidEntries_RejectsNullForeignAndDuplicateValues()
        {
            var table = new TestTable("Table", new Translator(), new Translator());
            var entry = new TestEntry("Table", "Entry");
            table.AddPublic(entry);

            Assert.Equal("entry", Assert.Throws<ArgumentNullException>(() => table.AddPublic(null)).ParamName);
            Assert.Equal("entry", Assert.Throws<ArgumentException>(() =>
                table.AddPublic(new TestEntry("Other", "Foreign"))).ParamName);
            Assert.Throws<InvalidOperationException>(() =>
                table.AddPublic(new TestEntry("Table", "Entry")));
            Assert.Same(entry, Assert.Single(table));
        }

        [Fact]
        public void Sheet_AddQueryAndEnumeration_PreserveInsertionOrderAndNullValues()
        {
            var sheet = new TestSheet();
            var first = new TestTable("First", new Translator(), new Translator());

            sheet.AddPublic("First", first);
            sheet.AddPublic("Second", null);

            Assert.Equal(2, sheet.Count);
            Assert.True(sheet.Contains("First"));
            Assert.False(sheet.Contains("Missing"));
            Assert.Same(first, sheet["First"]);
            Assert.Null(sheet["Second"]);
            Assert.Equal(new[] { "First", "Second" }, sheet.Keys);
            Assert.Equal(new TestTable[] { first, null }, sheet.Values);
            Assert.Equal(new[] { "First", "Second" }, sheet.Select(pair => pair.Key));
            Assert.Equal(2, ((IEnumerable)sheet).Cast<KeyValuePair<string, TestTable>>().Count());
        }

        [Fact]
        public void Sheet_DuplicateKeyThrowsAndClearRemovesAllTables()
        {
            var sheet = new TestSheet();
            sheet.AddPublic("Table", new TestTable("Table", new Translator(), new Translator()));

            Assert.Throws<ArgumentException>(() => sheet.AddPublic("Table", null));

            sheet.ClearPublic();

            Assert.Empty(sheet);
            Assert.Empty(sheet.Keys);
            Assert.Empty(sheet.Values);
        }

        private sealed class TestTable : TableBase<TestEntry>
        {
            public TestTable(string key, Translator name, Translator description)
                : base(key, name, description)
            {
            }

            public void AddPublic(TestEntry entry)
            {
                Add(entry);
            }
        }

        private sealed class TestSheet : SheetBase<string, TestTable>
        {
            public void AddPublic(string key, TestTable table)
            {
                Add(key, table);
            }

            public void ClearPublic()
            {
                Clear();
            }
        }

        private sealed class TestEntry : IEntry
        {
            public string TableKey { get; }

            public string Key { get; }

            public Translator Name { get; } = new Translator();

            public Translator Description { get; } = new Translator();

            public Type ValueType => typeof(int);

            public object BoxedValue => 0;

            public TestEntry(string tableKey, string key)
            {
                TableKey = tableKey;
                Key = key;
            }
        }
    }
}
