using Moq;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.Test.HGuiSpace
{
    public class EntryEditBufferTests
    {
        [Fact]
        public void SetValue_WhenMultipleSourcesExist_ReturnsLatestEntry()
        {
            var buffer = new EntryEditBuffer();

            buffer.SetValue("text", "first", true);
            buffer.SetValue("second", true);

            var result = buffer.GetLatestValue();

            Assert.Equal("second", result.Value);
            Assert.True(result.IsValid);
            Assert.True(buffer.IsUsing);
        }

        [Fact]
        public void GetLatestValue_WhenKeyedValueIsNewest_ReturnsKeyedEntry()
        {
            var buffer = new EntryEditBuffer();
            buffer.SetValue("first", true);
            buffer.SetValue("field", "latest", true);

            var result = buffer.GetLatestValue();

            Assert.Equal("latest", result.Value);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetValue_WithInvalidKey_ThrowsArgumentException(string key)
        {
            var buffer = new EntryEditBuffer();

            var exception = Assert.Throws<ArgumentException>(() => buffer.SetValue(key, 42, true));

            Assert.Equal("key", exception.ParamName);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void SetValue_WithNullOrderedEntry_ThrowsArgumentNullException()
        {
            var buffer = new EntryEditBuffer();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                buffer.SetValue("field", (EntryEditBuffer.OrderedEntry)null));

            Assert.Equal("entry", exception.ParamName);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void SetValue_WithOrderedEntry_PreservesProvidedEntryAndOrder()
        {
            var buffer = new EntryEditBuffer();
            buffer.SetValue("global", true);
            var orderedEntry = new EntryEditBuffer.OrderedEntry("keyed", 100, true);

            buffer.SetValue("field", orderedEntry);

            Assert.Same(orderedEntry, buffer.GetValue("field"));
            Assert.Same(orderedEntry, buffer.GetLatestValue());
            Assert.True(buffer.IsUsing);
        }

        [Fact]
        public void Commit_WhenLatestEntryIsValid_UpdatesEntryAndClearsBuffer()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry("old");
            buffer.SetValue("new", true);

            var result = buffer.Commit(entry.Object);

            Assert.True(result);
            Assert.Equal("new", entry.Object.Value);
            Assert.False(buffer.IsUsing);
            Assert.True(buffer.GetLatestValue().IsEmpty);
        }

        [Fact]
        public void Commit_WhenLatestEntryIsInvalid_DiscardsChangesAndClearsBuffer()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(10);
            buffer.SetValue(20, true);
            buffer.SetValue("invalid", false);

            var result = buffer.Commit(entry.Object);

            Assert.False(result);
            Assert.Equal(10, entry.Object.Value);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void Commit_WhenTransformedValueMatchesCurrentValue_DoesNotSetEntry()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(10);
            buffer.SetValue("10", true);

            var result = buffer.Commit(entry.Object, value => int.Parse((string)value));

            Assert.False(result);
            entry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void Commit_WhenTransformReturnsNull_DiscardsValueAndClearsBuffer()
        {
            // transform 返回 null 是"放弃本次写入"的哨兵（如提交时刻类型转换失败），不是写入 null；
            // 输入本身有效时也不例外，且缓冲仍被清空。
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(10);
            buffer.SetValue("20", true);

            var result = buffer.Commit(entry.Object, _ => null);

            Assert.False(result);
            Assert.Equal(10, entry.Object.Value);
            Assert.False(buffer.IsUsing);
        }

        [Fact]
        public void Commit_WithKey_WhenTransformReturnsNull_DiscardsValueAndClearsBuffer()
        {
            // 有键提交与无键提交对 null 哨兵的语义一致：放弃写入并清空包括其他来源在内的整个缓冲区。
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(10);
            buffer.SetValue("field", "20", true);

            var result = buffer.Commit("field", entry.Object, _ => null);

            Assert.False(result);
            Assert.Equal(10, entry.Object.Value);
            Assert.False(buffer.IsUsing);
            Assert.True(buffer.GetValue("field").IsEmpty);
        }

        [Fact]
        public void Commit_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            var buffer = new EntryEditBuffer();

            var exception = Assert.Throws<ArgumentNullException>(() => buffer.Commit(null));

            Assert.Equal("entry", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Commit_WithInvalidKey_ThrowsArgumentException(string key)
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(0);

            var exception = Assert.Throws<ArgumentException>(() => buffer.Commit(key, entry.Object));

            Assert.Equal("key", exception.ParamName);
            entry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
        }

        [Fact]
        public void Commit_WithKeyAndNullEntry_ThrowsArgumentNullException()
        {
            var buffer = new EntryEditBuffer();

            var exception = Assert.Throws<ArgumentNullException>(() => buffer.Commit("field", null));

            Assert.Equal("entry", exception.ParamName);
        }

        [Fact]
        public void Commit_WithKey_TransformsSelectedValueAndClearsEntireBuffer()
        {
            var buffer = new EntryEditBuffer();
            var entry = CreateEntry(0);
            buffer.SetValue("other", "ignored", true);
            buffer.SetValue("field", "42", true);

            var result = buffer.Commit("field", entry.Object, value => int.Parse((string)value));

            Assert.True(result);
            Assert.Equal(42, entry.Object.Value);
            Assert.False(buffer.IsUsing);
            Assert.True(buffer.GetValue("field").IsEmpty);
            Assert.True(buffer.GetValue("other").IsEmpty);
        }

        [Fact]
        public void Clear_WhenBufferContainsValues_ResetsAllState()
        {
            var buffer = new EntryEditBuffer();
            buffer.SetValue("field", 42, true);
            buffer.SetValue(99, true);

            buffer.Clear();

            Assert.False(buffer.IsUsing);
            Assert.Equal(0, buffer.Seq);
            Assert.True(buffer.Value.IsEmpty);
            Assert.True(buffer.GetValue("field").IsEmpty);
        }

        private static Mock<IEntryBinding> CreateEntry(object value)
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupProperty(x => x.Value, value);
            return entry;
        }
    }
}
