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
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void AddError_WithReadOnlyList_AppendsErrorsInOrder()
        {
            var initial = new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Initial");
            var first = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "First");
            var second = new ConfigFileError(ConfigFileErrorCode.InvalidType, "Second");
            var result = new ConfigFileResult<string>(null, false, new[] { initial });

            result.AddError(new[] { first, second });

            Assert.Equal(new[] { initial, first, second }, result.Errors);
        }

        [Fact]
        public void AddError_WithParamsArray_AppendsErrorsInOrder()
        {
            var initial = new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Initial");
            var first = new ConfigFileError(ConfigFileErrorCode.InvalidValue, "First");
            var second = new ConfigFileError(ConfigFileErrorCode.InvalidType, "Second");
            var result = new ConfigFileResult<string>(null, false, new[] { initial });

            result.AddError(first, second);

            Assert.Equal(new[] { initial, first, second }, result.Errors);
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
