using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    /// <summary>
    /// 将 <see cref="IConfigEntry"/> 适配为配置 GUI 可编辑节点，并为其持有独立的暂存输入缓冲区。
    /// 本类不负责延迟提交或通知 UI；这些职责分别由 <see cref="EntryChangeSink"/> 和 GUI 上下文承担。
    /// </summary>
    public class EntryBinding : IResettableEntryBinding
    {
        /// <summary>
        /// 获取被适配的底层配置项。
        /// </summary>
        public IConfigEntry Entry { get; }

        /// <inheritdoc/>
        public string Key => Entry.Key;

        /// <inheritdoc/>
        public Translator Name => Entry.Name;

        /// <inheritdoc/>
        public Translator Description => Entry.Description;

        /// <inheritdoc/>
        public Type ValueType => Entry.ValueType;

        /// <inheritdoc/>
        public IUiMetadata Metadata { get; }

        /// <inheritdoc/>
        public EntryEditBuffer EditBuffer { get; }

        /// <summary>
        /// 获取或设置配置项的已提交值。
        /// 设置值必须为声明类型可赋值的非 null 实例；相同值不会重复写入底层配置项。
        /// </summary>
        /// <exception cref="ArgumentException">值为 null 或其运行时类型不能赋给 <see cref="ValueType"/>。</exception>
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

        /// <summary>
        /// 清除所有暂存输入，并将底层配置项恢复为声明的默认值。
        /// </summary>
        public void ResetValue()
        {
            EditBuffer.Clear();
            Entry.BoxedValue = Entry.BoxedDefaultValue;
        }

        /// <summary>
        /// 创建配置项绑定，并根据配置管理器类型和配置键解析可选 GUI 元数据。
        /// 元数据解析失败会回退为无元数据，不影响配置项继续显示。
        /// </summary>
        /// <param name="classType">声明配置字段及其 GUI 特性的配置管理器类型。</param>
        /// <param name="entry">要适配的底层配置项。</param>
        /// <exception cref="ArgumentNullException"><paramref name="classType"/> 或 <paramref name="entry"/> 为 null。</exception>
        public EntryBinding(Type classType, IConfigEntry entry)
        {
            if (classType == null)
                throw new ArgumentNullException(nameof(classType));

            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Metadata = UiMetadataHelper.GetMetadata(classType, entry);
            EditBuffer = new EntryEditBuffer();
        }
    }
}
