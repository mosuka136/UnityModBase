using Moq;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.Test.HGuiSpace
{
    public class ValueProviderTests
    {
        [Fact]
        public void GetValue_WhenBufferIsUnused_ReturnsEntryValue()
        {
            var entry = CreateEntry(10);

            var result = ValueProvider.GetValue(entry.Object);

            Assert.Equal(10, result);
        }

        [Fact]
        public void GetValue_WhenBufferContainsInvalidValue_ReturnsBufferedValue()
        {
            var entry = CreateEntry(10);
            entry.Object.EditBuffer.SetValue("invalid", false);

            var result = ValueProvider.GetValue(entry.Object);

            Assert.Equal("invalid", result);
        }

        [Fact]
        public void GetValidValue_WhenLatestBufferedValueIsInvalid_ReturnsEntryValue()
        {
            var entry = CreateEntry(10);
            entry.Object.EditBuffer.SetValue("invalid", false);

            var result = ValueProvider.GetValidValue(entry.Object);

            Assert.Equal(10, result);
        }

        [Fact]
        public void GetValidValueGeneric_WhenBufferContainsValidValue_ConvertsValue()
        {
            var entry = CreateEntry(10);
            entry.Object.EditBuffer.SetValue(12, true);

            var result = ValueProvider.GetValidValue<float>(entry.Object);

            Assert.Equal(12f, result);
        }

        private static Mock<IEntryBinding> CreateEntry(object value)
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.SetupGet(x => x.Value).Returns(value);
            return entry;
        }
    }
}
