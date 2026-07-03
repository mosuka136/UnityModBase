using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class BooleanEditor : IValueEditor
    {
        public IUnityGuiProvider UnityGui { get; }

        public BooleanEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        public bool CanEdit(IEntryBinding entry)
        {
            return entry.ValueType == typeof(bool);
        }

        public void DrawValue(IEntryBinding entry, GuiStateStore state, EntryChangeSink changeSink)
        {
            if (entry.Value is bool value)
            {
                bool newValue = UnityGui.Toggle(value, value ? TranslatorResource.On : TranslatorResource.Off, UnityGui.ExpandWidth(true));
                if (newValue != value)
                    changeSink.SetValue(entry, newValue);
            }
        }

        public void DrawExtra(IEntryBinding entry, GuiStateStore state, EntryChangeSink changeSink)
        {
        }
    }
}
