using System;
using UnityModBase.HClassAttribute;

namespace UnityModBase.Test.HClassAttribute
{
    public class ConfigGuiAttributeTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void Constructor_WithPositiveCount_StoresCount(int count)
        {
            // Act
            var attribute = new EntryGuiAttribute(count);

            // Assert
            Assert.Equal(count, attribute.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_WhenCountIsNotPositive_ThrowsArgumentOutOfRangeException(int count)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new EntryGuiAttribute(count));
            Assert.Equal("count", exception.ParamName);
        }

        [Fact]
        public void AttributeUsage_TargetsPropertiesAndDoesNotAllowMultiple()
        {
            // Arrange：组合声明只需一个 ConfigGuiAttribute 指明槽位数量，重复标记没有语义。
            // Act
            var usage = Attribute.GetCustomAttribute(
                typeof(EntryGuiAttribute),
                typeof(AttributeUsageAttribute)) as AttributeUsageAttribute;

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Property));
            Assert.True(usage.Inherited);
            Assert.False(usage.AllowMultiple);
        }
    }
}
