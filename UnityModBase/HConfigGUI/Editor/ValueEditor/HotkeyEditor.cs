using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class HotkeyEditor : IValueEditor
    {
        public HotkeyEditSession Session { get; }
        public IUnityGuiProvider UnityGui { get; }
        public StyleResource StyleProvider { get; }

        public HotkeyEditor(IUnityGuiProvider unityGui, StyleResource styleProvider)
        {
            Session = new HotkeyEditSession();
            UnityGui = unityGui;
            StyleProvider = styleProvider;
        }

        public bool CanEdit(IEntryBinding entry)
        {
            return typeof(Hotkey).IsAssignableFrom(entry.ValueType);
        }

        public void DrawValue(IEntryBinding entry, GuiContext context)
        {
            if (!typeof(Hotkey).IsAssignableFrom(entry.ValueType))
                return;

            var valueString = GetHotkeyDisplayString(entry);
            if (valueString == null)
                return;

            if (UnityGui.Button(valueString, UnityGui.ExpandWidth(true)))
            {
                if (Session.Entry == entry)
                    Session.ConfirmEdit(context.ChangeSink);
                else
                {
                    Session.ConfirmEdit(context.ChangeSink);
                    Session.BeginEdit(entry);
                }
            }
        }

        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
            if (Session.Entry != entry)
                return;

            Session.Update();

            var value = Session.WorkingValue;
            if (value == null)
                return;

            UnityGui.BeginHorizontal();
            UnityGui.Space(context.GetFloat(GuiContext.LeadingBlankWidthKey, 0f));
            UnityGui.BeginVertical(UnityGui.BoxStyle);

            var chords = new List<HotkeyChord>(value.Hotkeys);
            foreach (var chord in chords)
            {
                UnityGui.BeginHorizontal();

                UnityGui.Button(chord.ToString(), UnityGui.ExpandWidth(true));

                if (UnityGui.Button(TranslatorResource.Record, UnityGui.ExpandWidth(false)))
                {
                    chord.Clear();
                    Session.SetWorkingChord(chord, context.ChangeSink);
                    SetPopupWindow(context);
                }

                if (value.Count > 1 && UnityGui.Button(TranslatorResource.Remove, UnityGui.ExpandWidth(false)))
                    Session.RemoveChord(chord, context.ChangeSink);

                UnityGui.EndHorizontal();
            }

            UnityGui.BeginHorizontal();
            if (UnityGui.Button(TranslatorResource.Add, UnityGui.ExpandWidth(true)))
            {
                Session.AddChord(new HotkeyChord(value.UnityService), context.ChangeSink);
                SetPopupWindow(context);
            }
            UnityGui.EndHorizontal();

            UnityGui.EndVertical();
            UnityGui.EndHorizontal();
        }

        public void SetPopupWindow(GuiContext context)
        {
            context.Popup.IsOpen = true;
            context.Popup.Title = TranslatorResource.RecordHotkeyPopupTitle;
            context.Popup.DrawAction = () => RecordHotkey(context);
            context.Popup.CloseAction = Session.CancelRecord;
        }

        public void RecordHotkey(GuiContext context)
        {
            if (!Session.IsRecording)
                return;

            Session.Update();

            UnityGui.FlexibleSpace();
            UnityGui.Label(Session.WorkingChord.ToString(), StyleProvider.RecordingHotkeyLabelStyle, UnityGui.ExpandWidth(true));

            UnityGui.FlexibleSpace();
            if (UnityGui.Button(TranslatorResource.Apply, UnityGui.ExpandWidth(true)))
            {
                Session.ConfirmRecord(context.ChangeSink);
                context.Popup.IsOpen = false;
            }
        }

        public string GetHotkeyDisplayString(IEntryBinding entry)
        {
            if (Session.Entry == entry)
                return Session.WorkingValue?.ToString() ?? string.Empty;

            return ValueProvider.GetValidValue(entry)?.ToString();
        }
    }
}
