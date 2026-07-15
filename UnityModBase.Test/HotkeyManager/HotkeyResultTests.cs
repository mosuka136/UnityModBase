using UnityModBase.HotkeyManager;

namespace UnityModBase.Test.HotkeyManager
{
    public class HotkeyResultTests
    {
        [Fact]
        public void Constructor_Default_InitializesEmptyErrors()
        {
            // Arrange & Act
            var result = new HotkeyResult<string>();

            // Assert
            Assert.NotNull(result.Errors);
            Assert.Empty(result.Errors);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Ok_WithValue_ReturnsSuccessfulResult()
        {
            // Act
            var result = HotkeyResult<int>.Ok(42);

            // Assert
            Assert.Equal(42, result.Value);
            Assert.True(result.Success);
            Assert.Empty(result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void ImplicitValueConversion_ReturnsSuccessfulResult()
        {
            // Act
            HotkeyResult<string> result = "Ctrl+A";

            // Assert
            Assert.Equal("Ctrl+A", result.Value);
            Assert.True(result.Success);
            Assert.Empty(result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithErrorList_ReturnsFailedResult()
        {
            // Arrange
            var errors = new List<string> { "error1", "error2" };

            // Act
            var result = HotkeyResult<int>.Fail(errors);

            // Assert
            Assert.Equal(default(int), result.Value);
            Assert.False(result.Success);
            Assert.NotSame(errors, result.Errors);
            Assert.Equal(errors, result.Errors);

            errors.Add("late mutation");
            Assert.Equal(2, result.Errors.Count);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithParamsErrors_CapturesErrorsInOrderAndIgnoresLaterMutation()
        {
            // Arrange
            var errors = new[] { "error1", "error2" };

            // Act
            var result = HotkeyResult<string>.Fail(errors);
            errors[0] = "mutated";

            // Assert
            Assert.Null(result.Value);
            Assert.False(result.Success);
            Assert.Equal(new[] { "error1", "error2" }, result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithPrimaryAndErrorLists_MergesErrorsInOrderAndIgnoresLaterMutation()
        {
            // Arrange
            var firstGroup = new List<string> { "first", "second" };
            var secondGroup = new List<string> { "third" };

            // Act
            var result = HotkeyResult<int>.Fail("primary", firstGroup, secondGroup);
            firstGroup[0] = "mutated";
            secondGroup.Add("late mutation");

            // Assert
            Assert.Equal(default(int), result.Value);
            Assert.False(result.Success);
            Assert.Equal(new[] { "primary", "first", "second", "third" }, result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void Fail_WithNullErrorList_ReturnsFailedResultWithEmptyErrors()
        {
            // Act
            var result = HotkeyResult<int>.Fail((IReadOnlyList<string>)null);

            // Assert
            Assert.Equal(default(int), result.Value);
            Assert.False(result.Success);
            Assert.Empty(result.Errors);
            AssertReadOnlyErrors(result.Errors);
        }

        [Fact]
        public void ToString_WhenValueIsNull_ReturnsEmptyString()
        {
            // Arrange
            var result = new HotkeyResult<string>();

            // Act
            var text = result.ToString();

            // Assert
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public void ToString_WhenValueHasContent_ReturnsValueString()
        {
            // Arrange
            var result = new HotkeyResult<string>("Ctrl+A", true, new List<string>());

            // Act
            var text = result.ToString();

            // Assert
            Assert.Equal("Ctrl+A", text);
        }

        private static void AssertReadOnlyErrors(IReadOnlyList<string> errors)
        {
            if (errors is ICollection<string> collection)
            {
                Assert.True(collection.IsReadOnly);
                Assert.Throws<NotSupportedException>(() => collection.Add("mutation"));
            }
        }
    }
}
