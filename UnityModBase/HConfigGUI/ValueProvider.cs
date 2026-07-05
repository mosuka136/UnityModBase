using System;
using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.HConfigGUI
{
    public static class ValueProvider
    {
        public static object GetValue(IEntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.EditBuffer.IsUsing)
            {
                var latestEntry = entry.EditBuffer.GetLatestValue();
                if (!latestEntry.IsEmpty)
                    return latestEntry.Value;
            }

            return entry.Value;
        }

        public static T GetValue<T>(IEntryBinding entry)
        {
            return TypeConvert.To<T>(GetValue(entry));
        }

        public static object GetValidValue(IEntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.EditBuffer.IsUsing)
            {
                var latestEntry = entry.EditBuffer.GetLatestValue();
                if (!latestEntry.IsEmpty && latestEntry.IsValid)
                    return latestEntry.Value;
            }

            return entry.Value;
        }

        public static T GetValidValue<T>(IEntryBinding entry)
        {
            return TypeConvert.To<T>(GetValidValue(entry));
        }
    }
}
