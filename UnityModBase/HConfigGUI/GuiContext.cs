using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI
{
    public class GuiContext : IUserContext
    {
        public const string Separator = "+";
        public static string LeadingBlankWidthKey => "LeadingBlankWidth";
        public static string RearBlankWidthKey => "RearBlankWidth";

        private readonly Dictionary<string, float> _floatState = new Dictionary<string, float>();

        public readonly static GuiContext InvalidGuiContext = new GuiContext();
        public bool IsValid => !ReferenceEquals(this, InvalidGuiContext);

        public EntryChangeSink ChangeSink { get; private set; }
        public GroupBinding UserData { get; set; }

        public PopupState Popup { get; private set; }
        public string ExpandedEnumKey { get; set; } = string.Empty;
        public string SelectedGroupKey { get; set; } = string.Empty;
        public float EntryLabelWidth { get; set; } = -1f;
        public float GroupButtonWidth { get; set; } = -1f;

        public GuiContext()
        {
            ChangeSink = new EntryChangeSink();
            Popup = new PopupState();
        }

        public class PopupState
        {
            public bool IsOpen { get; set; } = false;
            public Translator Title { get; set; }
            public Action DrawAction { get; set; }
            public Action CloseAction { get; set; }
        }

        public string GetKey(IEntryBinding entry, string suffix = "")
        {
            return entry.Key + Separator + suffix;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (_floatState.TryGetValue(key, out var value))
                return value;
            _floatState[key] = defaultValue;
            return defaultValue;
        }

        public void SetFloat(string key, float value)
        {
            _floatState[key] = value;
        }

        public void DeleteFloat(string key)
        {
            if (_floatState.ContainsKey(key))
                _floatState.Remove(key);
        }

        public void DeleteFloat(IEntryBinding entry)
        {
            DeleteEntryState(_floatState, entry);
        }

        private static void DeleteEntryState<T>(Dictionary<string, T> state, IEntryBinding entry)
        {
            if (string.IsNullOrEmpty(entry?.Key))
                return;

            var keys = state.Keys.Where(key => key.StartsWith(entry.Key)).ToList();
            foreach (var key in keys)
                state.Remove(key);
        }

        public void Dispose()
        {
        }
    }
}
