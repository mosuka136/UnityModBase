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

        public float RearBlankWidth => UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.Reset)).x;
        public IUnityGuiProvider UnityGui { get; }

        public EntryEditor(ValueEditorRegistry registry, IUnityGuiProvider unity)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry), "Registry cannot be null.");
            UnityGui = unity ?? throw new ArgumentNullException(nameof(unity), "UnityGui cannot be null.");
        }

        public void Render(IEntryBinding entry, GuiContext context)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");
            
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Invalid GuiContext provided to {nameof(Render)}.");
                return;
            }

            var editor = Registry.GetEditor(entry);

            context.SetFloat(GuiContext.RearBlankWidthKey, RearBlankWidth);

            UnityGui.BeginHorizontal();
            UnityGui.Label(UnityGui.GetContent(entry.Name, entry.Description), UnityGui.Width(context.GetFloat(GuiContext.LeadingBlankWidthKey)));
            editor.DrawValue(entry, context);
            if (UnityGui.Button(TranslatorResource.Reset, UnityGui.ExpandWidth(false)))
                context.ChangeSink.ResetValue(entry);
            UnityGui.EndHorizontal();

            editor.DrawExtra(entry, context);
        }
    }
}
