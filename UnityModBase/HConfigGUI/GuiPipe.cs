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

        public static void InvokeOnEntryValueChanged(IEntryBinding entry)
        {
            foreach (var handler in (OnEntryValueChanged?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<IEntryBinding>>())
            {
                try
                {
                    handler?.Invoke(entry);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Error invoking OnEntryValueChanged handler: {handler?.Method.Name}", ex);
                }
            }
        }

        public static void InvokeOnEntryValueReset(IEntryBinding entry)
        {
            foreach (var handler in (OnEntryValueReset?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<IEntryBinding>>())
            {
                try
                {
                    handler?.Invoke(entry);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Error invoking OnEntryValueReset handler: {handler?.Method.Name}", ex);
                }
            }
        }
    }
}
