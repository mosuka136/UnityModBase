using System;
using System.Collections.Generic;
using Moq;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Bindings
{
    /// <summary>
    /// 集合元素投影绑定把有序集合条目的单个元素投影为独立配置项绑定：
    /// 元素写入复制父集合并构造新实例整体写回，各元素暂存缓冲互相独立。
    /// </summary>
    public class CollectionElementBindingTests
    {
        [Fact]
        public void Constructor_WhenParentIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new CollectionElementBinding(null, 0));
            Assert.Equal("parent", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenElementIndexIsNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var parentMock = CreateParentMock(new List<int> { 1 });

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new CollectionElementBinding(parentMock.Object, -1));
            Assert.Equal("elementIndex", exception.ParamName);
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(HashSet<int>))]
        [InlineData(typeof(int[,]))]
        [InlineData(null)]
        public void Constructor_WhenParentValueTypeIsNotOrderedCollection_ThrowsArgumentException(Type valueType)
        {
            // Arrange：非集合、无序集合和多维数组都没有按下标读写的投影语义。
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(valueType);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(
                () => new CollectionElementBinding(parentMock.Object, 0));
            Assert.Equal("parent", exception.ParamName);
        }

        [Theory]
        [InlineData(typeof(List<int>), true)]
        [InlineData(typeof(int[]), true)]
        [InlineData(typeof(IList<int>), true)]
        [InlineData(typeof(ICollection<int>), false)]
        [InlineData(typeof(HashSet<int>), false)]
        [InlineData(typeof(IEnumerable<int>), false)]
        [InlineData(typeof(int[,]), false)]
        [InlineData(typeof(string), false)]
        [InlineData(null, false)]
        public void IsOrderedCollectionType_WhenGivenVariousTypes_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Act
            var result = CollectionElementBinding.IsOrderedCollectionType(valueType, out var elementType);

            // Assert
            Assert.Equal(expected, result);
            if (expected)
            {
                Assert.Equal(typeof(int), elementType);
            }
            else if (valueType == null)
            {
                // 判定失败时元素类型输出不属于契约（string、多维数组等仍会推导出元素类型），仅类型缺失时必为 null。
                Assert.Null(elementType);
            }
        }

        [Fact]
        public void ElementView_WhenParentProvided_ExposesElementTypesKeysAndDisplayInfo()
        {
            // Arrange
            var name = new Translator("名称", "Name");
            var parentMock = CreateParentMock(new List<int> { 1, 2 });
            parentMock.SetupGet(x => x.Name).Returns(name);

            // Act
            var element0 = new CollectionElementBinding(parentMock.Object, 0);
            var element1 = new CollectionElementBinding(parentMock.Object, 1);

            // Assert：元素值类型为集合元素类型，名称透传父条目，键彼此不同且不与父键冲突。
            Assert.Equal(typeof(int), element0.ValueType);
            Assert.Same(name, element0.Name);
            Assert.Equal("CollectionEntry", parentMock.Object.Key);
            Assert.Equal("\0Coll_CollectionEntry_0", element0.Key);
            Assert.Equal("\0Coll_CollectionEntry_1", element1.Key);
            Assert.NotEqual(element0.Key, element1.Key);
            Assert.NotNull(element0.Description);
            Assert.Equal(string.Empty, element0.Description.Chinese);
            Assert.Equal(string.Empty, element0.Description.English);
            Assert.Null(element0.Metadata);
            Assert.NotNull(element0.EditBuffer);
            Assert.NotSame(element0.EditBuffer, element1.EditBuffer);
        }

        [Fact]
        public void Value_Get_WhenParentHasCommittedCollection_ReturnsElementAtIndex()
        {
            // Arrange
            var parentMock = CreateParentMock(new List<string> { "a", "b", "c" });

            // Act & Assert
            Assert.Equal("a", new CollectionElementBinding(parentMock.Object, 0).Value);
            Assert.Equal("c", new CollectionElementBinding(parentMock.Object, 2).Value);
        }

        [Fact]
        public void Value_Get_WhenParentValueIsNull_ReturnsNull()
        {
            // Arrange：父条目值缺失属于异常状态，元素读取降级为 null 而不抛出。
            var parentMock = CreateParentMock(null);

            // Act & Assert
            Assert.Null(new CollectionElementBinding(parentMock.Object, 0).Value);
        }

        [Fact]
        public void Value_Get_WhenIndexOutsideCollection_ReturnsNull()
        {
            // Arrange：下标越界同样降级返回 null；正常流程中越界投影会由集合编辑器重建。
            var parentMock = CreateParentMock(new List<int> { 1 });

            // Act & Assert
            Assert.Null(new CollectionElementBinding(parentMock.Object, 5).Value);
        }

        [Fact]
        public void Value_Get_WhenParentCollectionUnchanged_ReturnsSameBoxedElementInstance()
        {
            // Arrange：值类型元素经非泛型枚举每次都会重新装箱；按父集合实例缓存后，
            // 同一集合实例下重复读取必须返回同一引用，供元组编辑器以引用比较识别外部写入
            //（回归：不缓存时 list<(string, bool)> 的字符串列每帧被误判外部写入，暂存输入被清空导致无法输入）。
            var parentMock = CreateParentMock(new List<(string, bool)> { ("a", true) });
            var element0 = new CollectionElementBinding(parentMock.Object, 0);

            // Act
            var first = element0.Value;
            var second = element0.Value;

            // Assert
            Assert.Same(first, second);
            Assert.Equal(("a", true), first);
        }

        [Fact]
        public void Value_Get_WhenParentCollectionReplaced_ReturnsElementFromNewInstance()
        {
            // Arrange：父集合被整体替换后读取缓存失效，读取反映新实例的内容。
            var parentMock = CreateParentMock(new List<(string, bool)> { ("a", true) });
            var element0 = new CollectionElementBinding(parentMock.Object, 0);
            Assert.Equal(("a", true), element0.Value);

            // Act：元素写入复制父集合并构造新实例整体写回，父集合实例随之更换。
            element0.Value = ("b", false);

            // Assert
            Assert.Equal(("b", false), element0.Value);
        }

        [Fact]
        public void Value_Set_ReplacesOnlyTargetElementWithNewCollectionInstance()
        {
            // Arrange
            var original = new List<int> { 1, 2, 3 };
            var parentMock = CreateParentMock(original);
            var element1 = new CollectionElementBinding(parentMock.Object, 1);

            // Act
            element1.Value = 9;

            // Assert：只替换目标下标，其余元素保留，父条目收到新集合实例而非原地修改。
            var result = Assert.IsType<List<int>>(parentMock.Object.Value);
            Assert.NotSame(original, result);
            Assert.Equal(new[] { 1, 9, 3 }, result);
            Assert.Equal(new[] { 1, 2, 3 }, original);
        }

        [Fact]
        public void Value_SetOnArrayParent_ProducesNewArrayWithSameElementType()
        {
            // Arrange：数组条目的值类型精确匹配要求数组元素写入后仍是同元素类型的数组。
            var original = new[] { 1, 2 };
            var parentMock = CreateParentMock(original, typeof(int[]));
            var element0 = new CollectionElementBinding(parentMock.Object, 0);

            // Act
            element0.Value = 7;

            // Assert
            var result = Assert.IsType<int[]>(parentMock.Object.Value);
            Assert.NotSame(original, result);
            Assert.Equal(new[] { 7, 2 }, result);
        }

        [Fact]
        public void Value_SetOnInterfaceTypedParent_ProducesListInstance()
        {
            // Arrange：接口声明的条目无法实例化，元素写入统一生成 List<T> 具体实现。
            var parentMock = CreateParentMock(new List<int> { 1 }, typeof(IList<int>));
            var element0 = new CollectionElementBinding(parentMock.Object, 0);

            // Act
            element0.Value = 7;

            // Assert
            var result = Assert.IsType<List<int>>(parentMock.Object.Value);
            Assert.Equal(new[] { 7 }, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-number")]
        public void Value_SetInvalidValue_ThrowsArgumentException(object invalidValue)
        {
            // Arrange：与 EntryBinding.Value 相同的赋值约束——null 或不可赋值类型拒绝写入，不带参数名。
            var parentMock = CreateParentMock(new List<int> { 1 });
            var element0 = new CollectionElementBinding(parentMock.Object, 0);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => element0.Value = invalidValue);
        }

        [Fact]
        public void Value_SetWhenIndexOutsideCollection_ThrowsInvalidOperationException()
        {
            // Arrange：父集合短于记录下标说明投影已过期（正常由编辑器重建保证不会发生），写入显式失败。
            var parentMock = CreateParentMock(new List<int> { 1 });
            var element5 = new CollectionElementBinding(parentMock.Object, 5);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => element5.Value = 9);
        }

        [Fact]
        public void Value_SetInvokesOnParentValueWrittenCallback()
        {
            // Arrange
            var parentMock = CreateParentMock(new List<int> { 1 });
            var callbackCount = 0;
            var element0 = new CollectionElementBinding(parentMock.Object, 0, () => callbackCount++);

            // Act
            element0.Value = 7;

            // Assert
            Assert.Equal(1, callbackCount);
        }

        [Fact]
        public void CopyElements_WhenCollectionIsNull_ReturnsEmptyArray()
        {
            // Act & Assert
            Assert.Empty(CollectionElementBinding.CopyElements(null));
        }

        [Fact]
        public void CopyElements_WhenCollectionHasElements_ReturnsSnapshot()
        {
            // Arrange
            var collection = new List<int> { 1, 2 };

            // Act
            var elements = CollectionElementBinding.CopyElements(collection);

            // Assert
            Assert.Equal(new object[] { 1, 2 }, elements);
        }

        [Fact]
        public void CreateCollection_WhenAnyArgumentIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => CollectionElementBinding.CreateCollection(null, typeof(int), Array.Empty<object>()));
            Assert.Throws<ArgumentNullException>(() => CollectionElementBinding.CreateCollection(typeof(List<int>), null, Array.Empty<object>()));
            Assert.Throws<ArgumentNullException>(() => CollectionElementBinding.CreateCollection(typeof(List<int>), typeof(int), null));
        }

        [Fact]
        public void CreateCollection_WhenTargetHasNoDefaultConstructor_ThrowsNotSupportedException()
        {
            // Arrange：没有公开无参构造的有序集合类型没有可用的创建路径。
            var elements = new object[] { 1 };

            // Act & Assert
            Assert.Throws<NotSupportedException>(
                () => CollectionElementBinding.CreateCollection(typeof(NoDefaultConstructorList), typeof(int), elements));
        }

        /// <summary>
        /// 创建值为指定集合的父条目严格替身；值用 SetupProperty 提供可读写的已提交值存储。
        /// </summary>
        private static Mock<IEntryBinding> CreateParentMock(object value, Type valueType = null)
        {
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(valueType ?? value?.GetType() ?? typeof(List<int>));
            parentMock.SetupProperty(x => x.Value, value);
            parentMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            parentMock.SetupGet(x => x.Key).Returns("CollectionEntry");
            return parentMock;
        }

        /// <summary>只有私有构造函数的有序集合类型，用于验证无可用创建路径时的失败行为。</summary>
        private sealed class NoDefaultConstructorList : List<int>
        {
            private NoDefaultConstructorList()
            {
            }
        }
    }
}
