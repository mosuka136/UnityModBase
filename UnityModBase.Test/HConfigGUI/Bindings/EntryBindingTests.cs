using Moq;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigGUI.Bindings
{
    public class EntryBindingTests
    {
        private class TestConfig
        {
            [ConfigSlider(-1f, 20f, 0.1f)]
            public float SetLootDropRatio { get; set; }

            public bool EnableUnityModBase { get; set; }
        }

        [Fact]
        public void Key_EntryProvided_ReturnsEntryKey()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.Key).Returns("EnableUnityModBase");
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            var result = binding.Key;

            // Assert
            Assert.Equal("EnableUnityModBase", result);
        }

        [Fact]
        public void Name_EntryProvided_ReturnsEntryName()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            var name = new Translator("名称", "Name");
            entryMock.SetupGet(entry => entry.Name).Returns(name);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            var result = binding.Name;

            // Assert
            Assert.Same(name, result);
        }

        [Fact]
        public void Description_EntryProvided_ReturnsEntryDescription()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            var description = new Translator("描述", "Description");
            entryMock.SetupGet(entry => entry.Description).Returns(description);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            var result = binding.Description;

            // Assert
            Assert.Same(description, result);
        }

        [Fact]
        public void ValueType_EntryProvided_ReturnsEntryValueType()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.ValueType).Returns(typeof(string));
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            var result = binding.ValueType;

            // Assert
            Assert.Equal(typeof(string), result);
        }

        [Fact]
        public void Value_GetterCalled_ReturnsEntryBoxedValue()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupProperty(entry => entry.BoxedValue, 42);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            var result = binding.Value;

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void Value_SetterGivenAssignableDifferentValue_UpdatesEntryBoxedValue()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.ValueType).Returns(typeof(int));
            entryMock.SetupProperty(entry => entry.BoxedValue, 1);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            binding.Value = 2;

            // Assert
            Assert.Equal(2, entryMock.Object.BoxedValue);
            entryMock.VerifySet(entry => entry.BoxedValue = 2, Times.Once);
        }

        [Fact]
        public void Value_SetterGivenAssignableSameValue_DoesNotUpdateEntryBoxedValue()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.ValueType).Returns(typeof(int));
            entryMock.SetupProperty(entry => entry.BoxedValue, 5);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            binding.Value = 5;

            // Assert
            Assert.Equal(5, entryMock.Object.BoxedValue);
            entryMock.VerifySet(entry => entry.BoxedValue = It.IsAny<object>(), Times.Never);
        }

        [Fact]
        public void Value_SetterGivenIncompatibleValue_ThrowsArgumentException()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.ValueType).Returns(typeof(int));
            entryMock.SetupProperty(entry => entry.BoxedValue, 1);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            var exception = Assert.Throws<ArgumentException>(() => binding.Value = "invalid");

            // Assert
            Assert.Contains("Invalid value type", exception.Message);
            Assert.Contains(typeof(int).ToString(), exception.Message);
            Assert.Contains(typeof(string).ToString(), exception.Message);
            Assert.Equal(1, entryMock.Object.BoxedValue);
        }


        [Fact]
        public void ResetValue_DefaultValueAvailable_UpdatesEntryBoxedValueToDefaultValue()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.BoxedDefaultValue).Returns(99);
            entryMock.SetupProperty(entry => entry.BoxedValue, 1);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Act
            binding.ResetValue();

            // Assert
            Assert.Equal(99, entryMock.Object.BoxedValue);
            entryMock.VerifySet(entry => entry.BoxedValue = 99, Times.Once);
        }

        [Fact]
        public void Constructor_EntryWithSliderMetadataKey_SetsEntryAndResolvesSliderMetadata()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.Key).Returns("SetLootDropRatio");

            // Act
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Same(entryMock.Object, binding.Entry);
            var metadata = Assert.IsType<global::UnityModBase.HConfigGUI.UiSliderMetadata>(binding.Metadata);
            Assert.Equal(-1f, metadata.Min);
            Assert.Equal(20f, metadata.Max);
            Assert.Equal(0.1f, metadata.Step);
        }

        [Fact]
        public void Constructor_EntryWithExistingNonSliderKey_SetsEntryAndMetadataToNull()
        {
            // Arrange
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.Key).Returns("EnableUnityModBase");

            // Act
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            // Assert
            Assert.Same(entryMock.Object, binding.Entry);
            Assert.Null(binding.Metadata);
        }

        [Fact]
        public void Constructor_ValidEntry_CreatesOwnedEditBuffer()
        {
            var entryMock = CreateEntryMock();

            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);

            Assert.NotNull(binding.EditBuffer);
            Assert.False(binding.EditBuffer.IsUsing);
        }

        [Fact]
        public void ResetValue_WhenEditBufferContainsValue_ClearsBufferAndRestoresDefaultValue()
        {
            var entryMock = CreateEntryMock();
            entryMock.SetupGet(entry => entry.BoxedDefaultValue).Returns(99);
            entryMock.SetupProperty(entry => entry.BoxedValue, 1);
            var binding = new EntryBinding(typeof(TestConfig), entryMock.Object);
            binding.EditBuffer.SetValue(2, true);

            binding.ResetValue();

            Assert.False(binding.EditBuffer.IsUsing);
            Assert.Equal(99, entryMock.Object.BoxedValue);
        }


        private static Mock<IConfigEntry> CreateEntryMock()
        {
            var entryMock = new Mock<IConfigEntry>();
            entryMock.SetupGet(entry => entry.Key).Returns("EnableUnityModBase");
            entryMock.SetupGet(entry => entry.Name).Returns(new Translator("默认名称", "Default Name"));
            entryMock.SetupGet(entry => entry.Description).Returns(new Translator("默认描述", "Default Description"));
            entryMock.SetupGet(entry => entry.ValueType).Returns(typeof(int));
            entryMock.SetupProperty(entry => entry.BoxedValue, 0);
            return entryMock;
        }
    }
}
