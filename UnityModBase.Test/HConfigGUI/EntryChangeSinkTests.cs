using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using Moq;

namespace UnityModBase.Test.HConfigGUI
{
    public class EntryChangeSinkTests
    {
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
            GuiPipe.OnEntryValueChanged += handler;

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
                GuiPipe.OnEntryValueChanged -= handler;
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
            GuiPipe.OnEntryValueChanged += handler;

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
                GuiPipe.OnEntryValueChanged -= handler;
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
            GuiPipe.OnEntryValueChanged += handler;

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
                GuiPipe.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void FlushValue_WhenBufferedValueIsInvalid_DiscardsValueAndRaisesEditFinishedOnly()
        {
            var sink = new EntryChangeSink();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editBuffer = new EntryEditBuffer();
            entryMock.SetupGet(x => x.EditBuffer).Returns(editBuffer);
            entryMock.SetupGet(x => x.Value).Returns(10);
            var changedCount = 0;
            var finishedCount = 0;
            Action<IEntryBinding> changedHandler = _ => changedCount++;
            Action<IEntryBinding> finishedHandler = _ => finishedCount++;
            GuiPipe.OnEntryValueChanged += changedHandler;
            GuiPipe.OnEntryEditFinished += finishedHandler;

            try
            {
                sink.SetValue(entryMock.Object, "invalid", false, 0.5f);

                sink.FlushValue(0.5f);

                Assert.Equal(0, changedCount);
                Assert.Equal(1, finishedCount);
                Assert.False(editBuffer.IsUsing);
                entryMock.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            }
            finally
            {
                GuiPipe.OnEntryValueChanged -= changedHandler;
                GuiPipe.OnEntryEditFinished -= finishedHandler;
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
            GuiPipe.OnEntryValueReset += handler;

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
                GuiPipe.OnEntryValueReset -= handler;
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
            GuiPipe.OnEntryValueChanged += handler;

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
                GuiPipe.OnEntryValueChanged -= handler;
            }
        }
    }
}
