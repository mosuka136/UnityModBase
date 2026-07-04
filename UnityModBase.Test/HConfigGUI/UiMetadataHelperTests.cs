using UnityModBase.HClassAttribute;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigSpace;
using Moq;

namespace UnityModBase.Test.HConfigGUI
{
    public class UiMetadataHelperTests
    {
        private class TestConfig
        {
            [ConfigSlider(-1f, 20f, 0.1f)]
            public float LootDropRatio { get; set; }

            public bool EnableDebugMode { get; set; }
        }

        [Fact]
        public void GetMetadata_WhenEntryIsNull_ReturnsNull()
        {
            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(TestConfig), null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetMetadata_WhenEntryKeyHasSliderAttribute_ReturnsSliderMetadata()
        {
            // Arrange
            var entryMock = new Mock<IConfigEntry>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.Key).Returns(nameof(TestConfig.LootDropRatio));

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
        public void GetMetadata_WhenEntryKeyDoesNotHaveSliderAttribute_ReturnsNull()
        {
            // Arrange
            var entryMock = new Mock<IConfigEntry>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.Key).Returns(nameof(TestConfig.EnableDebugMode));

            // Act
            var result = UiMetadataHelper.GetMetadata(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Null(result);
            entryMock.VerifyGet(x => x.Key, Times.Once);
        }
    }
}
