using UnityModBase.HConfigSpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigFileResultTests
    {
        [Fact]
        public void Constructor_Default_InitializesFailedResultWithEmptyErrors()
        {
            var result = new ConfigFileResult<string>();

            Assert.Null(result.Value);
            Assert.False(result.Success);
            Assert.False(result.HasErrors);
            Assert.Empty(result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Ok_WithValue_ReturnsSuccessfulResult()
        {
            var result = ConfigFileResult<int>.Ok(42);

            Assert.Equal(42, result.Value);
            Assert.True(result.Success);
            Assert.False(result.HasErrors);
            Assert.Empty(result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Constructor_WithMutableErrors_CapturesReadOnlyCopy()
        {
            var error = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Initial");
            var errors = new List<ConfigFileError> { error };

            var result = new ConfigFileResult<string>(null, false, errors);
            errors.Clear();

            Assert.NotSame(errors, result.Errors);
            Assert.Equal(new[] { error }, result.Errors);
            Assert.True(result.HasErrors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithMutableErrorList_CapturesReadOnlyCopy()
        {
            var error = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Initial");
            var errors = new List<ConfigFileError> { error };

            var result = ConfigFileResult<string>.Fail((IReadOnlyList<ConfigFileError>)errors);
            errors.Clear();

            Assert.Null(result.Value);
            Assert.False(result.Success);
            Assert.True(result.HasErrors);
            Assert.Equal(new[] { error }, result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithParamsArray_CapturesErrorsInOrderAndIgnoresLaterMutation()
        {
            var first = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "First");
            var second = new ConfigFileError(ConfigFileErrorCode.InvalidType, "Second");
            var replacement = new ConfigFileError(ConfigFileErrorCode.TableNotFound, "Replacement");
            var errors = new[] { first, second };

            var result = ConfigFileResult<int>.Fail(errors);
            errors[0] = replacement;

            Assert.Equal(default(int), result.Value);
            Assert.False(result.Success);
            Assert.Equal(new[] { first, second }, result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithNullErrorList_ReturnsFailedResultWithEmptyErrors()
        {
            var result = ConfigFileResult<string>.Fail((IReadOnlyList<ConfigFileError>)null);

            Assert.Null(result.Value);
            Assert.False(result.Success);
            Assert.False(result.HasErrors);
            Assert.Empty(result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void AddError_WithReadOnlyList_AppendsErrorsInOrder()
        {
            var initial = new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Initial");
            var first = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "First");
            var second = new ConfigFileError(ConfigFileErrorCode.InvalidType, "Second");
            var result = new ConfigFileResult<string>(null, false, new[] { initial });
            var errors = new List<ConfigFileError> { first, second };

            result.AddError((IReadOnlyList<ConfigFileError>)errors);
            errors.Clear();

            Assert.Equal(new[] { initial, first, second }, result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void AddError_WithParamsArray_AppendsErrorsInOrder()
        {
            var initial = new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Initial");
            var first = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "First");
            var second = new ConfigFileError(ConfigFileErrorCode.InvalidType, "Second");
            var replacement = new ConfigFileError(ConfigFileErrorCode.TableNotFound, "Replacement");
            var result = new ConfigFileResult<string>(null, false, new[] { initial });
            var errors = new[] { first, second };

            result.AddError(errors);
            errors[0] = replacement;

            Assert.Equal(new[] { initial, first, second }, result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void SetValue_UpdatesValueAndSuccessWhilePreservingErrors()
        {
            var error = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Error");
            var result = new ConfigFileResult<int>(0, false, new[] { error });

            result.SetValue(42);

            Assert.Equal(42, result.Value);
            Assert.True(result.Success);
            Assert.Equal(new[] { error }, result.Errors);
        }

        [Fact]
        public void SetValue_NullValue_IsStillSuccessful()
        {
            var result = new ConfigFileResult<string>("initial", false, null);

            result.SetValue(null);

            Assert.Null(result.Value);
            Assert.True(result.Success);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ImplicitValueConversion_ReturnsSuccessfulResult()
        {
            ConfigFileResult<string> result = "value";

            Assert.Equal("value", result.Value);
            Assert.True(result.Success);
            Assert.False(result.HasErrors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void ExplicitValueConversion_WhenSuccessful_ReturnsValue()
        {
            var result = ConfigFileResult<int>.Ok(42);

            var value = (int)result;

            Assert.Equal(42, value);
        }

        [Fact]
        public void ExplicitValueConversion_WhenFailed_ThrowsInvalidOperationException()
        {
            var result = ConfigFileResult<int>.Fail(
                new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Invalid"));

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = (int)result;
            });

            Assert.Equal("Cannot convert a failed ConfigFileResult to its value.", exception.Message);
        }

        [Fact]
        public void ImplicitObjectResultConversion_CapturesIndependentReadOnlyErrorSnapshot()
        {
            var first = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "First");
            var second = new ConfigFileError(ConfigFileErrorCode.InvalidType, "Second");
            var source = ConfigFileResult<string>.Fail(first);

            ConfigFileResult<object> result = source;
            source.AddError(second);

            Assert.Null(result.Value);
            Assert.False(result.Success);
            Assert.Equal(new[] { first }, result.Errors);
            Assert.Equal(new[] { first, second }, source.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        private static void AssertReadOnlyErrors(IReadOnlyList<ConfigFileError> errors)
        {
            if (errors is ICollection<ConfigFileError> collection)
            {
                Assert.True(collection.IsReadOnly);
                Assert.Throws<NotSupportedException>(() => collection.Add(
                    new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Mutation")));
            }
        }
    }

    public class ConfigFileErrorTests
    {
        [Theory]
        [InlineData(ConfigFileErrorCode.InvalidValue, "Test message", "TestMethod", "[InvalidValue] Test message (Caller: TestMethod)")]
        [InlineData(ConfigFileErrorCode.TableNotFound, "Table is missing", "", "[TableNotFound] Table is missing (Caller: )")]
        public void FormattingMethods_ReturnExpectedMessage(
            ConfigFileErrorCode code,
            string message,
            string caller,
            string expected)
        {
            var error = new ConfigFileError(code, message, caller);

            Assert.Equal(expected, error.ToString());
            Assert.Equal(expected, error.GetFullMessage());
        }
    }
}
