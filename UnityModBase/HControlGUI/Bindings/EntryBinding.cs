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

        /// <summary>
        /// 获取当前界面缓存值。写入会把新值提交给控制条目：等值写入被条目忽略，
        /// 实际变化会同步更新缓存并触发条目的 <c>OnValueChanged</c> 事件，因此绘制期间写入可能重入事件处理器。
        /// </summary>
        public object Value
        {
            get => Entry.BoxedValue;
            set => ((IControlEntryInternal)Entry).SetBoxedValueFromGui(value);
        }

        /// <summary>创建实时控制项的编辑绑定。</summary>
        /// <param name="entry">被适配的非 null 实时控制项。</param>
        internal EntryBinding(IControlEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}
