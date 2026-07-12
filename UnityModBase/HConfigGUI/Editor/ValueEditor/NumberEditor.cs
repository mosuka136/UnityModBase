using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class NumberEditor : IValueEditor
    {
        public float DelayApplyDuration { get; set; } = 0.5f;

        public IUnityGuiProvider UnityGui { get; }

        public NumberEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        public virtual bool CanEdit(IEntryBinding entry)
        {
            var type = entry.ValueType;
            return type.IsPrimitive && type != typeof(bool) && type != typeof(char);
        }

        public virtual void DrawValue(IEntryBinding entry, GuiContext context)
        {
            if (!entry.ValueType.IsPrimitive || entry.ValueType == typeof(bool) || entry.ValueType == typeof(char))
                return;

            var valueString = ValueProvider.GetValue(entry).ToString();
            string newValueString = UnityGui.TextField(valueString, UnityGui.ExpandWidth(true));
            if (valueString != newValueString)
                context.ChangeSink.SetConvertedValue(entry, newValueString, DelayApplyDuration);
        }

        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
        }

        public void Dispose()
        {
        }
    }
}
