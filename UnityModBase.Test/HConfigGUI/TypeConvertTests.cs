using UnityModBase.HConfigGUI;

namespace UnityModBase.Test.HConfigGUI
{
    public class TypeConvertTests
    {
        [Theory]
        [MemberData(nameof(GetSupportedConversions))]
        public void To_WhenConversionIsSupported_ReturnsTargetType(object value, Type targetType, object expected)
        {
            var result = TypeConvert.To(value, targetType);

            Assert.IsType(targetType, result);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToGeneric_WhenConversionIsSupported_ReturnsConvertedValue()
        {
            var result = TypeConvert.To<int>("42");

            Assert.Equal(42, result);
        }

        [Fact]
        public void To_WhenValueIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => TypeConvert.To(null, typeof(int)));

            Assert.Equal("value", exception.ParamName);
        }

        [Fact]
        public void To_WhenTargetTypeIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => TypeConvert.To("42", null));

            Assert.Equal("targetType", exception.ParamName);
        }

        [Fact]
        public void To_WhenValueAlreadyMatchesTargetType_ReturnsOriginalReference()
        {
            var value = new object();

            var result = TypeConvert.To(value, typeof(object));

            Assert.Same(value, result);
        }

        [Fact]
        public void TryTo_WhenInputIsInvalid_ReturnsFalse()
        {
            var success = TypeConvert.TryTo("invalid", typeof(int), out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryTo_WhenTargetTypeIsUnsupported_ReturnsFalse()
        {
            var success = TypeConvert.TryTo("value", typeof(DateTime), out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryTo_WhenEnumNameIsInvalid_ReturnsFalse()
        {
            var success = TypeConvert.TryTo("NotADay", typeof(DayOfWeek), out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryTo_WhenNumericValueOverflows_ReturnsFalse()
        {
            var success = TypeConvert.TryTo("256", typeof(byte), out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryTo_WhenTargetTypeIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                TypeConvert.TryTo("42", null, out _));

            Assert.Equal("targetType", exception.ParamName);
        }

        [Fact]
        public void TryToGeneric_WhenConversionSucceeds_ReturnsTypedValue()
        {
            var success = TypeConvert.TryTo<int>("42", out var result);

            Assert.True(success);
            Assert.Equal(42, result);
        }

        [Fact]
        public void TryToGeneric_WhenConversionFails_ReturnsDefaultValue()
        {
            var success = TypeConvert.TryTo<int>("invalid", out var result);

            Assert.False(success);
            Assert.Equal(default, result);
        }

        public static IEnumerable<object[]> GetSupportedConversions()
        {
            yield return new object[] { "200", typeof(byte), (byte)200 };
            yield return new object[] { "-100", typeof(sbyte), (sbyte)-100 };
            yield return new object[] { "-32000", typeof(short), (short)-32000 };
            yield return new object[] { "65000", typeof(ushort), (ushort)65000 };
            yield return new object[] { "123456", typeof(int), 123456 };
            yield return new object[] { "123456", typeof(uint), (uint)123456 };
            yield return new object[] { "1234567890123", typeof(long), 1234567890123L };
            yield return new object[] { "1234567890123", typeof(ulong), 1234567890123UL };
            yield return new object[] { "12.5", typeof(float), 12.5f };
            yield return new object[] { "12.5", typeof(double), 12.5d };
            yield return new object[] { "true", typeof(bool), true };
            yield return new object[] { 42, typeof(string), "42" };
            yield return new object[] { "Friday", typeof(DayOfWeek), DayOfWeek.Friday };
            yield return new object[] { 1, typeof(DayOfWeek), DayOfWeek.Monday };
        }
    }
}
