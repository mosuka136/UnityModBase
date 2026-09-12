using Moq;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.Test.HGuiSpace.Editor
{
    public class ValueEditorRegistryTests : IDisposable
    {
        private readonly ValueEditorRegistry _registry = new ValueEditorRegistry();

        [Fact]
        public void RegisterEditor_WhenRegisteredEditorCanEdit_ReturnsRegisteredEditor()
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editor = new Mock<IValueEditor>(MockBehavior.Strict);
            editor.Setup(x => x.CanEdit(entry.Object)).Returns(true);
            _registry.RegisterEditor(editor.Object);

            Assert.Same(editor.Object, _registry.GetEditor(entry.Object));
            editor.Verify(x => x.CanEdit(entry.Object), Times.Once);
            editor.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetEditor_WhenFirstCannotEdit_ReturnsFirstLaterMatch()
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var first = new Mock<IValueEditor>(MockBehavior.Strict);
            var second = new Mock<IValueEditor>(MockBehavior.Strict);
            first.Setup(x => x.CanEdit(entry.Object)).Returns(false);
            second.Setup(x => x.CanEdit(entry.Object)).Returns(true);
            _registry.RegisterEditor(first.Object);
            _registry.RegisterEditor(second.Object);

            Assert.Same(second.Object, _registry.GetEditor(entry.Object));
        }

        [Fact]
        public void GetEditor_WhenNoEditorMatches_ReturnsUnsupportedEditor()
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var editor = new Mock<IValueEditor>(MockBehavior.Strict);
            editor.Setup(x => x.CanEdit(entry.Object)).Returns(false);
            _registry.RegisterEditor(editor.Object);

            Assert.Same(UnsupportedEditor.Default, _registry.GetEditor(entry.Object));
        }

        [Fact]
        public void RegisterEditor_WhenEditorIsNull_ThrowsArgumentNullException()
        {
            Assert.Equal("editor", Assert.Throws<ArgumentNullException>(() => _registry.RegisterEditor(null)).ParamName);
        }

        [Fact]
        public void GetEditor_WhenEntryIsNull_DoesNotConsultEditors()
        {
            var editor = new Mock<IValueEditor>(MockBehavior.Strict);
            _registry.RegisterEditor(editor.Object);

            Assert.Same(UnsupportedEditor.Default, _registry.GetEditor(null));
            editor.VerifyNoOtherCalls();
        }

        [Fact]
        public void Dispose_WhenOneEditorThrows_DisposesRemainingAndClearsOnlyThisRegistry()
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var throwing = new Mock<IValueEditor>(MockBehavior.Strict);
            var surviving = new Mock<IValueEditor>(MockBehavior.Strict);
            throwing.Setup(x => x.Dispose()).Throws(new InvalidOperationException("dispose failed"));
            surviving.Setup(x => x.Dispose());
            _registry.RegisterEditor(throwing.Object);
            _registry.RegisterEditor(surviving.Object);

            _registry.Dispose();

            throwing.Verify(x => x.Dispose(), Times.Once);
            surviving.Verify(x => x.Dispose(), Times.Once);
            Assert.Same(UnsupportedEditor.Default, _registry.GetEditor(entry.Object));
        }

        [Fact]
        public void TwoRegistries_WhenOneIsDisposed_OtherRemainsUsable()
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            var firstEditor = new Mock<IValueEditor>(MockBehavior.Strict);
            var secondEditor = new Mock<IValueEditor>(MockBehavior.Strict);
            firstEditor.Setup(x => x.Dispose());
            secondEditor.Setup(x => x.CanEdit(entry.Object)).Returns(true);
            secondEditor.Setup(x => x.Dispose());
            var other = new ValueEditorRegistry();
            _registry.RegisterEditor(firstEditor.Object);
            other.RegisterEditor(secondEditor.Object);

            _registry.Dispose();

            Assert.Same(secondEditor.Object, other.GetEditor(entry.Object));
            other.Dispose();
        }

        [Fact]
        public void CreateDefault_WhenEntryIsOrderedCollection_ResolvesCollectionEditor()
        {
            // Arrange：默认注册表应把有序集合条目路由到集合编辑器（位于双元素编辑器之前注册）。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(System.Collections.Generic.List<int>));
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            using (var registry = ValueEditorRegistry.CreateDefault(
                new Mock<IUnityProvider>(MockBehavior.Strict).Object,
                unityGuiMock.Object,
                new Mock<IEntryStyleResource>(MockBehavior.Strict).Object))
            {
                // Act & Assert
                Assert.IsType<CollectionEditor>(registry.GetEditor(entryMock.Object));
            }
        }

        [Fact]
        public void CreateDefault_WhenEntryIsSupportedTuple_ResolvesTupleEditor()
        {
            // Arrange：默认注册表应把封闭元组条目路由到元组编辑器（位于双元素编辑器之前注册）。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof((int, string)));
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            using (var registry = ValueEditorRegistry.CreateDefault(
                new Mock<IUnityProvider>(MockBehavior.Strict).Object,
                unityGuiMock.Object,
                new Mock<IEntryStyleResource>(MockBehavior.Strict).Object))
            {
                // Act & Assert
                Assert.IsType<TupleEditor>(registry.GetEditor(entryMock.Object));
            }
        }

        public void Dispose()
        {
            _registry.Dispose();
        }
    }
}
