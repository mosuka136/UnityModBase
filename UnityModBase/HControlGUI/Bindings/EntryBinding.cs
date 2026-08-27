using System;
using UnityModBase.HControlSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlGUI.Bindings
{
    /// <summary>
    /// 将纯内存实时控制项适配为现有值编辑器可使用的绑定。
    /// </summary>
    internal sealed class EntryBinding : IEntryBinding
    {
        /// <summary>获取底层实时控制项。</summary>
        public IControlEntry Entry { get; }

        /// <inheritdoc/>
        public string Key => Entry.Key;

        /// <inheritdoc/>
        public Translator Name => Entry.Name;

        /// <inheritdoc/>
        public Translator Description => Entry.Description;

        /// <inheritdoc/>
        public Type ValueType => Entry.ValueType;

        /// <inheritdoc/>
        public IUiMetadata Metadata => Entry.Metadata;

        /// <inheritdoc/>
        public EntryEditBuffer EditBuffer { get; } = new EntryEditBuffer();

        /// <inheritdoc/>
        public object Value
        {
            get => Entry.BoxedValue;
            set => ((IControlEntryInternal)Entry).SetBoxedValueFromGui(value);
        }

        internal EntryBinding(IControlEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}
