using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.HConfigGUI
{
    public class EntryChangeSink
    {
        private readonly Dictionary<IEntryBinding, float> _pendingEntries = new Dictionary<IEntryBinding, float>();

        public void SetConvertedValue(IEntryBinding entry, object value, float delay = 0.0f)
        {
            if (entry == null)
                return;

            var isValid = TypeConvert.TryTo(value, entry.ValueType, out var convertedValue);
            SetValue(entry, isValid ? convertedValue : value, isValid, delay);
        }

        public void SetValue(IEntryBinding entry, object value, bool isValid = true, float delay = 0.0f)
        {
            if (entry == null)
                return;

            entry.EditBuffer.SetValue(value, isValid);

            if (delay <= 0.0f)
                Commit(entry);
            else
                _pendingEntries[entry] = delay;
        }

        public void SetValue(IEntryBinding entry, string key, object value, bool isValid = true, float delay = 0.0f)
        {
            if (entry == null)
                return;

            entry.EditBuffer.SetValue(key, value, isValid);

            if (delay <= 0.0f)
                Commit(entry);
            else
                _pendingEntries[entry] = delay;
        }

        public void ResetValue(IEntryBinding entry)
        {
            if (entry == null)
                return;

            _pendingEntries.Remove(entry);
            entry.EditBuffer.Clear();

            entry.ResetValue();
            GuiPipe.InvokeOnEntryValueReset(entry);
        }

        public void FlushValue(float deltaTime)
        {
            var entries = new List<IEntryBinding>(_pendingEntries.Keys);
            foreach (var entry in entries)
            {
                var remainingDelay = _pendingEntries[entry] - deltaTime;
                if (remainingDelay <= 0.0f)
                {
                    _pendingEntries.Remove(entry);
                    Commit(entry);
                }
                else
                    _pendingEntries[entry] = remainingDelay;
            }
        }

        private static void Commit(IEntryBinding entry)
        {
            var valueChanged = entry.EditBuffer.Commit(entry);

            if (valueChanged)
                GuiPipe.InvokeOnEntryValueChanged(entry);

            GuiPipe.InvokeOnEntryEditFinished(entry);
        }
    }
}
