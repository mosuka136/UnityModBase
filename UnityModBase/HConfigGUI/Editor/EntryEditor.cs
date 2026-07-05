using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class EntryEditor
    {
        public ValueEditorRegistry Registry { get; }
        public GuiContext Context { get; }

        public float RearBlankWidth => UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.Reset)).x;
        public IUnityGuiProvider UnityGui { get; }

        public EntryEditor(ValueEditorRegistry registry, GuiContext context, IUnityGuiProvider unity)
        {
            Registry = registry;
            Context = context;
            UnityGui = unity;

            GuiPipe.OnEntryValueChanged += e => { Context.DeleteText(e); Context.DeleteBool(e); };
            GuiPipe.OnEntryEditFinished += e => { Context.DeleteText(e); Context.DeleteBool(e); };
            GuiPipe.OnEntryValueReset += e => { Context.DeleteText(e); Context.DeleteBool(e); };
        }

        public void Render(IEntryBinding entry)
        {
            var editor = Registry.GetEditor(entry);

            if (UnityGui == null || entry == null)
                return;

            Context.SetFloat(GuiContext.RearBlankWidthKey, RearBlankWidth);

            UnityGui.BeginHorizontal();
            UnityGui.Label(UnityGui.GetContent(entry.Name, entry.Description), UnityGui.Width(Context.GetFloat(GuiContext.LeadingBlankWidthKey)));
            editor.DrawValue(entry, Context);
            if (UnityGui.Button(TranslatorResource.Reset, UnityGui.ExpandWidth(false)))
                Context.ChangeSink.ResetValue(entry);
            UnityGui.EndHorizontal();

            editor.DrawExtra(entry, Context);
        }
    }
}
