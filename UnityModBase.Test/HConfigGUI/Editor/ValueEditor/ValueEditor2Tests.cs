using System;
using System.Collections.Generic;
using Moq;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigSpace;
using UnityModBase.HProvider;

namespace UnityModBase.Test.HConfigGUI.Editor.ValueEditor
{
    /// <summary>
    /// 双元素编辑器当前只提交了匹配逻辑：按绑定值类型是否为封闭的 ConfigEntryValue&lt;,&gt; 泛型识别，
    /// 绘制方法尚未实现，调用即抛 NotImplementedException（见源码 TODO）。
    /// </summary>
    public class ValueEditor2Tests
    {
        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ValueEditor_2(null));
            Assert.Equal("unityGui", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithUnityGui_StoresProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new ValueEditor_2(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Theory]
        [InlineData(typeof(ConfigEntryValue<int, float>), true)]
        [InlineData(typeof(ConfigEntryValue<string, string>), true)]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(KeyValuePair<int, string>), false)]
        public void CanEdit_WhenEntryValueTypeIsClosedConfigEntryValueGeneric_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange：只识别 ConfigEntryValue<,> 的封闭泛型，其他泛型（如 KeyValuePair<,>）与非泛型均不可编辑。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CanEdit_WhenEntryIsNull_ReturnsFalse()
        {
            // Arrange
            var editor = CreateEditor();

            // Act
            var result = editor.CanEdit(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Draw_WhenCalled_ThrowsNotImplementedException()
        {
            // Arrange：占位实现的既定契约——绘制路径尚未实现，防止在补齐前被静默接入。
            var editor = CreateEditor();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => editor.DrawValue(null, null));
            Assert.Throws<NotImplementedException>(() => editor.DrawExtra(null, null));
        }

        private static ValueEditor_2 CreateEditor()
        {
            return new ValueEditor_2(new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object);
        }
    }
}
