using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using Moq;

namespace UnityModBase.Test.HConfigGUI
{
    public class EntryChangeSinkTests
    {
        [Theory]
        [MemberData(nameof(GetSupportedNumberConversions))]
        public void SetConvertedValue_WhenInputIsValid_QueuesTargetTypeValue(
            Type valueType,
            object initialValue,
            string input,
            object expectedValue)
        {
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var storedValue = initialValue;
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            entryMock.SetupGet(x => x.Value).Returns(() => storedValue);
            entryMock.SetupSet(x => x.Value = It.IsAny<object>()).Callback<object>(value => storedValue = value);

            sink.SetConvertedValue(entryMock.Object, input, 0.5f);
            sink.FlushValue(0.4f);

            Assert.Equal(initialValue, storedValue);

            sink.FlushValue(0.1f);

            Assert.IsType(valueType, storedValue);
            Assert.Equal(expectedValue, storedValue);
        }

        [Fact]
        public void SetConvertedValue_WhenInputIsInvalid_DoesNotChangeEntryValue()
        {
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            entryMock.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(int));
            entryMock.SetupGet(x => x.Value).Returns(10);

            sink.SetConvertedValue(entryMock.Object, "invalid", 0.5f);

            Assert.Equal("invalid", editBuffer.GetLatestValue().Value);
            Assert.False(editBuffer.GetLatestValue().IsValid);

            sink.FlushValue(0.5f);

            entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
        }

        [Fact]
        public void SetValue_WhenDelayIsNotPositiveAndValueIsUnchanged_DoesNotSetValueOrRaiseEvent()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            var value = new object();
            var eventCallCount = 0;
            Action<IEntryBinding> handler = _ => eventCallCount++;
            entryMock.SetupGet(x => x.Value).Returns(value);
            sink.OnEntryValueChanged += handler;

            try
            {
                // Act
                sink.SetValue(entryMock.Object, value, delay: 0.0f);

                // Assert
                Assert.Equal(0, eventCallCount);
                entryMock.VerifyGet(x => x.Value, Times.Once);
                entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            }
            finally
            {
                sink.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void SetValue_WhenDelayIsNotPositiveAndValueChanges_SetsValueAndRaisesEvent()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            var originalValue = new object();
            var newValue = new object();
            IEntryBinding changedEntry = null;
            var eventCallCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                changedEntry = x;
                eventCallCount++;
            };
            entryMock.SetupGet(x => x.Value).Returns(originalValue);
            entryMock.SetupSet(x => x.Value = newValue);
            sink.OnEntryValueChanged += handler;

            try
            {
                // Act
                sink.SetValue(entryMock.Object, newValue, delay: -1.0f);

                // Assert
                Assert.Equal(1, eventCallCount);
                Assert.Same(entryMock.Object, changedEntry);
                entryMock.VerifyGet(x => x.Value, Times.Once);
                entryMock.VerifySet(x => x.Value = newValue, Times.Once);
            }
            finally
            {
                sink.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void SetValue_WhenDelayIsPositive_StoresPendingValueUntilFlushCompletes()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            entryMock.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            var currentValue = new object();
            var delayedValue = new object();
            var storedValue = currentValue;
            IEntryBinding changedEntry = null;
            var eventCallCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                changedEntry = x;
                eventCallCount++;
            };
            entryMock.SetupGet(x => x.Value).Returns(() => storedValue);
            entryMock.SetupSet(x => x.Value = delayedValue).Callback<object>(x => storedValue = x);
            sink.OnEntryValueChanged += handler;

            try
            {
                // Act
                sink.SetValue(entryMock.Object, delayedValue, delay: 1.0f);
                sink.FlushValue(0.25f);

                // Assert
                Assert.Same(currentValue, storedValue);
                Assert.Equal(0, eventCallCount);
                Assert.True(editBuffer.IsUsing);
                Assert.Same(delayedValue, editBuffer.GetLatestValue().Value);
                entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);

                // Act
                sink.FlushValue(0.75f);

                // Assert
                Assert.Same(delayedValue, storedValue);
                Assert.Equal(1, eventCallCount);
                Assert.Same(entryMock.Object, changedEntry);
                Assert.False(editBuffer.IsUsing);
                entryMock.VerifySet(x => x.Value = delayedValue, Times.Once);
            }
            finally
            {
                sink.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void FlushValue_WhenBufferedValueIsInvalid_DiscardsValueWithoutRaisingChangedEvent()
        {
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            entryMock.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entryMock.SetupGet(x => x.Value).Returns(10);
            var changedCount = 0;
            Action<IEntryBinding> changedHandler = _ => changedCount++;
            sink.OnEntryValueChanged += changedHandler;

            try
            {
                sink.SetValue(entryMock.Object, "invalid", false, 0.5f);

                sink.FlushValue(0.5f);

                Assert.Equal(0, changedCount);
                Assert.False(editBuffer.IsUsing);
                entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            }
            finally
            {
                sink.OnEntryValueChanged -= changedHandler;
            }
        }

        [Fact]
        public void SetValue_WhenPendingValueIsReplaced_UsesLatestValueAndRestartsDelay()
        {
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            var storedValue = "old";
            entryMock.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entryMock.SetupGet(x => x.Value).Returns(() => storedValue);
            entryMock.SetupSet(x => x.Value = "latest").Callback<object>(value => storedValue = (string)value);

            sink.SetValue(entryMock.Object, "first", delay: 0.25f);
            sink.FlushValue(0.20f);
            sink.SetValue(entryMock.Object, "latest", delay: 0.25f);
            sink.FlushValue(0.20f);

            Assert.Equal("old", storedValue);
            Assert.True(editBuffer.IsUsing);

            sink.FlushValue(0.05f);

            Assert.Equal("latest", storedValue);
            Assert.False(editBuffer.IsUsing);
            entryMock.VerifySet(x => x.Value = "latest", Times.Once);
        }

        [Fact]
        public void ResetValue_WhenEntryHasPendingValue_RemovesPendingValueAndRaisesResetEvent()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            var currentValue = new object();
            var delayedValue = new object();
            var storedValue = currentValue;
            IEntryBinding resetEntry = null;
            var resetEventCallCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                resetEntry = x;
                resetEventCallCount++;
            };
            entryMock.SetupGet(x => x.Key).Returns("TestKey");
            entryMock.SetupGet(x => x.Value).Returns(() => storedValue);
            entryMock.Setup(x => x.ResetValue()).Callback(() => storedValue = currentValue);
            sink.OnEntryValueReset += handler;

            try
            {
                sink.SetValue(entryMock.Object, delayedValue, delay: 1.0f);

                // Act
                sink.ResetValue(entryMock.Object);
                sink.FlushValue(1.0f);

                // Assert
                Assert.Equal(1, resetEventCallCount);
                Assert.Same(entryMock.Object, resetEntry);
                Assert.Same(currentValue, storedValue);
                entryMock.Verify(x => x.ResetValue(), Times.Once);
                entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            }
            finally
            {
                sink.OnEntryValueReset -= handler;
            }
        }

        [Fact]
        public void FlushValue_WhenPendingDelayExpiresAndValueMatchesCurrent_DoesNotRaiseEvent()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            var value = new object();
            var storedValue = value;
            var eventCallCount = 0;
            Action<IEntryBinding> handler = _ => eventCallCount++;
            entryMock.SetupGet(x => x.Value).Returns(() => storedValue);
            sink.OnEntryValueChanged += handler;

            try
            {
                sink.SetValue(entryMock.Object, value, delay: 0.5f);

                // Act
                sink.FlushValue(0.5f);
                storedValue = new object();
                sink.FlushValue(1.0f);

                // Assert
                Assert.Equal(0, eventCallCount);
                entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            }
            finally
            {
                sink.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void CommitPending_WhenValidDelayedValueExists_CommitsImmediately()
        {
            var sink = new EntryChangeSink();
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            var storedValue = "old";
            entry.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entry.SetupGet(x => x.Value).Returns(() => storedValue);
            entry.SetupSet(x => x.Value = "new").Callback<object>(value => storedValue = (string)value);

            sink.SetValue(entry.Object, "new", delay: 10f);

            sink.CommitPending();
            sink.FlushValue(10f);

            Assert.Equal("new", storedValue);
            Assert.False(editBuffer.IsUsing);
            entry.VerifySet(x => x.Value = "new", Times.Once);
        }

        [Fact]
        public void CommitPending_WhenDelayedValueIsInvalid_DiscardsBuffer()
        {
            var sink = new EntryChangeSink();
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            entry.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entry.SetupGet(x => x.Value).Returns(10);

            sink.SetValue(entry.Object, "invalid", false, 10f);

            sink.CommitPending();
            sink.FlushValue(10f);

            Assert.False(editBuffer.IsUsing);
            entry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
        }

        [Fact]
        public void CommitPending_WhenMultipleEntriesArePending_CommitsEachEntryOnce()
        {
            var sink = new EntryChangeSink();
            var firstEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var secondEntry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var firstBuffer = new EntryEditBuffer();
            var secondBuffer = new EntryEditBuffer();
            var firstValue = "first-old";
            var secondValue = "second-old";
            firstEntry.SetupGet(x => x.EditBuffer).Returns(firstBuffer);
            firstEntry.SetupGet(x => x.Value).Returns(() => firstValue);
            firstEntry
                .SetupSet(x => x.Value = "first-new")
                .Callback<object>(value => firstValue = (string)value);
            secondEntry.SetupGet(x => x.EditBuffer).Returns(secondBuffer);
            secondEntry.SetupGet(x => x.Value).Returns(() => secondValue);
            secondEntry
                .SetupSet(x => x.Value = "second-new")
                .Callback<object>(value => secondValue = (string)value);
            var changedEntries = new List<IEntryBinding>();
            sink.OnEntryValueChanged += changedEntries.Add;
            sink.SetValue(firstEntry.Object, "first-new", delay: 10f);
            sink.SetValue(secondEntry.Object, "second-new", delay: 20f);

            sink.CommitPending();
            sink.FlushValue(float.MaxValue);

            Assert.Equal("first-new", firstValue);
            Assert.Equal("second-new", secondValue);
            Assert.False(firstBuffer.IsUsing);
            Assert.False(secondBuffer.IsUsing);
            Assert.Contains(firstEntry.Object, changedEntries);
            Assert.Contains(secondEntry.Object, changedEntries);
            Assert.Equal(2, changedEntries.Count);
            firstEntry.VerifySet(x => x.Value = "first-new", Times.Once);
            secondEntry.VerifySet(x => x.Value = "second-new", Times.Once);
        }

        public static IEnumerable<object[]> GetSupportedNumberConversions()
        {
            yield return new object[] { typeof(byte), (byte)1, "200", (byte)200 };
            yield return new object[] { typeof(sbyte), (sbyte)1, "-100", (sbyte)-100 };
            yield return new object[] { typeof(short), (short)1, "-32000", (short)-32000 };
            yield return new object[] { typeof(ushort), (ushort)1, "65000", (ushort)65000 };
            yield return new object[] { typeof(int), 1, "123456", 123456 };
            yield return new object[] { typeof(uint), (uint)1, "123456", (uint)123456 };
            yield return new object[] { typeof(long), 1L, "1234567890123", 1234567890123L };
            yield return new object[] { typeof(ulong), 1UL, "1234567890123", 1234567890123UL };
            yield return new object[] { typeof(float), 1.0f, "12.5", 12.5f };
            yield return new object[] { typeof(double), 1.0d, "12.5", 12.5d };
        }
    }
}
