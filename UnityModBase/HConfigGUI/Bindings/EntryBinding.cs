using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class EntryBinding : IEntryBinding
    {
        public IConfigEntry Entry { get; }
        public string Key => Entry.Key;
        public Translator Name => Entry.Name;
        public Translator Description => Entry.Description;
        public Type ValueType => Entry.ValueType;
        public IUiMetadata Metadata { get; }
        public EntryEditBuffer EditBuffer { get; }

        public object Value
        {
            get => Entry.BoxedValue;
            set
            {
                if (Entry.ValueType.IsAssignableFrom(value?.GetType()))
                {
                    if (!value.Equals(Entry.BoxedValue))
                        Entry.BoxedValue = value;
                }
                else
                    throw new ArgumentException($"Invalid value type. Expected {Entry.ValueType}, got {value?.GetType()}.");
            }
        }

        public void ResetValue()
        {
            EditBuffer.Clear();
            Entry.BoxedValue = Entry.BoxedDefaultValue;
        }

        public EntryBinding(Type classType, IConfigEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Metadata = UiMetadataHelper.GetMetadata(classType, entry);
            EditBuffer = new EntryEditBuffer();
        }
    }
}
