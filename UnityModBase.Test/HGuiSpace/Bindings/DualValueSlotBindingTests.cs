using System;
using Moq;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Bindings
{
    /// <summary>
    /// 双元素槽位绑定把父条目的单个元素投影为独立配置项绑定：
    /// 元素写入与另一元素合并为新的整体值，元数据按槽位下标解析，暂存缓冲两槽互相独立。
    /// </summary>
    public class DualValueSlotBindingTests
    {
        [Fact]
        public void Constructor_WhenParentIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new DualValueSlotBinding(null, 0));
            Assert.Equal("parent", exception.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        public void Constructor_WhenSlotIndexIsOutOfRange_ThrowsArgumentOutOfRangeException(int slotIndex)
        {
            // Arrange
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new DualValueSlotBinding(parentMock.Object, slotIndex));
            Assert.Equal("slotIndex", exception.ParamName);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(null)]
        public void Constructor_WhenParentValueTypeIsNullOrNotDualValueGeneric_ThrowsArgumentException(Type valueType)
        {
            // Arrange：值类型缺失或非 EntryValue<,> 泛型的父条目均无法按元素投影。
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(valueType);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(
                () => new DualValueSlotBinding(parentMock.Object, 0));
            Assert.Equal("parent", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenParentValueTypeIsOpenGeneric_ThrowsArgumentException()
        {
            // Arrange：开放泛型定义缺少元素类型参数，无法确定槽位值类型。
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<,>));

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new DualValueSlotBinding(parentMock.Object, 0));
        }

        [Fact]
        public void SlotView_WhenParentProvided_ExposesElementTypesKeysAndDisplayInfo()
        {
            // Arrange
            var name = new Translator("名称", "Name");
            var slot0Description = new Translator("元素一", "First");
            var slot1Description = new Translator("元素二", "Second");
            var sliderMetadata = new UiSliderMetadata(0f, 10f, 1f);
            var metadata = new UiCompositeMetadata(new IUiMetadata[] { null, sliderMetadata });
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"), metadata);
            parentMock.SetupGet(x => x.Name).Returns(name);

            // Act
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0, description: slot0Description);
            var slot1 = new DualValueSlotBinding(parentMock.Object, 1, description: slot1Description);

            // Assert：槽位值类型和说明对应各自元素，名称透传父条目，键彼此不同且不与父键冲突。
            Assert.Equal(typeof(int), slot0.ValueType);
            Assert.Equal(typeof(string), slot1.ValueType);
            Assert.Same(name, slot0.Name);
            Assert.Same(name, slot1.Name);
            Assert.Same(slot0Description, slot0.Description);
            Assert.Same(slot1Description, slot1.Description);
            Assert.Equal("DualEntry", parentMock.Object.Key);
            Assert.NotEqual(parentMock.Object.Key, slot0.Key);
            Assert.NotEqual(slot0.Key, slot1.Key);
            Assert.NotNull(slot0.EditBuffer);
            Assert.NotNull(slot1.EditBuffer);
            Assert.NotSame(slot0.EditBuffer, slot1.EditBuffer);
        }

        [Fact]
        public void Constructor_WhenDescriptionOmitted_UsesBlankDescription()
        {
            // Arrange：省略说明的槽位（如手工构造的投影）退化为空说明，而不是回退父条目组合说明。
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));
            parentMock.SetupGet(x => x.Description).Returns(new Translator("组合说明", "Pair description"));

            // Act
            var slot = new DualValueSlotBinding(parentMock.Object, 0);

            // Assert
            Assert.NotNull(slot.Description);
            Assert.Equal(string.Empty, slot.Description.Chinese);
            Assert.Equal(string.Empty, slot.Description.English);
        }

        [Fact]
        public void Metadata_WhenParentHasComboMetadata_MapsEachSlotByIndex()
        {
            // Arrange：组合元数据只在第二个槽位声明了滑条。
            var sliderMetadata = new UiSliderMetadata(0f, 10f, 1f);
            var parentMock = CreateParentMock(
                new EntryValue<int, string>(1, "a"),
                new UiCompositeMetadata(new IUiMetadata[] { null, sliderMetadata }));

            // Act
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);
            var slot1 = new DualValueSlotBinding(parentMock.Object, 1);

            // Assert
            Assert.Null(slot0.Metadata);
            Assert.Same(sliderMetadata, slot1.Metadata);
        }

        [Fact]
        public void Metadata_WhenParentMetadataIsNullOrNotCombo_ReturnsNullForBothSlots()
        {
            // Arrange：父元数据缺失或不是组合元数据时，两个槽位都按“无元数据”处理。
            var withoutMetadata = CreateParentMock(new EntryValue<int, string>(1, "a"));
            var withSliderMetadata = CreateParentMock(
                new EntryValue<int, string>(1, "a"), new UiSliderMetadata(0f, 10f, 1f));

            // Act & Assert
            Assert.Null(new DualValueSlotBinding(withoutMetadata.Object, 0).Metadata);
            Assert.Null(new DualValueSlotBinding(withoutMetadata.Object, 1).Metadata);
            Assert.Null(new DualValueSlotBinding(withSliderMetadata.Object, 0).Metadata);
            Assert.Null(new DualValueSlotBinding(withSliderMetadata.Object, 1).Metadata);
        }

        [Fact]
        public void Metadata_WhenComboArrayShorterThanSlotCount_MissingSlotReturnsNull()
        {
            // Arrange：组合元数据数组长度由声明决定，可能小于槽位数，越界槽位保持无元数据。
            var parentMock = CreateParentMock(
                new EntryValue<int, string>(1, "a"),
                new UiCompositeMetadata(new IUiMetadata[] { new UiSliderMetadata(0f, 10f, 1f) }));

            // Act
            var slot1 = new DualValueSlotBinding(parentMock.Object, 1);

            // Assert
            Assert.Null(slot1.Metadata);
        }

        [Fact]
        public void Value_Get_WhenParentHasCommittedTuple_ReturnsOwnSlotElement()
        {
            // Arrange
            var parentMock = CreateParentMock(new EntryValue<int, string>(42, "hello"));
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);
            var slot1 = new DualValueSlotBinding(parentMock.Object, 1);

            // Act & Assert
            Assert.Equal(42, slot0.Value);
            Assert.Equal("hello", slot1.Value);
        }

        [Fact]
        public void Value_Get_WhenParentValueIsNull_ReturnsNull()
        {
            // Arrange：父条目值缺失属于异常状态，槽位读取降级为 null 而不抛出。
            var parentMock = CreateParentMock(null);
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);

            // Act & Assert
            Assert.Null(slot0.Value);
        }

        [Fact]
        public void Value_SetFirstSlot_MergesWithCurrentSecondElement()
        {
            // Arrange
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);

            // Act
            slot0.Value = 7;

            // Assert：只替换第一个元素，第二个元素保持父条目当前已提交值。
            var result = Assert.IsType<EntryValue<int, string>>(parentMock.Object.Value);
            Assert.Equal(7, result.Value1);
            Assert.Equal("a", result.Value2);
        }

        [Fact]
        public void Value_SetSecondSlot_MergesWithCurrentFirstElement()
        {
            // Arrange
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));
            var slot1 = new DualValueSlotBinding(parentMock.Object, 1);

            // Act
            slot1.Value = "b";

            // Assert
            var result = Assert.IsType<EntryValue<int, string>>(parentMock.Object.Value);
            Assert.Equal(1, result.Value1);
            Assert.Equal("b", result.Value2);
        }

        [Fact]
        public void Value_SetSequentialSlots_BothChangesPreservedInFinalTuple()
        {
            // Arrange：两槽先后写入互不覆盖，最终整体值同时包含两次修改。
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);
            var slot1 = new DualValueSlotBinding(parentMock.Object, 1);

            // Act
            slot0.Value = 7;
            slot1.Value = "b";

            // Assert
            var result = Assert.IsType<EntryValue<int, string>>(parentMock.Object.Value);
            Assert.Equal(7, result.Value1);
            Assert.Equal("b", result.Value2);
        }

        [Fact]
        public void Value_SetWhenParentValueIsNull_FillsOtherSlotWithNullElement()
        {
            // Arrange
            var parentMock = CreateParentMock(null);
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);

            // Act
            slot0.Value = 7;

            // Assert
            var result = Assert.IsType<EntryValue<int, string>>(parentMock.Object.Value);
            Assert.Equal(7, result.Value1);
            Assert.Null(result.Value2);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-number")]
        public void Value_SetInvalidValue_ThrowsArgumentException(object invalidValue)
        {
            // Arrange：与 EntryBinding.Value 相同的赋值约束——null 或不可赋值类型拒绝写入，不带参数名。
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => slot0.Value = invalidValue);
        }

        [Fact]
        public void Value_SetInvokesOnParentValueWrittenCallback()
        {
            // Arrange
            var parentMock = CreateParentMock(new EntryValue<int, string>(1, "a"));
            var callbackCount = 0;
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0, () => callbackCount++);

            // Act
            slot0.Value = 7;

            // Assert
            Assert.Equal(1, callbackCount);
        }

        /// <summary>
        /// 创建值为 <see cref="EntryValue{T1, T2}"/> 的父条目严格替身；
        /// 值用 SetupProperty 提供可读写的已提交值存储。
        /// </summary>
        private static Mock<IEntryBinding> CreateParentMock(EntryValue<int, string> value, IUiMetadata metadata = null)
        {
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));
            parentMock.SetupProperty(x => x.Value, value);
            parentMock.SetupGet(x => x.Metadata).Returns(metadata);
            parentMock.SetupGet(x => x.Key).Returns("DualEntry");
            return parentMock;
        }
    }
}
