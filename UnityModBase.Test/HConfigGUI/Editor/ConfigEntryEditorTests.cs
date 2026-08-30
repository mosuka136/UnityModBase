using System.Reflection;
using Moq;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using ValueEditorRegistry = UnityModBase.HGuiSpace.Editor.ValueEditorRegistry;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class ConfigEntryEditorTests
    {
        [Fact]
        public void DrawTrailingAction_WhenResetClicked_ResetsOnlyResettableEntry()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var entry = new Mock<IResettableEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.Setup(x => x.ResetValue());
            unityGui.Setup(x => x.Button(It.IsAny<string>(), It.IsAny<UnityEngine.GUILayoutOption[]>())).Returns(true);
            unityGui.Setup(x => x.ExpandWidth(false)).Returns((UnityEngine.GUILayoutOption)null);
            using var registry = new ValueEditorRegistry();
            var editor = new EntryEditor(unityGui.Object, registry);

            var method = typeof(EntryEditor).GetMethod(
                "DrawTrailingAction",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(editor, new object[] { entry.Object, new GuiContext() });

            entry.Verify(x => x.ResetValue(), Times.Once);
        }
    }
}
