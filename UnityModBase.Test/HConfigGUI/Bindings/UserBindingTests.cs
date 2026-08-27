using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;
using System;
using System.IO;

namespace UnityModBase.Test.HConfigGUI.Bindings
{
    public class GroupBindingCreateRootTests : IDisposable
    {
        private readonly List<string> _registeredUserIds = new List<string>();

        private class TestConfig
        {
            public bool EnableHLog { get; set; }

            public float SetLootDropRatio { get; set; }
        }

        [Fact]
        public void CreateRoot_UserWithoutConfig_ReturnsEmptyRootGroupWithUserIdentity()
        {
            // Arrange
            var context = CreateUser();

            // Act
            var result = GroupBindingFactory.CreateRoot(context);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(context.UserId, result.Key);
            Assert.Equal(context.UserId, result.Name.Chinese);
            Assert.Equal(context.UserId, result.Name.English);
            Assert.Empty(result.Children);
        }

        [Fact]
        public void CreateRoot_UserWithTablesAndEntries_ReturnsGroupsMatchingConfigStructure()
        {
            // Arrange
            var context = CreateUser();
            context.Service.RegisterConfig<TestConfig>(Path.Combine(Path.GetTempPath(), $"UnityModBase.Test.{Guid.NewGuid():N}.cfg"));
            context.Service.Config.SaveOnConfigSet = false;

            var firstTableName = new Translator("表一", "Table One");
            var firstTableDescription = new Translator("第一个表", "First table");
            context.Service.Config.CreateTable("FirstTable", firstTableName, firstTableDescription);
            var firstEntryName = new Translator("条目一", "Entry One");
            var firstEntryDescription = new Translator("第一个条目", "First entry");
            context.Service.Config.Bind("FirstTable", nameof(TestConfig.EnableHLog), false, firstEntryName, firstEntryDescription);
            var secondEntryName = new Translator("条目二", "Entry Two");
            var secondEntryDescription = new Translator("第二个条目", "Second entry");
            context.Service.Config.Bind("FirstTable", nameof(TestConfig.SetLootDropRatio), 1f, secondEntryName, secondEntryDescription);

            var secondTableName = new Translator("表二", "Table Two");
            var secondTableDescription = new Translator("第二个表", "Second table");
            context.Service.Config.CreateTable("SecondTable", secondTableName, secondTableDescription);

            // Act
            var result = GroupBindingFactory.CreateRoot(context);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(context.UserId, result.Key);
            Assert.Equal(2, result.Children.Count);

            var firstTableBinding = Assert.IsType<GroupBinding>(result.Children[0]);
            Assert.Equal("FirstTable", firstTableBinding.Key);
            Assert.Same(firstTableName, firstTableBinding.Name);
            Assert.Same(firstTableDescription, firstTableBinding.Description);
            Assert.Equal(2, firstTableBinding.Children.Count);

            var firstEntryBinding = Assert.IsAssignableFrom<IEntryBinding>(firstTableBinding.Children[0]);
            Assert.Equal("EnableHLog", firstEntryBinding.Key);
            Assert.Same(firstEntryName, firstEntryBinding.Name);
            Assert.Same(firstEntryDescription, firstEntryBinding.Description);
            Assert.Equal(typeof(bool), firstEntryBinding.ValueType);

            var secondEntryBinding = Assert.IsAssignableFrom<IEntryBinding>(firstTableBinding.Children[1]);
            Assert.Equal("SetLootDropRatio", secondEntryBinding.Key);
            Assert.Same(secondEntryName, secondEntryBinding.Name);
            Assert.Same(secondEntryDescription, secondEntryBinding.Description);
            Assert.Equal(typeof(float), secondEntryBinding.ValueType);

            var secondTableBinding = Assert.IsType<GroupBinding>(result.Children[1]);
            Assert.Equal("SecondTable", secondTableBinding.Key);
            Assert.Same(secondTableName, secondTableBinding.Name);
            Assert.Same(secondTableDescription, secondTableBinding.Description);
            Assert.Empty(secondTableBinding.Children);
        }

        public void Dispose()
        {
            foreach (var userId in _registeredUserIds)
                UserManager.RemoveUser(userId);
        }

        private UserContext CreateUser()
        {
            var context = UserManager.Register(
                Guid.NewGuid().ToString("N"),
                new Translator("测试用户", "Test User"));
            _registeredUserIds.Add(context.UserId);
            return context;
        }
    }
}
