using System.Collections;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigSheetTests
    {
        [Fact]
        public void NewSheet_ExposesEmptyViews()
        {
            var sheet = new ConfigSheet();

            Assert.Equal(0, sheet.Count);
            Assert.Empty(sheet.Keys);
            Assert.Empty(sheet.Values);
            Assert.Empty(sheet);
        }

        [Fact]
        public void Add_StoresTablesInInsertionOrderAcrossAllViews()
        {
            var sheet = new ConfigSheet();
            var first = CreateConfigTable("First");
            var second = CreateConfigTable("Second");

            sheet.Add("First", first);
            sheet.Add("Second", second);

            Assert.Equal(2, sheet.Count);
            Assert.True(sheet.Contains("First"));
            Assert.False(sheet.Contains("Missing"));
            Assert.Same(first, sheet["First"]);
            Assert.Equal(new[] { "First", "Second" }, sheet.Keys);
            Assert.Equal(new[] { first, second }, sheet.Values);

            var entries = sheet.ToArray();
            Assert.Equal("First", entries[0].Key);
            Assert.Same(first, entries[0].Value);
            Assert.Equal("Second", entries[1].Key);
            Assert.Same(second, entries[1].Value);
        }

        [Fact]
        public void Add_DuplicateKey_ThrowsArgumentException()
        {
            var sheet = new ConfigSheet();
            sheet.Add("Table", CreateConfigTable("Table"));

            Assert.Throws<ArgumentException>(() => sheet.Add("Table", CreateConfigTable("Other")));
        }

        [Fact]
        public void Add_NullTable_PreservesNullValue()
        {
            var sheet = new ConfigSheet();

            sheet.Add("Table", null);

            Assert.Null(sheet["Table"]);
            Assert.Null(Assert.Single(sheet.Values));
            Assert.Null(Assert.Single(sheet).Value);
        }

        [Fact]
        public void NonGenericEnumerator_ReturnsSameOrderedEntries()
        {
            var sheet = new ConfigSheet();
            var table = CreateConfigTable("Table");
            sheet.Add("Table", table);

            var entries = ((IEnumerable)sheet).Cast<KeyValuePair<string, ConfigTable>>().ToArray();

            var entry = Assert.Single(entries);
            Assert.Equal("Table", entry.Key);
            Assert.Same(table, entry.Value);
        }

        private static ConfigTable CreateConfigTable(string key)
        {
            var translator = new Translator("中文", "English");
            var fileTable = new ConfigFileTable(key, translator);
            return new ConfigTable(key, fileTable, translator, translator);
        }
    }
}
