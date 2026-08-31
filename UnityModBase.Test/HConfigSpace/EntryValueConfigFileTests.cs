using System.Linq;
using UnityModBase.HConfigSpace;

namespace UnityModBase.Test.HConfigSpace
{
    // EntryValue<T1,T2> 是配置与控制共享的双元素值，
    // 编码格式为顶层逗号分隔（无外层圆括号），与 ConfigFileModel 的原生元组 (a,b) 格式不同。
    // 以下测试围绕编解码、类型提示、等值判断、多元素属性契约（含任意元素数）展开，
    // 复用 ConfigEntryTests 中已定义的 UnsupportedType 与 StubConfigEntryValue 辅助类型。
    public class EntryValueConfigFileTests
    {
        // ---- 构造函数 ----

        [Fact]
        public void Constructor_SetsBothElements()
        {
            // Arrange
            var value = new EntryValue<int, bool>(7, true);

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
            var value = new EntryValue<int, int>(a, b);

            // Act
            var result = ConfigFileModel.Encode(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void Encode_TwoStrings_QuotesEachElement()
        {
            // Arrange
            var value = new EntryValue<string, string>("a", "b");

            // Act
            var result = ConfigFileModel.Encode(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("\"a\",\"b\"", result.Value);
        }

        [Fact]
        public void Encode_MixedTypes_EncodesEachByDeclaredType()
        {
            // Arrange
            var value = new EntryValue<int, string>(3, "x");

            // Act
            var result = ConfigFileModel.Encode(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("3,\"x\"", result.Value);
        }

        [Fact]
        public void Encode_WhenFirstElementCannotEncode_ReturnsFailure()
        {
            // null 字符串无法编码；Encode 应聚合失败而不抛异常。
            // Arrange
            var value = new EntryValue<string, int>(null, 1);

            // Act
            var result = ConfigFileModel.Encode(value);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Encode_WhenSecondElementUnsupported_ReturnsFailure()
        {
            // Arrange
            var value = new EntryValue<int, UnsupportedType>(1, new UnsupportedType());

            // Act
            var result = ConfigFileModel.Encode(value);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Encode_WhenElementIsNestedEntryValue_ReturnsUnsupportedTypeFailure()
        {
            var value = new EntryValue<EntryValue<int, int>, string>(
                new EntryValue<int, int>(1, 2),
                "value");

            var result = ConfigFileModel.Encode(value);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Code == ConfigFileErrorCode.UnsupportedType);
        }

        // ---- Decode ----

        [Fact]
        public void Decode_ValidCommaSeparatedText_ReturnsInstanceWithDecodedElements()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("1,2");

            // Assert
            Assert.True(result.Success);
            var decoded = Assert.IsType<EntryValue<int, int>>(result.Value);
            Assert.Equal(1, decoded.Value1);
            Assert.Equal(2, decoded.Value2);
        }

        [Fact]
        public void Decode_TrimsWhitespaceAroundElements()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("  1 , 2  ");

            // Assert
            Assert.True(result.Success);
            var decoded = (EntryValue<int, int>)result.Value;
            Assert.Equal(1, decoded.Value1);
            Assert.Equal(2, decoded.Value2);
        }

        [Fact]
        public void Decode_PreservesCommasInsideQuotedStrings()
        {
            // 引号内的逗号不作为顶层分隔符，验证词法扫描与字符串解码的协作。
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<string, string>>("\"a,b\",\"c\"");

            // Assert
            Assert.True(result.Success);
            var decoded = (EntryValue<string, string>)result.Value;
            Assert.Equal("a,b", decoded.Value1);
            Assert.Equal("c", decoded.Value2);
        }

        [Fact]
        public void Decode_TooFewElements_ReturnsFailure()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("1");

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Message.Contains("Expected 2 values"));
        }

        [Fact]
        public void Decode_TooManyElements_ReturnsFailure()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("1,2,3");

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Message.Contains("Expected 2 values"));
        }

        [Fact]
        public void Decode_WhenFirstElementInvalid_ReturnsFailureWithElementError()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("abc,2");

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Message.Contains("Invalid int value"));
        }

        [Fact]
        public void Decode_WhenBothElementsInvalid_AggregatesErrorsFromBoth()
        {
            // 两个元素都解码失败时，应聚合各自的错误而不是只返回第一个。
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("abc,def");

            // Assert
            Assert.False(result.Success);
            Assert.True(result.Errors.Count >= 2);
        }

        [Fact]
        public void Decode_EmptyContent_ReturnsFailure()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.Decode<EntryValue<int, int>>("");

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Decode_WhenElementTypeIsNestedEntryValue_ReturnsUnsupportedTypeFailure()
        {
            var result = ConfigFileModel.Decode<EntryValue<int, EntryValue<int, int>>>("1,2,3");

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Code == ConfigFileErrorCode.UnsupportedType);
        }

        // ---- EncodeValueType ----

        [Fact]
        public void EncodeValueType_TwoPrimitives_ReturnsCommaSeparatedHints()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.EncodeValueType<EntryValue<int, string>>();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int32,String", result.Value);
        }

        [Fact]
        public void EncodeValueType_WhenElementTypeUnsupported_ReturnsFailure()
        {
            // Arrange
            // Act
            var result = ConfigFileModel.EncodeValueType<EntryValue<UnsupportedType, int>>();

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        // ---- 多元素标记与属性契约 ----

        [Fact]
        public void MultipleValueCodec_WhenMarkerTypeHasThreeElements_EncodesFlatTextAndRoundTrips()
        {
            // 编解码按 IEntryMultipleValue 标记与 Value1..ValueN 属性约定工作，不限于内置的双元素 EntryValue。
            // Arrange
            var value = new TripleValue<int, int, string>(1, 2, "mid");

            // Act
            var encoded = ConfigFileModel.Encode(value);
            var typeHint = ConfigFileModel.EncodeValueType<TripleValue<int, int, string>>();
            var decoded = ConfigFileModel.Decode<TripleValue<int, int, string>>("3, 4, \"tail\"");

            // Assert
            Assert.True(encoded.Success);
            Assert.Equal("1,2,\"mid\"", encoded.Value);
            Assert.True(typeHint.Success);
            Assert.Equal("Int32,Int32,String", typeHint.Value);
            Assert.True(decoded.Success);
            var triple = Assert.IsType<TripleValue<int, int, string>>(decoded.Value);
            Assert.Equal(3, triple.Value1);
            Assert.Equal(4, triple.Value2);
            Assert.Equal("tail", triple.Value3);
        }

        [Fact]
        public void MultipleValueCodec_WhenValuePropertyIsMissing_ReturnsUnsupportedTypeFailure()
        {
            // 实现了标记但缺少 Value1..ValueN 可读属性的类型违反反射契约，三个入口统一拒绝。
            // Arrange
            // Act
            var decoded = ConfigFileModel.Decode<NoPropertyMultipleValue<int, int>>("1,2");

            // Assert
            Assert.False(ConfigFileModel.Encode(new NoPropertyMultipleValue<int, int>()).Success);
            Assert.False(decoded.Success);
            Assert.Contains(decoded.Errors, error =>
                error.Code == ConfigFileErrorCode.UnsupportedType && error.Message.Contains("Value1"));
            Assert.False(ConfigFileModel.EncodeValueType<NoPropertyMultipleValue<int, int>>().Success);
        }

        [Fact]
        public void MultipleValueCodec_WhenValuePropertyTypeMismatches_ReturnsUnsupportedTypeFailure()
        {
            // 属性存在但声明类型与泛型参数不一致时，同样按反射契约失败。
            // Arrange
            // Act
            var encoded = ConfigFileModel.Encode(new MismatchedPropertyMultipleValue<int, int>(1, "2"));

            // Assert
            Assert.False(encoded.Success);
            Assert.Contains(encoded.Errors, error =>
                error.Code == ConfigFileErrorCode.UnsupportedType && error.Message.Contains("does not match generic argument"));
            Assert.False(ConfigFileModel.Decode<MismatchedPropertyMultipleValue<int, int>>("1,2").Success);
        }

        [Fact]
        public void Decode_WhenConstructorThrows_ReturnsInvalidValueFailure()
        {
            // 元素均解码成功但构造函数拒绝组合时，应聚合为失败结果而不是向调用方抛异常。
            // Arrange
            // Act
            var decoded = ConfigFileModel.Decode<ThrowingCtorMultipleValue<int, int>>("1,2");

            // Assert
            Assert.False(decoded.Success);
            Assert.Contains(decoded.Errors, error => error.Code == ConfigFileErrorCode.InvalidValue);
        }

        // ---- Equals ----

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            // Arrange
            var a = new EntryValue<int, string>(1, "x");
            var b = new EntryValue<int, string>(1, "x");

            // Assert
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentFirstElement_ReturnsFalse()
        {
            // Arrange
            var a = new EntryValue<int, string>(1, "x");
            var b = new EntryValue<int, string>(2, "x");

            // Assert
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentSecondElement_ReturnsFalse()
        {
            // Arrange
            var a = new EntryValue<int, string>(1, "x");
            var b = new EntryValue<int, string>(1, "y");

            // Assert
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_NullOther_ReturnsFalse()
        {
            // Arrange
            var a = new EntryValue<int, int>(1, 2);

            // Assert
            Assert.False(a.Equals((EntryValue<int, int>)null));
        }

        [Fact]
        public void Equals_DifferentRuntimeType_ReturnsFalse()
        {
            // 不同 IEntryValue 实现之间不应被视为相等。
            // Arrange
            IEntryValue a = new EntryValue<int, int>(1, 2);
            IEntryValue b = new StubConfigEntryValue("1,2");

            // Assert
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_NestedEquivalentArrays_ReturnsTrue()
        {
            // 元素为数组时按元素深度比较，而非引用比较。
            // Arrange
            var a = new EntryValue<int[], int>(new[] { 1, 2 }, 0);
            var b = new EntryValue<int[], int>(new[] { 1, 2 }, 0);

            // Assert
            Assert.True(a.Equals(b));
        }

        // ---- 往返一致性 ----

        [Fact]
        public void RoundTrip_EncodeThenDecode_PreservesValues()
        {
            // Arrange
            var original = new EntryValue<int, string>(42, "hello");
            // Act
            var encodeResult = ConfigFileModel.Encode(original);
            var decoded = ConfigFileModel.Decode<EntryValue<int, string>>(encodeResult.Value);

            // Assert
            Assert.True(decoded.Success);
            var value = (EntryValue<int, string>)decoded.Value;
            Assert.Equal(42, value.Value1);
            Assert.Equal("hello", value.Value2);
        }

        // 遵守 Value1..ValueN 属性与构造约定的三元素条目值，证明共享编解码支持任意元素数。
        private sealed class TripleValue<T1, T2, T3> : IEntryMultipleValue
        {
            public T1 Value1 { get; }

            public T2 Value2 { get; }

            public T3 Value3 { get; }

            public TripleValue(T1 value1, T2 value2, T3 value3)
            {
                Value1 = value1;
                Value2 = value2;
                Value3 = value3;
            }

            public bool Equals(IEntryValue other)
            {
                return other is TripleValue<T1, T2, T3> triple
                    && EqualityComparer<T1>.Default.Equals(Value1, triple.Value1)
                    && EqualityComparer<T2>.Default.Equals(Value2, triple.Value2)
                    && EqualityComparer<T3>.Default.Equals(Value3, triple.Value3);
            }
        }

        // 实现了 IEntryMultipleValue 但违反 Value1..ValueN 属性契约的形状，用于验证编解码入口的契约校验。
        // 等值判断仅用引用相等：本形状只服务于编解码契约校验，不参与业务等值。
        private sealed class NoPropertyMultipleValue<T1, T2> : IEntryMultipleValue
        {
            public bool Equals(IEntryValue other)
            {
                return ReferenceEquals(this, other);
            }
        }

        private sealed class MismatchedPropertyMultipleValue<T1, T2> : IEntryMultipleValue
        {
            public T1 Value1 { get; }

            public string Value2 { get; }

            public MismatchedPropertyMultipleValue(T1 value1, string value2)
            {
                Value1 = value1;
                Value2 = value2;
            }

            public bool Equals(IEntryValue other)
            {
                return ReferenceEquals(this, other);
            }
        }

        private sealed class ThrowingCtorMultipleValue<T1, T2> : IEntryMultipleValue
        {
            public T1 Value1 { get; }

            public T2 Value2 { get; }

            public ThrowingCtorMultipleValue(T1 value1, T2 value2)
            {
                throw new InvalidOperationException("constructor rejected the combination");
            }

            public bool Equals(IEntryValue other)
            {
                return ReferenceEquals(this, other);
            }
        }
    }
}
