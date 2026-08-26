using System;
using UnityModBase.HConfigGUI;

namespace UnityModBase.Test.HConfigGUI
{
    public class UiMetadataTests
    {
        [Fact]
        public void MetadataType_WhenAccessed_ReturnsUiSliderMetadataType()
        {
            // Arrange
            var metadata = new UiSliderMetadata(0f, 1f, 0.5f);

            // Act
            var result = metadata.MetadataType;

            // Assert
            Assert.Equal(typeof(UiSliderMetadata), result);
        }

        [Fact]
        public void UiSliderMetadata_WhenConstructed_SetsMinMaxAndStepProperties()
        {
            // Arrange
            const float min = -10.5f;
            const float max = 20.25f;
            const float step = 0.75f;

            // Act
            var metadata = new UiSliderMetadata(min, max, step);

            // Assert
            Assert.Equal(min, metadata.Min);
            Assert.Equal(max, metadata.Max);
            Assert.Equal(step, metadata.Step);
        }

        [Fact]
        public void UiConfigMetadata_WhenAccessed_ReturnsUiConfigMetadataType()
        {
            // Arrange
            var metadata = new UiConfigMetadata(new IUiMetadata[] { new UiSliderMetadata(0f, 1f, 0.5f) });

            // Act
            var result = metadata.MetadataType;

            // Assert
            Assert.Equal(typeof(UiConfigMetadata), result);
        }

        [Fact]
        public void UiConfigMetadata_WhenConstructed_PreservesMetadataArrayReference()
        {
            // Arrange：组合元数据只承载聚合结果，应原样保留传入数组（含未识别声明的 null 槽位）。
            var metadatas = new IUiMetadata[] { new UiSliderMetadata(0f, 1f, 0.5f), null };

            // Act
            var metadata = new UiConfigMetadata(metadatas);

            // Assert
            Assert.Same(metadatas, metadata.Metadatas);
        }

    }
}
