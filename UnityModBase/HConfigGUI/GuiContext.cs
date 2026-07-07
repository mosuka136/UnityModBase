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

        private readonly Dictionary<string, string> _textState = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _intState = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _floatState = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> _boolState = new Dictionary<string, bool>();

        public readonly static GuiContext InvalidGuiContext = new GuiContext();
        public bool IsValid => !ReferenceEquals(this, InvalidGuiContext);

        public EntryChangeSink ChangeSink { get; private set; }
        public GroupBinding UserData { get; set; }

        public PopupState Popup { get; private set; }
        public string ExpandedEnumKey { get; set; } = string.Empty;
        public string SelectedGroupKey { get; set; } = string.Empty;
        public string ToastMessage { get; set; } = string.Empty;
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

        public string GetText(string key, string defaultValue = "")
        {
            if (_textState.TryGetValue(key, out var value))
                return value;
            _textState[key] = defaultValue;
            return defaultValue;
        }

        public void SetText(string key, string value)
        {
            _textState[key] = value;
        }

        public void DeleteText(string key)
        {
            _textState.Remove(key);
        }

        public void DeleteText(IEntryBinding entry)
        {
            DeleteEntryState(_textState, entry);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (_boolState.TryGetValue(key, out var value))
                return value;
            _boolState[key] = defaultValue;
            return defaultValue;
        }

        public void SetBool(string key, bool value)
        {
            _boolState[key] = value;
        }

        public void DeleteBool(string key)
        {
            _boolState.Remove(key);
        }

        public void DeleteBool(IEntryBinding entry)
        {
            DeleteEntryState(_boolState, entry);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (_intState.TryGetValue(key, out var value))
                return value;
            _intState[key] = defaultValue;
            return defaultValue;
        }

        public void SetInt(string key, int value)
        {
            _intState[key] = value;
        }

        public void DeleteInt(string key)
        {
            _intState.Remove(key);
        }

        public void DeleteInt(IEntryBinding entry)
        {
            DeleteEntryState(_intState, entry);
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
