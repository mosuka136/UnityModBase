using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.Test.HGuiSpace.Bindings
{
    public class DualValueDescriptorTests
    {
        [Fact]
        public void Constructor_WithValidGenericValueType_DescribesOnlyMatchingClosedTypes()
        {
            var descriptor = CreateDescriptor();

            Assert.True(descriptor.CanDescribe(typeof(TestDualValue<int, string>)));
            Assert.False(descriptor.CanDescribe(typeof(Tuple<int, string>)));
            Assert.False(descriptor.CanDescribe(typeof(TestDualValue<,>)));
            Assert.False(descriptor.CanDescribe(null));
        }

        [Fact]
        public void Constructor_WhenGenericTypeOrPropertiesAreInvalid_ThrowsMatchingArgumentException()
        {
            Assert.Equal("genericTypeDefinition", Assert.Throws<ArgumentException>(() =>
                new DualValueDescriptor(typeof(string), "Value1", "Value2")).ParamName);
            Assert.Equal("value1PropertyName", Assert.Throws<ArgumentException>(() =>
                new DualValueDescriptor(typeof(TestDualValue<,>), "Missing", "Value2")).ParamName);
            Assert.Equal("value2PropertyName", Assert.Throws<ArgumentException>(() =>
                new DualValueDescriptor(typeof(TestDualValue<,>), "Value1", "Missing")).ParamName);
        }

        [Fact]
        public void SlotOperations_ReadTypesAndCreateRequestedClosedValue()
        {
            var descriptor = CreateDescriptor();

            Assert.Equal(typeof(int), descriptor.GetSlotType(typeof(TestDualValue<int, string>), 0));
            Assert.Equal(typeof(string), descriptor.GetSlotType(typeof(TestDualValue<int, string>), 1));
            var value = Assert.IsType<TestDualValue<int, string>>(
                descriptor.CreateValue(typeof(TestDualValue<int, string>), 9, "east"));
            Assert.Equal(9, value.Value1);
            Assert.Equal("east", value.Value2);
        }

        private static DualValueDescriptor CreateDescriptor()
        {
            return new DualValueDescriptor(
                typeof(TestDualValue<,>),
                nameof(TestDualValue<int, int>.Value1),
                nameof(TestDualValue<int, int>.Value2));
        }

        private sealed class TestDualValue<T1, T2>
        {
            public T1 Value1 { get; }
            public T2 Value2 { get; }

            public TestDualValue(T1 value1, T2 value2)
            {
                Value1 = value1;
                Value2 = value2;
            }
        }
    }
}
