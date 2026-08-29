using System;
using System.Collections;
using System.Collections.Generic;
using UnityModBase.HConfigSpace;
using UnityModBase.HControlSpace;

namespace UnityModBase.Test.HEntrySpace
{
    public class EntryValueTests
    {
        [Fact]
        public void Constructor_StoresBothElements()
        {
            var value = new EntryValue<int, string>(7, "north");

            Assert.Equal(7, value.Value1);
            Assert.Equal("north", value.Value2);
        }

        [Fact]
        public void Equals_EquivalentValues_AgreesAcrossTypedObjectAndInterfaceViews()
        {
            var first = new EntryValue<int, string>(7, "north");
            var second = new EntryValue<int, string>(7, "north");

            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object)second));
            Assert.True(((IEntryValue)first).Equals(second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Theory]
        [InlineData(8, "north")]
        [InlineData(7, "south")]
        public void Equals_WhenEitherElementDiffers_ReturnsFalse(int value1, string value2)
        {
            var value = new EntryValue<int, string>(7, "north");

            Assert.False(value.Equals(new EntryValue<int, string>(value1, value2)));
        }

        [Fact]
        public void Equals_NullOrDifferentEntryValueImplementation_ReturnsFalse()
        {
            var value = new EntryValue<int, string>(7, "north");

            Assert.False(value.Equals((EntryValue<int, string>)null));
            Assert.False(((IEntryValue)value).Equals(new OtherEntryValue()));
        }

        [Fact]
        public void Equals_NestedCollectionsUseDeepComparisonAndMatchingHashCodes()
        {
            var first = new EntryValue<int[], List<string>>(
                new[] { 1, 2 },
                new List<string> { "a", "b" });
            var second = new EntryValue<int[], List<string>>(
                new[] { 1, 2 },
                new List<string> { "a", "b" });

            Assert.True(first.Equals(second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void ValueEqual_MultidimensionalArraysWithDifferentShapes_ReturnsFalse()
        {
            var first = new int[1, 2] { { 1, 2 } };
            var second = new int[2, 1] { { 1 }, { 2 } };

            Assert.False(EntryModel.ValueEqual(first, second));
        }

        [Fact]
        public void ValueEqual_MultidimensionalArraysWithSameShape_ComparesContentAndMatchesHashCodes()
        {
            // 等值判断按秩和各维长度先排除形状差异，再逐元素比较；哈希规则须与之一致。
            var first = new int[2, 2] { { 1, 2 }, { 3, 4 } };
            var second = new int[2, 2] { { 1, 2 }, { 3, 4 } };
            var third = new int[2, 2] { { 1, 2 }, { 3, 5 } };

            Assert.True(EntryModel.ValueEqual(first, second));
            Assert.False(EntryModel.ValueEqual(first, third));
            Assert.Equal(EntryModel.ValueHashCode(first), EntryModel.ValueHashCode(second));
        }

        [Fact]
        public void ValueEqual_SameElementsInDifferentRuntimeTypes_ConservativelyReturnsFalse()
        {
            // 声明类型是条目契约的一部分；数组与列表即使元素相同也不等价。
            Assert.False(EntryModel.ValueEqual(new[] { 1, 2 }, new List<int> { 1, 2 }));
            Assert.False(EntryModel.ValueEqual(1, 1L));
        }

        [Fact]
        public void ValueEqual_EntryValueImplementingEnumerable_UsesBusinessEqualityOverSequence()
        {
            // 同时可枚举的自定义值类型必须走 IEntryValue 业务等值分支，而不是按序列比较。
            var first = new EnumerableEntryValue(7, counterA: 1);
            var second = new EnumerableEntryValue(7, counterA: 2);

            Assert.True(EntryModel.ValueEqual(first, second));
            Assert.False(EntryModel.ValueEqual(first, new EnumerableEntryValue(8, counterA: 1)));
        }

        [Theory]
        [InlineData(typeof(EntryValue<int, string>), true)]
        [InlineData(typeof(EntryValue<EntryValue<int, int>, int>), false)]
        [InlineData(typeof(EntryValue<,>), false)]
        [InlineData(typeof((int, string)), false)]
        [InlineData(typeof(string), false)]
        public void IsEntryMultipleValueType_ReturnsExpectedResult(Type type, bool expected)
        {
            Assert.Equal(expected, EntryModel.IsEntryMultipleValueType(type));
        }

        [Fact]
        public void IsEntryMultipleValueType_Null_ReturnsFalse()
        {
            Assert.False(EntryModel.IsEntryMultipleValueType(null));
        }

        [Theory]
        [InlineData(typeof(EntryValue<int, string>), true)]
        [InlineData(typeof(OtherEntryValue), true)]
        [InlineData(typeof(int), true)]
        [InlineData(typeof(string), true)]
        [InlineData(typeof(TestEnum), true)]
        [InlineData(typeof(int[]), true)]
        [InlineData(typeof(List<int>), true)]
        [InlineData(typeof((int, string)), true)]
        [InlineData(typeof(object), false)]
        [InlineData(typeof(decimal), false)]
        public void IsEntryValueType_AcceptsSharedValueModelsOnly(Type type, bool expected)
        {
            Assert.Equal(expected, EntryModel.IsEntryValueType(type));
        }

        [Fact]
        public void IsEntryValueType_Null_ReturnsFalse()
        {
            Assert.False(EntryModel.IsEntryValueType(null));
        }

        [Theory]
        [InlineData("Entry_1", true)]
        [InlineData("条目1", true)]
        [InlineData("", false)]
        [InlineData("Entry-1", false)]
        public void EntryAndTableKeyValidation_UseTheSameRules(string key, bool expected)
        {
            Assert.Equal(expected, EntryModel.IsValidEntryKey(key));
            Assert.Equal(expected, EntryModel.IsValidTableKey(key));
        }

        [Fact]
        public void ValueEqual_NullAndUnsupportedObjectsFollowConservativeRules()
        {
            Assert.True(EntryModel.ValueEqual(null, null));
            Assert.False(EntryModel.ValueEqual(null, new object()));
            Assert.False(EntryModel.ValueEqual(new object(), new object()));
        }

        [Fact]
        public void EntryInterfaces_ExposeWriteCapabilityOnlyForConfigEntries()
        {
            Assert.False(typeof(IEntry).GetProperty(nameof(IEntry.BoxedValue)).CanWrite);
            Assert.True(typeof(IWritableEntry).GetProperty(nameof(IWritableEntry.BoxedValue)).CanWrite);
            Assert.True(typeof(IWritableEntry).IsAssignableFrom(typeof(IConfigEntry)));
            Assert.False(typeof(IWritableEntry).IsAssignableFrom(typeof(IControlEntry)));
        }

        private sealed class OtherEntryValue : IEntryValue
        {
            public bool Equals(IEntryValue other)
            {
                return ReferenceEquals(this, other);
            }
        }

        // 业务等值只看 businessValue；枚举序列由 counterA 决定，使两种比较规则产生不同结果。
        private sealed class EnumerableEntryValue : IEntryValue, IEnumerable<int>
        {
            private readonly int _businessValue;
            private readonly int _counterA;

            public EnumerableEntryValue(int businessValue, int counterA)
            {
                _businessValue = businessValue;
                _counterA = counterA;
            }

            public bool Equals(IEntryValue other)
            {
                return other is EnumerableEntryValue value && value._businessValue == _businessValue;
            }

            public IEnumerator<int> GetEnumerator()
            {
                yield return _counterA;
                yield return _businessValue;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private enum TestEnum
        {
            First,
            Second
        }
    }
}
