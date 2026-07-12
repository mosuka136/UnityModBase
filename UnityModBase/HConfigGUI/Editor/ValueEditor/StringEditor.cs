using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class StringEditor : IValueEditor
    {
        public float DelayApplyDuration { get; set; } = 0.5f;

        public IUnityGuiProvider UnityGui { get; }

        public StringEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        public bool CanEdit(IEntryBinding entry)
        {
            return entry.ValueType == typeof(string);
        }

        public void DrawValue(IEntryBinding entry, GuiContext context)
        {
            var value = ValueProvider.GetValidValue<string>(entry);
            var newValue = UnityGui.TextField(value, UnityGui.ExpandWidth(true));
            if (newValue != value)
                context.ChangeSink.SetValue(entry, newValue, delay: DelayApplyDuration);
        }

        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
        }

        public void Dispose()
        {
        }
    }
}
