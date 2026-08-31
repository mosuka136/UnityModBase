using System;
using Moq;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigGUI.Bindings
{
    // EntryMultipleBinding 在 EntryBinding 的单值适配行为之上透出 IEntryMultiple 的元素数量与两级说明，
    // 供配置绑定工厂在条目实现多元素契约时选用，组合编辑器据此绘制带独立提示的槽位。
    public class EntryMultipleBindingTests
    {
        private class TestConfig
        {
        }

        [Fact]
        public void MultipleView_ExposesCountAndTwoLevelDescriptions()
        {
            // Arrange
            var baseDescription = new Translator("基础说明", "Base");
            var firstDescription = new Translator("元素一", "First");
            var secondDescription = new Translator("元素二", "Second");
            var entryMock = CreateMultipleEntryMock(baseDescription, firstDescription, secondDescription);

            // Act
            var binding = new EntryMultipleBinding(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Same(entryMock.Object, binding.EntryMultiple);
            var multipleBinding = Assert.IsAssignableFrom<IEntryMultipleBinding>(binding);
            Assert.Equal(2, multipleBinding.Count);
            Assert.Same(baseDescription, multipleBinding.BaseDescription);
            Assert.Equal(2, multipleBinding.ValueDescription.Length);
            Assert.Same(firstDescription, multipleBinding.ValueDescription[0]);
            Assert.Same(secondDescription, multipleBinding.ValueDescription[1]);
        }

        [Fact]
        public void Description_ReturnsBaseDescriptionInsteadOfCombinedEntryDescription()
        {
            // Arrange：条目自身说明是带“值1/值2”标签的组合拼接文本；
            // 绑定说明隐藏基类实现改用基础说明，名称提示只保留总述，分元素说明由槽位控件按需展示。
            var combined = new Translator(
                $"基础说明{Environment.NewLine}值1：元素一{Environment.NewLine}值2：元素二",
                $"Base{Environment.NewLine}Value1: First{Environment.NewLine}Value2: Second");
            var baseDescription = new Translator("基础说明", "Base");
            var entryMock = CreateMultipleEntryMock(baseDescription, new Translator(), new Translator());
            entryMock.SetupGet(entry => entry.Description).Returns(combined);

            // Act
            var binding = new EntryMultipleBinding(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Same(baseDescription, binding.Description);
        }

        [Fact]
        public void Constructor_PassesConfigEntryViewToBaseAdapter()
        {
            // Arrange：多元素条目同时实现 IEntryMultiple 与 IConfigEntry，
            // 基类适配器经 IConfigEntry 视图读写键、类型与装箱值。
            var entryMock = CreateMultipleEntryMock(new Translator(), new Translator(), new Translator());
            entryMock.SetupGet(entry => entry.Key).Returns("Pair");
            entryMock.SetupGet(entry => entry.ValueType).Returns(typeof(EntryValue<int, string>));
            entryMock.As<IConfigEntry>().SetupProperty(entry => entry.BoxedValue, new EntryValue<int, string>(1, "a"));

            // Act
            var binding = new EntryMultipleBinding(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Same(entryMock.Object, binding.Entry);
            Assert.Equal("Pair", binding.Key);
            Assert.Equal(typeof(EntryValue<int, string>), binding.ValueType);
            var tuple = Assert.IsType<EntryValue<int, string>>(binding.Value);
            Assert.Equal(1, tuple.Value1);
            Assert.Equal("a", tuple.Value2);
        }

        [Fact]
        public void Constructor_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new EntryMultipleBinding(typeof(TestConfig), null));

            Assert.Equal("entry", exception.ParamName);
        }

        private static Mock<IEntryMultiple> CreateMultipleEntryMock(
            Translator baseDescription,
            Translator firstDescription,
            Translator secondDescription)
        {
            var entryMock = new Mock<IEntryMultiple>();
            // 基类 EntryBinding 以 IConfigEntry 视图适配条目；多元素契约与配置契约由同一实例实现。
            entryMock.As<IConfigEntry>();
            entryMock.SetupGet(entry => entry.Count).Returns(2);
            entryMock.SetupGet(entry => entry.BaseDescription).Returns(baseDescription);
            entryMock.SetupGet(entry => entry.ValueDescription)
                .Returns(new[] { firstDescription, secondDescription });
            return entryMock;
        }
    }
}
