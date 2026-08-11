using System.Linq;
using UnityModBase.HConfigSpace;

namespace UnityModBase.Test.HConfigSpace
{
    // ConfigEntryValue<T1,T2> 是双元素元组的 IConfigEntryValue 适配器，
    // 编码格式为顶层逗号分隔（无外层圆括号），与 ConfigFileModel 的原生元组 (a,b) 格式不同。
    // 以下测试围绕编解码、类型提示、等值判断和无参构造契约展开，
    // 复用 ConfigEntryTests 中已定义的 UnsupportedType 与 StubConfigEntryValue 辅助类型。
    public class ConfigEntryValueTupleTests
    {
        // ---- 构造函数 ----

        [Fact]
        public void ParameterlessConstructor_SetsBothElementsToTypeDefaults()
        {
            // IConfigEntryValue 契约要求无参构造可被 Activator.CreateInstance 调用，
            // 因此验证两个元素都被初始化为对应类型的默认值，而非未定义状态。
            // Arrange
            var value = new ConfigEntryValue<int, string>();

            // Assert
            Assert.Equal(0, value.Value1);
            Assert.Null(value.Value2);
        }

        [Fact]
        public void Constructor_SetsBothElements()
        {
            // Arrange
            var value = new ConfigEntryValue<int, bool>(7, true);

            // Assert
            Assert.Equal(7, value.Value1);
            Assert.True(value.Value2);
        }

        // ---- Encode ----

        [Theory]
        [InlineData(1, 2, "1,2")]
        [InlineData(-5, 99, "-5,99")]
        public void Encode_TwoPrimitives_ReturnsTopLevelCommaSeparatedText(int a, int b, string expected)
        {
            // 与原生元组 (a,b) 不同，本适配器编码不使用外层圆括号，仅按顶层逗号拼接。
            // Arrange
            var value = new ConfigEntryValue<int, int>(a, b);

            // Act
            var result = value.Encode();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void Encode_TwoStrings_QuotesEachElement()
        {
            // Arrange
            var value = new ConfigEntryValue<string, string>("a", "b");

            // Act
            var result = value.Encode();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("\"a\",\"b\"", result.Value);
        }

        [Fact]
        public void Encode_MixedTypes_EncodesEachByDeclaredType()
        {
            // Arrange
            var value = new ConfigEntryValue<int, string>(3, "x");

            // Act
            var result = value.Encode();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("3,\"x\"", result.Value);
        }

        [Fact]
        public void Encode_WhenFirstElementCannotEncode_ReturnsFailure()
        {
            // null 字符串无法编码；Encode 应聚合失败而不抛异常。
            // Arrange
            var value = new ConfigEntryValue<string, int>(null, 1);

            // Act
            var result = value.Encode();

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Encode_WhenSecondElementUnsupported_ReturnsFailure()
        {
            // Arrange
            var value = new ConfigEntryValue<int, UnsupportedType>(1, new UnsupportedType());

            // Act
            var result = value.Encode();

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        // ---- Decode ----

        [Fact]
        public void Decode_ValidCommaSeparatedText_ReturnsInstanceWithDecodedElements()
        {
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("1,2");

            // Assert
            Assert.True(result.Success);
            var decoded = Assert.IsType<ConfigEntryValue<int, int>>(result.Value);
            Assert.Equal(1, decoded.Value1);
            Assert.Equal(2, decoded.Value2);
        }

        [Fact]
        public void Decode_TrimsWhitespaceAroundElements()
        {
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("  1 , 2  ");

            // Assert
            Assert.True(result.Success);
            var decoded = (ConfigEntryValue<int, int>)result.Value;
            Assert.Equal(1, decoded.Value1);
            Assert.Equal(2, decoded.Value2);
        }

        [Fact]
        public void Decode_PreservesCommasInsideQuotedStrings()
        {
            // 引号内的逗号不作为顶层分隔符，验证词法扫描与字符串解码的协作。
            // Arrange
            var adapter = new ConfigEntryValue<string, string>();

            // Act
            var result = adapter.Decode("\"a,b\",\"c\"");

            // Assert
            Assert.True(result.Success);
            var decoded = (ConfigEntryValue<string, string>)result.Value;
            Assert.Equal("a,b", decoded.Value1);
            Assert.Equal("c", decoded.Value2);
        }

        [Fact]
        public void Decode_TooFewElements_ReturnsFailure()
        {
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("1");

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Message.Contains("Expected two values"));
        }

        [Fact]
        public void Decode_TooManyElements_ReturnsFailure()
        {
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("1,2,3");

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Message.Contains("Expected two values"));
        }

        [Fact]
        public void Decode_WhenFirstElementInvalid_ReturnsFailureWithElementError()
        {
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("abc,2");

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Message.Contains("Invalid int value"));
        }

        [Fact]
        public void Decode_WhenBothElementsInvalid_AggregatesErrorsFromBoth()
        {
            // 两个元素都解码失败时，应聚合各自的错误而不是只返回第一个。
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("abc,def");

            // Assert
            Assert.False(result.Success);
            Assert.True(result.Errors.Count >= 2);
        }

        [Fact]
        public void Decode_EmptyContent_ReturnsFailure()
        {
            // Arrange
            var adapter = new ConfigEntryValue<int, int>();

            // Act
            var result = adapter.Decode("");

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        // ---- EncodeValueType ----

        [Fact]
        public void EncodeValueType_TwoPrimitives_ReturnsCommaSeparatedHints()
        {
            // Arrange
            var value = new ConfigEntryValue<int, string>(0, "");

            // Act
            var result = value.EncodeValueType();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int32,String", result.Value);
        }

        [Fact]
        public void EncodeValueType_WhenElementTypeUnsupported_ReturnsFailure()
        {
            // Arrange
            var value = new ConfigEntryValue<UnsupportedType, int>(new UnsupportedType(), 0);

            // Act
            var result = value.EncodeValueType();

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        // ---- Equals ----

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            // Arrange
            var a = new ConfigEntryValue<int, string>(1, "x");
            var b = new ConfigEntryValue<int, string>(1, "x");

            // Assert
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentFirstElement_ReturnsFalse()
        {
            // Arrange
            var a = new ConfigEntryValue<int, string>(1, "x");
            var b = new ConfigEntryValue<int, string>(2, "x");

            // Assert
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentSecondElement_ReturnsFalse()
        {
            // Arrange
            var a = new ConfigEntryValue<int, string>(1, "x");
            var b = new ConfigEntryValue<int, string>(1, "y");

            // Assert
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_NullOther_ReturnsFalse()
        {
            // Arrange
            var a = new ConfigEntryValue<int, int>(1, 2);

            // Assert
            Assert.False(a.Equals(null));
        }

        [Fact]
        public void Equals_DifferentRuntimeType_ReturnsFalse()
        {
            // 不同 IConfigEntryValue 实现之间不应被视为相等。
            // Arrange
            IConfigEntryValue a = new ConfigEntryValue<int, int>(1, 2);
            IConfigEntryValue b = new StubConfigEntryValue("1,2");

            // Assert
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_NestedEquivalentArrays_ReturnsTrue()
        {
            // 元素为数组时按元素深度比较，而非引用比较。
            // Arrange
            var a = new ConfigEntryValue<int[], int>(new[] { 1, 2 }, 0);
            var b = new ConfigEntryValue<int[], int>(new[] { 1, 2 }, 0);

            // Assert
            Assert.True(a.Equals(b));
        }

        // ---- 往返一致性 ----

        [Fact]
        public void RoundTrip_EncodeThenDecode_PreservesValues()
        {
            // Arrange
            var original = new ConfigEntryValue<int, string>(42, "hello");
            var decoder = new ConfigEntryValue<int, string>();

            // Act
            var encodeResult = original.Encode();
            var decoded = decoder.Decode(encodeResult.Value);

            // Assert
            Assert.True(decoded.Success);
            var value = (ConfigEntryValue<int, string>)decoded.Value;
            Assert.Equal(42, value.Value1);
            Assert.Equal("hello", value.Value2);
        }
    }
}
