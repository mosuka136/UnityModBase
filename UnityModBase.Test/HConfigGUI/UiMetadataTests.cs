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

    }
}
