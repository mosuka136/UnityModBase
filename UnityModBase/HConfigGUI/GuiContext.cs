using System;
using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI
{
    public class GuiContext : IUserContext
    {
        private readonly Dictionary<string, float> _entryLabelWidth = new Dictionary<string, float>();

        public readonly static GuiContext InvalidGuiContext = new GuiContext();
        public bool IsValid => !ReferenceEquals(this, InvalidGuiContext);

        public EntryChangeSink ChangeSink { get; private set; }
        public GroupBinding UserData { get; set; }

        public PopupState Popup { get; private set; }
        public string ExpandedEnumKey { get; set; } = string.Empty;
        public string SelectedGroupKey { get; set; } = string.Empty;

        public float GroupButtonWidth { get; set; } = -1f;
        public float ResetButtonWidth { get; set; } = -1f;

        public bool IsEntryLabelWidthDirty { get; set; } = true;
        public bool IsGroupButtonWidthDirty { get; set; } = true;
        public bool IsResetButtonWidthDirty { get; set; } = true;

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

        public float GetEntryLabelWidth(string key, float defaultValue = 0f)
        {
            if (_entryLabelWidth.TryGetValue(key, out var value))
                return value;
            _entryLabelWidth[key] = defaultValue;
            return defaultValue;
        }

        public void SetEntryLabelWidth(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            _entryLabelWidth[key] = value;
        }

        public void SetLayoutDirtyFlags(bool entryLabelWidthDirty = true, bool groupButtonWidthDirty = true, bool resetButtonWidthDirty = true)
        {
            IsEntryLabelWidthDirty = entryLabelWidthDirty;
            IsGroupButtonWidthDirty = groupButtonWidthDirty;
            IsResetButtonWidthDirty = resetButtonWidthDirty;
        }

        public void Dispose()
        {
        }
    }
}
