using System;
using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.HConfigGUI
{
    public class EntryEditBuffer
    {
        private readonly Dictionary<string, OrderedEntry> _buffer = new Dictionary<string, OrderedEntry>();

        public OrderedEntry Value { get; private set; }
        public int Seq { get; private set; }
        public bool IsUsing { get; private set; }

        public EntryEditBuffer()
        {
            Value = OrderedEntry.Empty;
            Seq = 0;
            IsUsing = false;
        }

        public void SetValue(object value, bool isValid)
        {
            Value = new OrderedEntry(value, Seq++, isValid);
            IsUsing = true;
        }

        public void SetValue(string key, object value, bool isValid)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            _buffer[key] = new OrderedEntry(value, Seq++, isValid);
            IsUsing = true;
        }

        public void SetValue(string key, OrderedEntry entry)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            _buffer[key] = entry ?? throw new ArgumentNullException(nameof(entry));
            IsUsing = true;
        }

        public OrderedEntry GetValue(string key)
        {
            if (_buffer.TryGetValue(key, out var entry))
                return entry;
            return OrderedEntry.Empty;
        }

        public OrderedEntry GetLatestValue()
        {
            OrderedEntry latestEntry = Value;
            foreach (var entry in _buffer.Values)
            {
                if (entry.Order > latestEntry.Order)
                    latestEntry = entry;
            }
            return latestEntry;
        }

        public bool Commit(IEntryBinding entry, Func<object, object> transform = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var result = false;
            var latestEntry = GetLatestValue();
            if (!latestEntry.IsEmpty && latestEntry.IsValid)
            {
                var value = transform != null ? transform(latestEntry.Value) : latestEntry.Value;
                if (!Equals(entry.Value, value))
                {
                    entry.Value = value;
                    result = true;
                }
            }

            Clear();
            return result;
        }

        public bool Commit(string key, IEntryBinding entry, Func<object, object> transform = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var result = false;
            var latestEntry = GetValue(key);
            if (!latestEntry.IsEmpty && latestEntry.IsValid)
            {
                var value = transform != null ? transform(latestEntry.Value) : latestEntry.Value;
                if (!Equals(entry.Value, value))
                {
                    entry.Value = value;
                    result = true;
                }
            }

            Clear();
            return result;
        }

        public void Clear()
        {
            Value = OrderedEntry.Empty;
            _buffer.Clear();
            Seq = 0;
            IsUsing = false;
        }

        public class OrderedEntry
        {
            public int Order { get; set; }
            public object Value { get; set; }
            public bool IsValid { get; set; }

            public static readonly OrderedEntry Empty = new OrderedEntry(int.MinValue, int.MinValue, false);

            public bool IsEmpty => ReferenceEquals(this, Empty);

            public OrderedEntry(object value, int order, bool isValid)
            {
                Value = value;
                Order = order;
                IsValid = isValid;
            }
        }
    }
}
