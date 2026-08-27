using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using Moq;

namespace UnityModBase.Test.HGuiSpace
{
    public class UiMetadataHelperTests
    {
        // 滑条元数据按“静态运行时声明 + TableKey/Key 匹配”解析；静态属性的值模拟已绑定的配置项。
        private class TestConfig
        {
            [EntrySlider(-1f, 20f, 0.1f)]
            public static IConfigEntry LootDropRatio { get; } = CreateDeclaredEntry("LootDropRatio").Object;
        }

        private static Mock<IConfigEntry> CreateDeclaredEntry(string key)
        {
            var entryMock = new Mock<IConfigEntry>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.TableKey).Returns("Loot");
            entryMock.SetupGet(x => x.Key).Returns(key);
            return entryMock;
        }

        private static Mock<IConfigEntry> CreateQueryEntry(string tableKey, string key)
        {
            var entryMock = new Mock<IConfigEntry>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.TableKey).Returns(tableKey);
            entryMock.SetupGet(x => x.Key).Returns(key);
            return entryMock;
        }

        [Fact]
        public void GetMetadata_WhenClassTypeIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var entryMock = CreateQueryEntry("Loot", "LootDropRatio");

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                UiMetadataHelper.GetMetadata(null, entryMock.Object));
            Assert.Equal("classType", exception.ParamName);
        }

        [Fact]
        public void GetMetadata_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                UiMetadataHelper.GetMetadata(typeof(TestConfig), null));
            Assert.Equal("entry", exception.ParamName);
        }

        [Fact]
        public void GetMetadata_WhenEntryMatchesSliderDeclaration_ReturnsSliderMetadata()
        {
            // Arrange
            var entryMock = CreateQueryEntry("Loot", "LootDropRatio");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(TestConfig), entryMock.Object);

            // Assert
            var metadata = Assert.IsType<UiSliderMetadata>(result);
            Assert.Equal(-1f, metadata.Min);
            Assert.Equal(20f, metadata.Max);
            Assert.Equal(0.1f, metadata.Step);
            entryMock.VerifyGet(x => x.Key, Times.Once);
        }

        [Fact]
        public void GetMetadata_WhenEntryKeyDoesNotMatchAnySliderDeclaration_ReturnsNull()
        {
            // Arrange：表键一致但配置键不同，说明匹配确实进行过却未命中。
            var entryMock = CreateQueryEntry("Loot", "EnableDebugMode");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Null(result);
            entryMock.VerifyGet(x => x.Key, Times.Once);
        }

        [Fact]
        public void GetMetadata_WhenEntryTableKeyDoesNotMatchAnySliderDeclaration_ReturnsNull()
        {
            // Arrange：表键不一致时短路比较，不应读取配置键。
            var entryMock = CreateQueryEntry("OtherTable", "LootDropRatio");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Null(result);
            entryMock.VerifyGet(x => x.Key, Times.Never);
        }

        // 组合声明：EntryGui 指定槽位数量，多个带索引的滑条分别占据各自槽位。
        private class CompositeConfig
        {
            [EntryGui(2)]
            [EntrySlider(0, 0f, 100f, 1f)]
            [EntrySlider(1, 10f, 50f, 0.5f)]
            public static IConfigEntry PairedEntry { get; } = CreateDeclaredEntry("PairedEntry").Object;
        }

        private class PartialCompositeConfig
        {
            [EntryGui(2)]
            [EntrySlider(1, 10f, 50f, 0.5f)]
            public static IConfigEntry SparseEntry { get; } = CreateDeclaredEntry("SparseEntry").Object;
        }

        private class UnindexedCompositeConfig
        {
            [EntryGui(1)]
            [EntrySlider(0f, 100f, 1f)]
            public static IConfigEntry UnindexedEntry { get; } = CreateDeclaredEntry("UnindexedEntry").Object;
        }

        private class OutOfRangeCompositeConfig
        {
            [EntryGui(1)]
            [EntrySlider(5, 0f, 100f, 1f)]
            public static IConfigEntry OutOfRangeEntry { get; } = CreateDeclaredEntry("OutOfRangeEntry").Object;
        }

        private class NegativeIndexCompositeConfig
        {
            [EntryGui(1)]
            [EntrySlider(-5, 0f, 100f, 1f)]
            public static IConfigEntry NegativeIndexEntry { get; } = CreateDeclaredEntry("NegativeIndexEntry").Object;
        }

        private class ThrowingDeclarationConfig
        {
            public static IConfigEntry ThrowingEntry => throw new InvalidOperationException("declaration not ready");
        }

        [Fact]
        public void GetMetadata_WhenDeclarationCombinesEntryGuiWithIndexedSliders_ReturnsCompositeWithFilledSlots()
        {
            // Arrange
            var entryMock = CreateQueryEntry("Loot", "PairedEntry");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(CompositeConfig), entryMock.Object);

            // Assert
            var composite = Assert.IsType<UiCompositeMetadata>(result);
            Assert.Equal(2, composite.Metadatas.Length);
            var firstSlot = Assert.IsType<UiSliderMetadata>(composite.Metadatas[0]);
            Assert.Equal(0f, firstSlot.Min);
            Assert.Equal(100f, firstSlot.Max);
            Assert.Equal(1f, firstSlot.Step);
            var secondSlot = Assert.IsType<UiSliderMetadata>(composite.Metadatas[1]);
            Assert.Equal(10f, secondSlot.Min);
            Assert.Equal(50f, secondSlot.Max);
            Assert.Equal(0.5f, secondSlot.Step);
        }

        [Fact]
        public void GetMetadata_WhenCompositeSlotHasNoDeclaration_KeepsNullSlot()
        {
            // Arrange：组合长度为 2 但只有槽位 1 有声明，槽位 0 应保留为 null 供消费方容忍。
            var entryMock = CreateQueryEntry("Loot", "SparseEntry");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(PartialCompositeConfig), entryMock.Object);

            // Assert
            var composite = Assert.IsType<UiCompositeMetadata>(result);
            Assert.Equal(2, composite.Metadatas.Length);
            Assert.Null(composite.Metadatas[0]);
            var filledSlot = Assert.IsType<UiSliderMetadata>(composite.Metadatas[1]);
            Assert.Equal(10f, filledSlot.Min);
            Assert.Equal(50f, filledSlot.Max);
        }

        [Fact]
        public void GetMetadata_WhenSliderInsideCompositeHasNoIndex_SkipsThatSlider()
        {
            // Arrange：组合内的滑条未指定槽位下标（Index 为 -1 哨兵），应被跳过而不是默认占据槽位 0。
            var entryMock = CreateQueryEntry("Loot", "UnindexedEntry");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(UnindexedCompositeConfig), entryMock.Object);

            // Assert
            var composite = Assert.IsType<UiCompositeMetadata>(result);
            Assert.Single(composite.Metadatas);
            Assert.Null(composite.Metadatas[0]);
        }

        [Fact]
        public void GetMetadata_WhenSliderIndexIsNegative_TreatsAsUnspecifiedAndSkips()
        {
            // Arrange：带索引构造函数不校验下标，负值等效于未指定的 -1 哨兵，展开时跳过且不触发越界异常。
            var entryMock = CreateQueryEntry("Loot", "NegativeIndexEntry");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(NegativeIndexCompositeConfig), entryMock.Object);

            // Assert
            var composite = Assert.IsType<UiCompositeMetadata>(result);
            Assert.Single(composite.Metadatas);
            Assert.Null(composite.Metadatas[0]);
        }

        [Fact]
        public void GetMetadata_WhenSliderIndexExceedsCompositeCount_ReturnsNull()
        {
            // Arrange：越界槽位下标触发解析异常，整体降级为“无元数据”而不是抛出。
            var entryMock = CreateQueryEntry("Loot", "OutOfRangeEntry");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(OutOfRangeCompositeConfig), entryMock.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetMetadata_WhenDeclarationValueReadingThrows_ReturnsNull()
        {
            // Arrange：静态声明取值抛出的异常应降级为“无元数据”，避免单个声明阻断整个可编辑界面。
            var entryMock = CreateQueryEntry("Loot", "ThrowingEntry");

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(ThrowingDeclarationConfig), entryMock.Object);

            // Assert
            Assert.Null(result);
        }
    }
}
