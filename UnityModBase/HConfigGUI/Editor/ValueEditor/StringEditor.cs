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
            var key = context.GetKey(entry, "_string");
            var value = context.GetText(key, entry.Value as string);
            if (value == null)
                return;

            string newValue = UnityGui.TextField(value, UnityGui.ExpandWidth(true));
            if (newValue != value)
            {
                context.SetText(key, newValue);
                context.ChangeSink.SetValue(entry, newValue, true, DelayApplyDuration);
            }
        }

        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
        }
    }
}
