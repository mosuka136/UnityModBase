using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class GroupEditorTests
    {
        [Fact]
        public void Constructor_ValidDependencies_InitializesDependenciesAndEditorsWithoutContext()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleResource = new StyleResource(null);

            // Act
            var editor = new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                styleResource);

            // Assert
            Assert.Same(unityProvider.Object, editor.UnityService);
            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Same(styleResource, editor.StyleProvider);
            Assert.NotNull(editor.EditorRegistry);
            Assert.NotNull(editor.EntryEditor);
            Assert.Same(editor.EditorRegistry, editor.EntryEditor.Registry);
            Assert.Same(unityGui.Object, editor.EntryEditor.UnityGui);
            Assert.IsType<BooleanEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(bool)).Object));
            Assert.IsType<StringEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(string)).Object));
            Assert.IsType<SliderEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(int), metadata: new UiSliderMetadata(0f, 10f, 1f)).Object));
            Assert.IsType<NumberEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(float)).Object));
            Assert.IsType<EnumEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(TestEnum)).Object));
            Assert.Same(editor.HotkeyEditor, editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(Hotkey)).Object));
        }

        [Fact]
        public void Update_WhenDifferentContextIsPassed_FlushesOnlyThatContextsPendingValue()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var firstContext = new GuiContext();
            var secondContext = new GuiContext();
            var firstEntry = CreateEntryBindingMock(typeof(string), value: "first-old", key: "FirstEntry");
            var secondEntry = CreateEntryBindingMock(typeof(string), value: "second-old", key: "SecondEntry");
            firstEntry.SetupSet(x => x.Value = "first-new");
            secondEntry.SetupSet(x => x.Value = "second-new");
            var editor = new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                new StyleResource(null));
            firstContext.ChangeSink.SetValue(firstEntry.Object, "first-new", delay: 0.5f);
            secondContext.ChangeSink.SetValue(secondEntry.Object, "second-new", delay: 0.5f);

            // Act
            editor.Update(secondContext, 0.5f);

            // Assert
            firstEntry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            secondEntry.VerifySet(x => x.Value = "second-new", Times.Once);
        }

        private static Mock<IEntryBinding> CreateEntryBindingMock(System.Type valueType, object value = null, IUiMetadata metadata = null, string key = "Entry")
        {
            var mock = new Mock<IEntryBinding>(MockBehavior.Strict);
            mock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            mock.SetupGet(x => x.Key).Returns(key);
            mock.SetupGet(x => x.ValueType).Returns(valueType);
            mock.SetupGet(x => x.Value).Returns(value);
            mock.SetupGet(x => x.Metadata).Returns(metadata);
            return mock;
        }

        private enum TestEnum
        {
            One,
            Two,
        }
    }
}
