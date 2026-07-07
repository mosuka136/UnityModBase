using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.Test.HConfigGUI
{
    public class GuiContextTests
    {
        [Fact]
        public void GetKey_WhenSuffixProvided_ReturnsConcatenatedKey()
        {
            // Arrange
            var store = new GuiStateStore();
            var entryMock = new Mock<IEntryBinding>();
            entryMock.SetupGet(x => x.Key).Returns("EntryKey");

            // Act
            var result = store.GetKey(entryMock.Object, "Suffix");

            // Assert
            Assert.Equal("EntryKey+Suffix", result);
        }


        [Fact]
        public void GetKey_WhenSuffixNotProvided_ReturnsKeyWithSeparator()
        {
            // Arrange
            var store = new GuiStateStore();
            var entryMock = new Mock<IEntryBinding>();
            entryMock.SetupGet(x => x.Key).Returns("EntryKey");

            // Act
            var result = store.GetKey(entryMock.Object);

            // Assert
            Assert.Equal("EntryKey+", result);
        }

        [Fact]
        public void GetFloat_WhenKeyDoesNotExist_ReturnsAndStoresDefaultValue()
        {
            // Arrange
            var store = new GuiStateStore();

            // Act
            var result = store.GetFloat("FloatKey", 1.5f);
            var storedValue = store.GetFloat("FloatKey", 9.9f);

            // Assert
            Assert.Equal(1.5f, result);
            Assert.Equal(1.5f, storedValue);
        }

        [Fact]
        public void SetFloat_WhenCalled_StoresValueForKey()
        {
            // Arrange
            var store = new GuiStateStore();

            // Act
            store.SetFloat("FloatKey", 3.5f);
            var result = store.GetFloat("FloatKey", 1.5f);

            // Assert
            Assert.Equal(3.5f, result);
        }





        [Fact]
        public void DeleteFloat_WhenKeyExists_RemovesStoredValue()
        {
            // Arrange
            var store = new GuiStateStore();
            store.SetFloat("FloatKey", 2.5f);

            // Act
            store.DeleteFloat("FloatKey");
            var result = store.GetFloat("FloatKey", 1.5f);

            // Assert
            Assert.Equal(1.5f, result);
        }

        [Fact]
        public void DeleteFloat_WhenKeyDoesNotExist_DoesNothing()
        {
            // Arrange
            var store = new GuiStateStore();
            store.SetFloat("OtherKey", 2.5f);

            // Act
            store.DeleteFloat("MissingKey");
            var result = store.GetFloat("OtherKey", 1.5f);

            // Assert
            Assert.Equal(2.5f, result);
        }

        [Fact]
        public void DeleteFloat_WhenEntryProvided_RemovesAllMatchingKeysAndKeepsOthers()
        {
            // Arrange
            var store = new GuiStateStore();
            var entryMock = new Mock<IEntryBinding>();
            entryMock.SetupGet(x => x.Key).Returns("EntryKey");
            store.SetFloat("EntryKey", 1.1f);
            store.SetFloat(store.GetKey(entryMock.Object, "SuffixA"), 2.2f);
            store.SetFloat(store.GetKey(entryMock.Object, "SuffixB"), 3.3f);
            store.SetFloat("OtherKey", 4.4f);

            // Act
            store.DeleteFloat(entryMock.Object);
            var exactResult = store.GetFloat("EntryKey", 10.1f);
            var suffixAResult = store.GetFloat(store.GetKey(entryMock.Object, "SuffixA"), 20.2f);
            var suffixBResult = store.GetFloat(store.GetKey(entryMock.Object, "SuffixB"), 30.3f);
            var otherResult = store.GetFloat("OtherKey", 40.4f);

            // Assert
            Assert.Equal(10.1f, exactResult);
            Assert.Equal(20.2f, suffixAResult);
            Assert.Equal(30.3f, suffixBResult);
            Assert.Equal(4.4f, otherResult);
        }

        [Fact]
        public void DeleteFloat_WhenEntryIsNull_KeepsExistingState()
        {
            // Arrange
            var store = new GuiStateStore();
            store.SetFloat("ExistingKey", 2.5f);

            // Act
            store.DeleteFloat((IEntryBinding)null);

            // Assert
            Assert.Equal(2.5f, store.GetFloat("ExistingKey", 1.5f));
        }

        [Fact]
        public void DeleteFloat_WhenEntryKeyIsEmpty_KeepsExistingState()
        {
            // Arrange
            var store = new GuiStateStore();
            var entryMock = new Mock<IEntryBinding>();
            entryMock.SetupGet(x => x.Key).Returns(string.Empty);
            store.SetFloat("ExistingKey", 2.5f);

            // Act
            store.DeleteFloat(entryMock.Object);

            // Assert
            Assert.Equal(2.5f, store.GetFloat("ExistingKey", 1.5f));
        }

    }
}
