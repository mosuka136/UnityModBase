using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;
using System;
using System.IO;
using System.Linq;

namespace UnityModBase.Test.HConfigGUI.Bindings
{
    public class SheetBindingTests
    {
        private class TestConfig
        {
            public bool EnableHLog { get; set; }

            public float SetLootDropRatio { get; set; }
        }

        [Fact]
        public void CreateSheet_EmptySheet_ReturnsBindingWithNoTables()
        {
            // Arrange
            using var service = CreateService();

            // Act
            var result = new SheetBinding(service);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Sheet);
            Assert.Empty(result.Sheet);
        }

        [Fact]
        public void CreateSheet_SheetWithTablesAndEntries_ReturnsBindingsMatchingSourceStructure()
        {
            // Arrange
            var firstTableName = new Translator("表一", "Table One");
            var firstTableDescription = new Translator("第一个表", "First table");
            using var service = CreateService();
            service.Config.CreateTable("FirstTable", firstTableName, firstTableDescription);
            var firstEntryName = new Translator("条目一", "Entry One");
            var firstEntryDescription = new Translator("第一个条目", "First entry");
            service.Config.Bind("FirstTable", nameof(TestConfig.EnableHLog), false, firstEntryName, firstEntryDescription);
            var secondEntryName = new Translator("条目二", "Entry Two");
            var secondEntryDescription = new Translator("第二个条目", "Second entry");
            service.Config.Bind("FirstTable", nameof(TestConfig.SetLootDropRatio), 1f, secondEntryName, secondEntryDescription);

            var secondTableName = new Translator("表二", "Table Two");
            var secondTableDescription = new Translator("第二个表", "Second table");
            service.Config.CreateTable("SecondTable", secondTableName, secondTableDescription);

            // Act
            var result = new SheetBinding(service);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Sheet);
            Assert.Equal(2, result.Sheet.Count);

            var firstTableBinding = result.Sheet[0];
            Assert.Same(firstTableName, firstTableBinding.Name);
            Assert.Same(firstTableDescription, firstTableBinding.Description);
            var firstEntryBindings = firstTableBinding.Table.ToList();
            Assert.Equal(2, firstEntryBindings.Count);
            Assert.Equal("EnableHLog", firstEntryBindings[0].Key);
            Assert.Same(firstEntryName, firstEntryBindings[0].Name);
            Assert.Same(firstEntryDescription, firstEntryBindings[0].Description);
            Assert.Equal(typeof(bool), firstEntryBindings[0].ValueType);
            Assert.Equal("SetLootDropRatio", firstEntryBindings[1].Key);
            Assert.Same(secondEntryName, firstEntryBindings[1].Name);
            Assert.Same(secondEntryDescription, firstEntryBindings[1].Description);
            Assert.Equal(typeof(float), firstEntryBindings[1].ValueType);

            var secondTableBinding = result.Sheet[1];
            Assert.Same(secondTableName, secondTableBinding.Name);
            Assert.Same(secondTableDescription, secondTableBinding.Description);
            Assert.Empty(secondTableBinding.Table);
        }

        private static UserService CreateService()
        {
            var service = new UserService(Guid.NewGuid().ToString("N"));
            service.RegisterConfig<TestConfig>(Path.Combine(Path.GetTempPath(), $"UnityModBase.Test.{Guid.NewGuid():N}.cfg"));
            service.Config.SaveOnConfigSet = false;
            return service;
        }
    }
}
