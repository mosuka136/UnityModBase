using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using Moq;

namespace UnityModBase.Test.HConfigGUI
{
    public class EntryChangeSinkEventTests
    {
        [Fact]
        public void SetValue_WhenHandlerSubscribed_InvokesChangedHandlerWithEntry()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var originalValue = new object();
            var newValue = new object();
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.SetupGet(x => x.Value).Returns(originalValue);
            entry.SetupSet(x => x.Value = newValue);
            IEntryBinding receivedEntry = null;
            var invocationCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                receivedEntry = x;
                invocationCount++;
            };
            sink.OnEntryValueChanged += handler;

            try
            {
                // Act
                sink.SetValue(entry.Object, newValue);

                // Assert
                Assert.Equal(1, invocationCount);
                Assert.Same(entry.Object, receivedEntry);
                entry.VerifySet(x => x.Value = newValue, Times.Once);
            }
            finally
            {
                sink.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void ResetValue_WhenHandlerSubscribed_InvokesResetHandlerWithEntry()
        {
            // Arrange
            var sink = new EntryChangeSink();
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.Setup(x => x.ResetValue());
            IEntryBinding receivedEntry = null;
            var invocationCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                receivedEntry = x;
                invocationCount++;
            };
            sink.OnEntryValueReset += handler;

            try
            {
                // Act
                sink.ResetValue(entry.Object);

                // Assert
                Assert.Equal(1, invocationCount);
                Assert.Same(entry.Object, receivedEntry);
                entry.Verify(x => x.ResetValue(), Times.Once);
            }
            finally
            {
                sink.OnEntryValueReset -= handler;
            }
        }

        [Fact]
        public void SetValue_WhenDifferentSinkRaisesEvent_DoesNotInvokeHandler()
        {
            // Arrange
            var subscribedSink = new EntryChangeSink();
            var otherSink = new EntryChangeSink();
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var originalValue = new object();
            var newValue = new object();
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.SetupGet(x => x.Value).Returns(originalValue);
            entry.SetupSet(x => x.Value = newValue);
            var invocationCount = 0;
            Action<IEntryBinding> handler = _ => invocationCount++;
            subscribedSink.OnEntryValueChanged += handler;

            try
            {
                // Act
                otherSink.SetValue(entry.Object, newValue);

                // Assert
                Assert.Equal(0, invocationCount);
                entry.VerifySet(x => x.Value = newValue, Times.Once);
            }
            finally
            {
                subscribedSink.OnEntryValueChanged -= handler;
            }
        }
    }
}
