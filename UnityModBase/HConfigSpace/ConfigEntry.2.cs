using System;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 双元素元组配置项的门面类型，内部持有值类型为 <see cref="ConfigEntryValue{T1, T2}"/> 的 <see cref="ConfigEntry{T}"/>，
    /// 对调用方暴露按元素读写（<see cref="Value1"/>、<see cref="Value2"/>）的便利视图。
    /// 全部接口成员转发给内部配置项，自身不直接处理编码、事件或文件同步。
    /// </summary>
    /// <typeparam name="T1">第一个元素的值类型。</typeparam>
    /// <typeparam name="T2">第二个元素的值类型。</typeparam>
    /// <remarks>
    /// <see cref="ConfigService"/> 的双元素 <c>Bind&lt;T1, T2&gt;</c> 重载会创建本类型并将门面登记到运行时配置表；
    /// 自动保存事件和事务重载操作经由接口转发给内部配置项，因此与单值配置项遵循相同的生命周期。
    /// 平铺编码无法保留嵌套 <see cref="ConfigEntryValue{T1, T2}"/> 的元素边界；配置服务会拒绝这种元素类型，
    /// 直接调用构造函数时也应遵守相同约束。
    /// 本类型及其内部配置项均不提供并发保护，整体值和分元素值的读写必须由调用方串行化。
    /// </remarks>
    public class ConfigEntry<T1, T2> : IConfigEntry
    {
        private readonly ConfigEntry<ConfigEntryValue<T1, T2>> _configEntry;

        /// <inheritdoc/>
        public string TableKey => _configEntry.TableKey;

        /// <inheritdoc/>
        public string Key => _configEntry.Key;

        /// <inheritdoc/>
        public Translator Name => _configEntry.Name;

        /// <inheritdoc/>
        public Translator Description => _configEntry.Description;

        /// <inheritdoc/>
        public ConfigFileEntry Entry => _configEntry.Entry;

        /// <summary>
        /// 当前双元素值。与旧值不等价时，赋值会复用底层 <see cref="ConfigEntry{T}"/> 的编码、文件模型同步和事件流程。
        /// 若本门面由 <see cref="ConfigService"/> 登记且启用自动保存，变化事件还会在当前线程同步写入整个配置文件。
        /// </summary>
        /// <exception cref="InvalidOperationException">新值或其中任一元素无法按配置格式编码。</exception>
        public ConfigEntryValue<T1, T2> Value
        {
            get => _configEntry.Value;
            set => _configEntry.Value = value;
        }

        /// <summary>
        /// 第一个元素的便捷访问。与现值等价的赋值会被忽略；只有变化时才构造新的 <see cref="ConfigEntryValue{T1, T2}"/> 整体替换 <see cref="Value"/>，
        /// 而非原地修改，以保证编码与变化事件按整体值触发。
        /// </summary>
        /// <exception cref="InvalidOperationException">替换后的双元素值无法按配置格式编码。</exception>
        public T1 Value1
        {
            get => Value.Value1;
            set
            {
                if (!ConfigFileModel.ValueEqual(Value.Value1, value))
                    Value = new ConfigEntryValue<T1, T2>(value, Value.Value2);
            }
        }

        /// <summary>
        /// 第二个元素的便捷访问。赋值语义同 <see cref="Value1"/>：忽略等价值，变化时整体替换 <see cref="Value"/>。
        /// </summary>
        /// <exception cref="InvalidOperationException">替换后的双元素值无法按配置格式编码。</exception>
        public T2 Value2
        {
            get => Value.Value2;
            set
            {
                if (!ConfigFileModel.ValueEqual(Value.Value2, value))
                    Value = new ConfigEntryValue<T1, T2>(Value.Value1, value);
            }
        }

        /// <inheritdoc/>
        public Type ValueType => typeof(ConfigEntryValue<T1, T2>);

        /// <inheritdoc/>
        public object BoxedValue
        {
            get => _configEntry.BoxedValue;
            set => _configEntry.BoxedValue = value;
        }

        /// <inheritdoc/>
        public object BoxedDefaultValue => _configEntry.BoxedDefaultValue;

        /// <inheritdoc/>
        public event EventHandler OnValueChangedBase
        {
            add => _configEntry.OnValueChangedBase += value;
            remove => _configEntry.OnValueChangedBase -= value;
        }

        /// <summary>
        /// 构造双元素元组门面，内部委托给值类型为 <see cref="ConfigEntryValue{T1, T2}"/> 的 <see cref="ConfigEntry{T}"/>。
        /// <paramref name="description"/> 与两个分元素说明会按语言各自用换行拼接，作为整体说明写入内部配置项，
        /// 使文件注释与 UI 提示同时呈现总说明与各元素含义。
        /// </summary>
        /// <param name="entry">包含当前编码值且已设置所属表键名的文件项。</param>
        /// <param name="defaultValue1">第一个元素的声明默认值。</param>
        /// <param name="defaultValue2">第二个元素的声明默认值。</param>
        /// <param name="name">写入文件注释并供 UI 使用的名称。</param>
        /// <param name="description">整体说明，与分元素说明拼接后写入内部配置项。</param>
        /// <param name="valueDescription1">第一个元素的说明。</param>
        /// <param name="valueDescription2">第二个元素的说明。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 或任一名称、说明参数为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">值类型提示、默认值无法编码，或文件项当前值无法解码。</exception>
        public ConfigEntry(
            ConfigFileEntry entry,
            T1 defaultValue1,
            T2 defaultValue2,
            Translator name,
            Translator description,
            Translator valueDescription1,
            Translator valueDescription2)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));
            if (valueDescription1 == null)
                throw new ArgumentNullException(nameof(valueDescription1));
            if (valueDescription2 == null)
                throw new ArgumentNullException(nameof(valueDescription2));

            _configEntry = new ConfigEntry<ConfigEntryValue<T1, T2>>(
                entry,
                new ConfigEntryValue<T1, T2>(defaultValue1, defaultValue2),
                name,
                new Translator(
                    $"{description.Chinese}{Environment.NewLine}{valueDescription1.Chinese}{Environment.NewLine}{valueDescription2.Chinese}",
                    $"{description.English}{Environment.NewLine}{valueDescription1.English}{Environment.NewLine}{valueDescription2.English}")
                );
        }

        /// <inheritdoc/>
        public void ApplyBind(EntryChangePlan plan)
        {
            _configEntry.ApplyBind(plan);
        }

        /// <inheritdoc/>
        public bool PrepareBind(ConfigFileEntry candidate, out EntryChangePlan plan, out string errorMessage)
        {
            return _configEntry.PrepareBind(candidate, out plan, out errorMessage);
        }

        /// <inheritdoc/>
        public void PublishBind(EntryChangePlan plan)
        {
            _configEntry.PublishBind(plan);
        }

        /// <inheritdoc/>
        public void RollbackBind(EntryChangePlan plan)
        {
            _configEntry.RollbackBind(plan);
        }
    }
}
