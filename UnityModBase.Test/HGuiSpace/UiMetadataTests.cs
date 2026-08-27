using System;
using UnityModBase.HGuiSpace;

namespace UnityModBase.Test.HGuiSpace
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
        public void UiCompositeMetadata_WhenAccessed_ReturnsUiCompositeMetadataType()
        {
            // Arrange
            var metadata = new UiCompositeMetadata(new IUiMetadata[] { new UiSliderMetadata(0f, 1f, 0.5f) });

            // Act
            var result = metadata.MetadataType;

            // Assert
            Assert.Equal(typeof(UiCompositeMetadata), result);
        }

        [Fact]
        public void UiCompositeMetadata_WhenConstructed_PreservesMetadataArrayReference()
        {
            // Arrange：组合元数据只承载聚合结果，应原样保留传入数组（含未识别声明的 null 槽位）。
            var metadatas = new IUiMetadata[] { new UiSliderMetadata(0f, 1f, 0.5f), null };

            // Act
            var metadata = new UiCompositeMetadata(metadatas);

            // Assert
            Assert.Same(metadatas, metadata.Metadatas);
        }

    }
}
