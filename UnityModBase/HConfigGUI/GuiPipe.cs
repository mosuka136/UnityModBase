using System;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI
{
    public static class GuiPipe
    {
        public static Translator PopupTitle { get; set; }
        public static Action PopupWindowAction { get; set; }
        public static Action ClosePopupWindowAction { get; set; }

        public static event Action<IEntryBinding> OnEntryValueChanged;
        public static event Action<IEntryBinding> OnEntryValueReset;
        public static event Action<IEntryBinding> OnEntryEditFinished;

        public static void InvokeOnEntryValueChanged(IEntryBinding entry)
        {
            InvokeHandlers(OnEntryValueChanged, entry, nameof(OnEntryValueChanged));
        }

        public static void InvokeOnEntryValueReset(IEntryBinding entry)
        {
            InvokeHandlers(OnEntryValueReset, entry, nameof(OnEntryValueReset));
        }

        public static void InvokeOnEntryEditFinished(IEntryBinding entry)
        {
            InvokeHandlers(OnEntryEditFinished, entry, nameof(OnEntryEditFinished));
        }

        private static void InvokeHandlers(Action<IEntryBinding> handlers, IEntryBinding entry, string eventName)
        {
            foreach (var handler in (handlers?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<IEntryBinding>>())
            {
                try
                {
                    handler?.Invoke(entry);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Error invoking {eventName} handler: {handler?.Method.Name}", ex);
                }
            }
        }
    }
}
