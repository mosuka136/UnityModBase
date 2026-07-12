using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class UnsupportedEditor : IValueEditor
    {
        public IUnityGuiProvider UnityGui { get; }

        public static UnsupportedEditor Instance = new UnsupportedEditor();

        public UnsupportedEditor()
        {
            UnityGui = UnityGuiProvider.Instance;
        }

        public bool CanEdit(IEntryBinding entry)
        {
            return false;
        }

        public void DrawValue(IEntryBinding entry, GuiContext contextk)
        {
            UnityGui.Label("Unsupported type: " + entry.ValueType.FullName);
        }

        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
        }

        public void Dispose()
        {
        }
    }
}
