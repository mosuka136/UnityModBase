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
        public void Constructor_ValidDependencies_InitializesPropertiesAndRegistersEditors()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var guiStateStore = new GuiStateStore();
            var styleResource = new StyleResource(null);
            var layoutResource = new LayoutResource(unityGui.Object);
            var changeSink = new EntryChangeSink();

            // Act
            var editor = new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                guiStateStore,
                styleResource,
                layoutResource,
                changeSink);

            // Assert
            Assert.Same(unityProvider.Object, editor.UnityService);
            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Same(guiStateStore, editor.GuiStateStore);
            Assert.Same(styleResource, editor.StyleProvider);
            Assert.Same(layoutResource, editor.LayoutProvider);
            Assert.NotNull(editor.EditorRegistry);
            Assert.Same(changeSink, editor.ChangeSink);
            Assert.NotNull(editor.EntryEditor);
            Assert.Same(editor.EditorRegistry, editor.EntryEditor.Registry);
            Assert.Same(guiStateStore, editor.EntryEditor.State);
            Assert.Same(editor.ChangeSink, editor.EntryEditor.ChangeSink);
            Assert.Same(unityGui.Object, editor.EntryEditor.UnityGui);
            Assert.IsType<BooleanEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(bool)).Object));
            Assert.IsType<StringEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(string)).Object));
            Assert.IsType<SliderEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(int), metadata: new UiSliderMetadata(0f, 10f, 1f)).Object));
            Assert.IsType<NumberEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(float)).Object));
            Assert.IsType<EnumEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(TestEnum)).Object));
            Assert.IsType<HotkeyEditor>(editor.EditorRegistry.GetEditor(CreateEntryBindingMock(typeof(Hotkey)).Object));
        }

        [Fact]
        public void Update_WhenPendingDelayedValueExists_FlushesValueToEntry()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var guiStateStore = new GuiStateStore();
            var styleResource = new StyleResource(null);
            var changeSink = new EntryChangeSink();
            var entry = CreateEntryBindingMock(typeof(string), value: "old", key: "DelayedEntry");
            entry.SetupGet(x => x.Value).Returns("old");
            entry.SetupSet(x => x.Value = "new");
            var editor = new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                guiStateStore,
                styleResource,
                new LayoutResource(unityGui.Object),
                changeSink);
            editor.ChangeSink.SetValue(entry.Object, "new", delay: 0.5f);

            // Act
            editor.Update(0.5f);

            // Assert
            entry.VerifySet(x => x.Value = "new", Times.Once);
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
