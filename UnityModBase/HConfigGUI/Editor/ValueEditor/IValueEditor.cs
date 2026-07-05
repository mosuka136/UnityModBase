using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public interface IValueEditor
    {
        bool CanEdit(IEntryBinding entry);
        void DrawValue(IEntryBinding entry, GuiContext context);
        void DrawExtra(IEntryBinding entry, GuiContext context);
    }
}
