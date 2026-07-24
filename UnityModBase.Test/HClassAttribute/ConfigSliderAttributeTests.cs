using UnityModBase.HClassAttribute;

namespace UnityModBase.Test.HClassAttribute
{
    public class ConfigSliderAttributeTests
    {
        [Fact]
        public void Constructor_WithExplicitStep_StoresRangeAndStep()
        {
            // Act
            var attribute = new ConfigSliderAttribute(-1f, 10f, 0.5f);

            // Assert
            Assert.Equal(-1f, attribute.Min);
            Assert.Equal(10f, attribute.Max);
            Assert.Equal(0.5f, attribute.Step);
        }

        [Fact]
        public void Constructor_WithoutStep_UsesNegativeStepSentinel()
        {
            // Act
            var attribute = new ConfigSliderAttribute(0f, 100f);

            // Assert
            Assert.Equal(0f, attribute.Min);
            Assert.Equal(100f, attribute.Max);
            Assert.Equal(-1f, attribute.Step);
        }

        [Theory]
        [MemberData(nameof(GetNonFiniteArguments))]
        public void Constructor_WhenAnyArgumentIsNotFinite_ThrowsArgumentException(
            float min,
            float max,
            float step)
        {
            Assert.Throws<ArgumentException>(() => new ConfigSliderAttribute(min, max, step));
        }

        [Fact]
        public void Constructor_WhenMinimumExceedsMaximum_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ConfigSliderAttribute(10f, -1f, 0.5f));
        }

        [Fact]
        public void Constructor_WithEqualEndpointsAndZeroStep_StoresValues()
        {
            var attribute = new ConfigSliderAttribute(5f, 5f, 0f);

            Assert.Equal(5f, attribute.Min);
            Assert.Equal(5f, attribute.Max);
            Assert.Equal(0f, attribute.Step);
        }

        [Fact]
        public void AttributeUsage_TargetsPropertiesAndDoesNotAllowMultiple()
        {
            // Act
            var usage = Attribute.GetCustomAttribute(
                typeof(ConfigSliderAttribute),
                typeof(AttributeUsageAttribute)) as AttributeUsageAttribute;

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Property));
            Assert.True(usage.Inherited);
            Assert.False(usage.AllowMultiple);
        }

        public static IEnumerable<object[]> GetNonFiniteArguments()
        {
            foreach (var nonFiniteValue in new[]
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity
            })
            {
                yield return new object[] { nonFiniteValue, 10f, 1f };
                yield return new object[] { 0f, nonFiniteValue, 1f };
                yield return new object[] { 0f, 10f, nonFiniteValue };
            }
        }
    }
}
