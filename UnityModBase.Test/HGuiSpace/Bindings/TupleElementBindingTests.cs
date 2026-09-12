using System;
using System.Collections.Generic;
using Moq;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Bindings
{
    /// <summary>
    /// 元组元素投影绑定把封闭 ValueTuple 条目的单个元素投影为独立配置项绑定：
    /// 元素写入读取全部元素并构造新元组实例整体写回，各元素暂存缓冲互相独立。
    /// </summary>
    public class TupleElementBindingTests
    {
        [Fact]
        public void Constructor_WhenParentIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new TupleElementBinding(null, 0));
            Assert.Equal("parent", exception.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        public void Constructor_WhenElementIndexIsOutOfRange_ThrowsArgumentOutOfRangeException(int elementIndex)
        {
            // Arrange
            var parentMock = CreateParentMock<(int, string)>((1, "a"));

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new TupleElementBinding(parentMock.Object, elementIndex));
            Assert.Equal("elementIndex", exception.ParamName);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(EntryValue<int, string>))]
        [InlineData(null)]
        public void Constructor_WhenParentValueTypeIsNotSupportedTuple_ThrowsArgumentException(Type valueType)
        {
            // Arrange：非元组类型和双元素条目值都没有 Item 字段可投影。
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(valueType);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(
                () => new TupleElementBinding(parentMock.Object, 0));
            Assert.Equal("parent", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenParentValueTypeIsOpenGeneric_ThrowsArgumentException()
        {
            // Arrange：开放泛型定义缺少元素类型参数，无法确定元素值类型。
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(ValueTuple<,>));

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new TupleElementBinding(parentMock.Object, 0));
        }

        [Theory]
        [InlineData(typeof(ValueTuple<int>), true)]
        [InlineData(typeof((int, string)), true)]
        [InlineData(typeof((int, int, int, int, int, int, int)), true)]
        [InlineData(typeof((int, int, int, int, int, int, int, int)), false)]
        [InlineData(typeof(ValueTuple<,>), false)]
        [InlineData(typeof(EntryValue<int, string>), false)]
        [InlineData(typeof(string), false)]
        [InlineData(null, false)]
        public void IsSupportedTupleType_WhenGivenVariousTypes_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Act
            var result = TupleElementBinding.IsSupportedTupleType(valueType, out var elementTypes);

            // Assert
            Assert.Equal(expected, result);
            if (expected)
                Assert.NotNull(elementTypes);
            else
                Assert.Null(elementTypes);
        }

        [Theory]
        [InlineData(typeof((int, string)), true)]
        [InlineData(typeof((int, int, int)), true)]
        [InlineData(typeof((int, ElementEnum)), true)]
        [InlineData(typeof((int, List<int>)), false)]
        [InlineData(typeof(((int, int), int)), false)]
        [InlineData(typeof((int, int, int, int, int, int, int, int)), false)]
        [InlineData(typeof(List<int>), false)]
        [InlineData(null, false)]
        public void IsEditableTupleType_WhenGivenVariousTypes_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange：可整体编辑要求封闭 1 至 7 元且元素全部为基础类型或枚举；
            // 元素为集合或嵌套元组的组合不可编辑。

            // Act
            var result = TupleElementBinding.IsEditableTupleType(valueType);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ElementView_WhenParentProvided_ExposesElementTypesKeysAndDisplayInfo()
        {
            // Arrange
            var name = new Translator("名称", "Name");
            var parentMock = CreateParentMock<(int, string)>((1, "a"));
            parentMock.SetupGet(x => x.Name).Returns(name);

            // Act
            var element0 = new TupleElementBinding(parentMock.Object, 0);
            var element1 = new TupleElementBinding(parentMock.Object, 1);

            // Assert：元素值类型对应各自泛型参数，名称透传父条目，键彼此不同且不与父键冲突。
            Assert.Equal(typeof(int), element0.ValueType);
            Assert.Equal(typeof(string), element1.ValueType);
            Assert.Same(name, element0.Name);
            Assert.Equal("TupleEntry", parentMock.Object.Key);
            Assert.Equal("\0Tuple_TupleEntry_0", element0.Key);
            Assert.Equal("\0Tuple_TupleEntry_1", element1.Key);
            Assert.NotEqual(element0.Key, element1.Key);
            Assert.NotNull(element0.Description);
            Assert.Equal(string.Empty, element0.Description.Chinese);
            Assert.Equal(string.Empty, element0.Description.English);
            Assert.Null(element0.Metadata);
            Assert.NotNull(element0.EditBuffer);
            Assert.NotSame(element0.EditBuffer, element1.EditBuffer);
        }

        [Fact]
        public void Value_Get_WhenParentHasCommittedTuple_ReturnsElementAtIndex()
        {
            // Arrange
            var parentMock = CreateParentMock<(int, string)>((42, "hello"));

            // Act & Assert
            Assert.Equal(42, new TupleElementBinding(parentMock.Object, 0).Value);
            Assert.Equal("hello", new TupleElementBinding(parentMock.Object, 1).Value);
        }

        [Fact]
        public void Value_Get_WhenParentValueIsNull_ReturnsNull()
        {
            // Arrange：父条目值缺失属于异常状态，元素读取降级为 null 而不抛出。
            var parentMock = CreateParentMock<(int, string)>(value: null);

            // Act & Assert
            Assert.Null(new TupleElementBinding(parentMock.Object, 0).Value);
        }

        [Fact]
        public void Value_Set_ReplacesOnlyTargetElementWithNewTupleInstance()
        {
            // Arrange
            var original = (1, "a");
            var parentMock = CreateParentMock<(int, string)>(original);
            var element0 = new TupleElementBinding(parentMock.Object, 0);

            // Act
            element0.Value = 7;

            // Assert：只替换第一个元素，第二个元素保持父条目当前已提交值。
            var result = Assert.IsType<(int, string)>(parentMock.Object.Value);
            Assert.Equal((7, "a"), result);
        }

        [Fact]
        public void Value_SetOnThreeElementTuple_ReplacesOnlyTargetElement()
        {
            // Arrange
            var parentMock = CreateParentMock<(int, int, int)>((1, 2, 3));
            var element1 = new TupleElementBinding(parentMock.Object, 1);

            // Act
            element1.Value = 9;

            // Assert
            var result = Assert.IsType<(int, int, int)>(parentMock.Object.Value);
            Assert.Equal((1, 9, 3), result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-number")]
        public void Value_SetInvalidValue_ThrowsArgumentException(object invalidValue)
        {
            // Arrange：与 EntryBinding.Value 相同的赋值约束——null 或不可赋值类型拒绝写入，不带参数名。
            var parentMock = CreateParentMock<(int, string)>((1, "a"));
            var element0 = new TupleElementBinding(parentMock.Object, 0);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => element0.Value = invalidValue);
        }

        [Fact]
        public void Value_SetInvokesOnParentValueWrittenCallback()
        {
            // Arrange
            var parentMock = CreateParentMock<(int, string)>((1, "a"));
            var callbackCount = 0;
            var element0 = new TupleElementBinding(parentMock.Object, 0, () => callbackCount++);

            // Act
            element0.Value = 7;

            // Assert
            Assert.Equal(1, callbackCount);
        }

        /// <summary>
        /// 创建值为指定元组并带独立编辑缓冲区的父条目严格替身；
        /// 值用 SetupProperty 提供可读写的已提交值存储。泛型参数决定值类型，
        /// 值参数按装箱对象传入以便测试 null 降级路径。
        /// </summary>
        private static Mock<IEntryBinding> CreateParentMock<TTuple>(object value)
        {
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(TTuple));
            parentMock.SetupProperty(x => x.Value, value);
            parentMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            parentMock.SetupGet(x => x.Key).Returns("TupleEntry");
            return parentMock;
        }

        private enum ElementEnum
        {
            First,
            Second,
        }
    }
}
