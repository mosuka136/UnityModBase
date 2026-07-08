using System;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class EntryEditor
    {
        public ValueEditorRegistry Registry { get; }
        public IUnityGuiProvider UnityGui { get; }

        public EntryEditor(ValueEditorRegistry registry, IUnityGuiProvider unity)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry), "Registry cannot be null.");
            UnityGui = unity ?? throw new ArgumentNullException(nameof(unity), "UnityGui cannot be null.");
        }

        public void Draw(IEntryBinding entry, GuiContext context)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");
            
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Invalid GuiContext provided to {nameof(Draw)}.");
                return;
            }

            var editor = Registry.GetEditor(entry);

            UnityGui.BeginHorizontal();
            UnityGui.Label(UnityGui.GetContent(entry.Name, entry.Description), UnityGui.Width(context.GetEntryLabelWidth(context.SelectedGroupKey)));
            editor.DrawValue(entry, context);
            if (UnityGui.Button(TranslatorResource.Reset, UnityGui.ExpandWidth(false)))
                context.ChangeSink.ResetValue(entry);
            UnityGui.EndHorizontal();

            editor.DrawExtra(entry, context);
        }
    }
}
