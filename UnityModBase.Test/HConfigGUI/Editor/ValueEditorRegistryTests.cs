using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    /// <summary>
    /// 注册表为全进程共享的静态状态；每个测试开始前先释放并清空，保证匹配顺序断言不受其他测试残留影响。
    /// </summary>
    public class ValueEditorRegistryTests
    {
        public ValueEditorRegistryTests()
        {
            ValueEditorRegistry.Dispose();
        }

        [Fact]
        public void RegisterEditor_WhenRegisteredEditorCanEdit_ReturnsRegisteredEditor()
        {
            // Arrange
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            editorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);
            ValueEditorRegistry.RegisterEditor(editorMock.Object);

            // Act
            var result = ValueEditorRegistry.GetEditor(entryMock.Object);

            // Assert
            Assert.Same(editorMock.Object, result);
            editorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            editorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetEditor_WhenFirstRegisteredEditorCannotEdit_ReturnsFirstLaterMatchingEditor()
        {
            // Arrange
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var firstEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            var secondEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            firstEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(false);
            secondEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);
            ValueEditorRegistry.RegisterEditor(firstEditorMock.Object);
            ValueEditorRegistry.RegisterEditor(secondEditorMock.Object);

            // Act
            var result = ValueEditorRegistry.GetEditor(entryMock.Object);

            // Assert
            Assert.Same(secondEditorMock.Object, result);
            firstEditorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            secondEditorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            firstEditorMock.VerifyNoOtherCalls();
            secondEditorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetEditor_WhenNoRegisteredEditorCanEdit_ReturnsUnsupportedEditorInstance()
        {
            // Arrange
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var firstEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            var secondEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            firstEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(false);
            secondEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(false);
            ValueEditorRegistry.RegisterEditor(firstEditorMock.Object);
            ValueEditorRegistry.RegisterEditor(secondEditorMock.Object);

            // Act
            var result = ValueEditorRegistry.GetEditor(entryMock.Object);

            // Assert
            Assert.Same(UnsupportedEditor.Default, result);
            firstEditorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            secondEditorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            firstEditorMock.VerifyNoOtherCalls();
            secondEditorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void RegisterEditor_WhenEditorIsNull_ThrowsArgumentNullException()
        {
            // Arrange

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => ValueEditorRegistry.RegisterEditor(null));

            // Assert
            Assert.Equal("editor", exception.ParamName);
        }

        [Fact]
        public void GetEditor_WhenEntryIsNull_ReturnsUnsupportedEditorWithoutConsultingEditors()
        {
            // Arrange：严格模拟未设置 CanEdit，若注册表误咨询它将直接抛出 MockException。
            var editorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            ValueEditorRegistry.RegisterEditor(editorMock.Object);

            // Act
            var result = ValueEditorRegistry.GetEditor(null);

            // Assert
            Assert.Same(UnsupportedEditor.Default, result);
            editorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Dispose_WhenEditorDisposeThrows_StillDisposesRemainingEditorsAndClearsRegistry()
        {
            // Arrange
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var throwingEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            var survivingEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            throwingEditorMock.Setup(x => x.Dispose()).Throws(new InvalidOperationException("dispose failed"));
            survivingEditorMock.Setup(x => x.Dispose());
            ValueEditorRegistry.RegisterEditor(throwingEditorMock.Object);
            ValueEditorRegistry.RegisterEditor(survivingEditorMock.Object);

            // Act
            ValueEditorRegistry.Dispose();

            // Assert
            throwingEditorMock.Verify(x => x.Dispose(), Times.Once);
            survivingEditorMock.Verify(x => x.Dispose(), Times.Once);
            Assert.Same(UnsupportedEditor.Default, ValueEditorRegistry.GetEditor(entryMock.Object));
        }

        [Fact]
        public void RegisterEditor_AfterDispose_MatchesNewlyRegisteredEditor()
        {
            // Arrange
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            var staleEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            staleEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);
            ValueEditorRegistry.RegisterEditor(staleEditorMock.Object);
            ValueEditorRegistry.Dispose();
            var freshEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            freshEditorMock.Setup(x => x.CanEdit(entryMock.Object)).Returns(true);

            // Act
            ValueEditorRegistry.RegisterEditor(freshEditorMock.Object);

            // Assert
            Assert.Same(freshEditorMock.Object, ValueEditorRegistry.GetEditor(entryMock.Object));
            freshEditorMock.Verify(x => x.CanEdit(entryMock.Object), Times.Once);
            freshEditorMock.VerifyNoOtherCalls();
        }
    }
}
