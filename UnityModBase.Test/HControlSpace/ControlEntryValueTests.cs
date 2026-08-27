using UnityModBase.HControlSpace;

namespace UnityModBase.Test.HControlSpace
{
    public class ControlEntryValueTests
    {
        [Fact]
        public void Constructor_StoresBothValues()
        {
            var value = new ControlEntryValue<int, string>(7, "north");

            Assert.Equal(7, value.Value1);
            Assert.Equal("north", value.Value2);
        }

        [Fact]
        public void Equals_WhenBothElementsMatch_ReturnsTrueAndProducesSameHashCode()
        {
            var first = new ControlEntryValue<int, string>(7, "north");
            var second = new ControlEntryValue<int, string>(7, "north");

            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object)second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Theory]
        [InlineData(8, "north")]
        [InlineData(7, "south")]
        public void Equals_WhenEitherElementDiffers_ReturnsFalse(int value1, string value2)
        {
            var value = new ControlEntryValue<int, string>(7, "north");

            Assert.False(value.Equals(new ControlEntryValue<int, string>(value1, value2)));
        }
    }
}
