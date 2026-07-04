using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using Moq;

namespace UnityModBase.Test.HConfigGUI
{
    public class GuiPipeTests
    {
        [Fact]
        public void InvokeOnEntryValueChanged_WhenHandlerSubscribed_InvokesHandlerWithEntry()
        {
            // Arrange
            var entry = new Mock<IEntryBinding>(MockBehavior.Loose);
            entry.SetupGet(x => x.Key).Returns("TestKey");
            IEntryBinding receivedEntry = null;
            var invocationCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                receivedEntry = x;
                invocationCount++;
            };
            GuiPipe.OnEntryValueChanged += handler;

            try
            {
                // Act
                GuiPipe.InvokeOnEntryValueChanged(entry.Object);

                // Assert
                Assert.Equal(1, invocationCount);
                Assert.Same(entry.Object, receivedEntry);
            }
            finally
            {
                GuiPipe.OnEntryValueChanged -= handler;
            }
        }

        [Fact]
        public void InvokeOnEntryValueReset_WhenHandlerSubscribed_InvokesHandlerWithEntry()
        {
            // Arrange
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            IEntryBinding receivedEntry = null;
            var invocationCount = 0;
            Action<IEntryBinding> handler = x =>
            {
                receivedEntry = x;
                invocationCount++;
            };
            GuiPipe.OnEntryValueReset += handler;

            try
            {
                // Act
                GuiPipe.InvokeOnEntryValueReset(entry.Object);

                // Assert
                Assert.Equal(1, invocationCount);
                Assert.Same(entry.Object, receivedEntry);
            }
            finally
            {
                GuiPipe.OnEntryValueReset -= handler;
            }
        }

    }
}
