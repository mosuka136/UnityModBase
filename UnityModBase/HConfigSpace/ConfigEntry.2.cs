using System;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 双元素配置项的门面类型，内部持有值类型为 <see cref="EntryValue{T1, T2}"/> 的 <see cref="ConfigEntry{T}"/>，
    /// 对调用方暴露按元素读写（<see cref="Value1"/>、<see cref="Value2"/>）的便利视图。
    /// 全部接口成员转发给内部配置项，自身不直接处理编码、事件或文件同步。
    /// </summary>
    /// <typeparam name="T1">第一个元素的值类型。</typeparam>
    /// <typeparam name="T2">第二个元素的值类型。</typeparam>
    /// <remarks>
    /// <see cref="ConfigService"/> 的双元素 <c>Bind&lt;T1, T2&gt;</c> 重载会创建本类型并将门面登记到运行时配置表；
    /// 自动保存事件和事务重载操作经由接口转发给内部配置项，因此与单值配置项遵循相同的生命周期。
    /// 平铺编码无法保留嵌套 <see cref="EntryValue{T1, T2}"/> 的元素边界；配置服务会拒绝这种元素类型，
    /// 直接调用构造函数时也应遵守相同约束。
    /// 本类型及其内部配置项均不提供并发保护，整体值和分元素值的读写必须由调用方串行化。
    /// </remarks>
    public sealed class ConfigEntry<T1, T2> : IConfigEntry, IEntryMultiple
    {
        private readonly ConfigEntry<EntryValue<T1, T2>> _configEntry;

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
        public EntryValue<T1, T2> Value
        {
            get => _configEntry.Value;
            set => _configEntry.Value = value;
        }

        /// <summary>
        /// 第一个元素的便捷访问。与现值等价的赋值会被忽略；只有变化时才构造新的 <see cref="EntryValue{T1, T2}"/> 整体替换 <see cref="Value"/>，
        /// 而非原地修改，以保证编码与变化事件按整体值触发。
        /// </summary>
        /// <exception cref="InvalidOperationException">替换后的双元素值无法按配置格式编码。</exception>
        public T1 Value1
        {
            get => Value.Value1;
            set
            {
                if (!EntryModel.ValueEqual(Value.Value1, value))
                    Value = new EntryValue<T1, T2>(value, Value.Value2);
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
                if (!EntryModel.ValueEqual(Value.Value2, value))
                    Value = new EntryValue<T1, T2>(Value.Value1, value);
            }
        }

        /// <inheritdoc/>
        public Type ValueType => typeof(EntryValue<T1, T2>);

        /// <inheritdoc/>
        public object BoxedValue
        {
            get => _configEntry.BoxedValue;
            set => _configEntry.BoxedValue = value;
        }

        /// <inheritdoc/>
        public object BoxedDefaultValue => _configEntry.BoxedDefaultValue;

        /// <inheritdoc/>
        public int Count => 2;

        /// <inheritdoc/>
        public Translator BaseDescription { get; }

        /// <inheritdoc/>
        public Translator[] ValueDescription { get; }

        /// <inheritdoc/>
        public event EventHandler OnValueChangedBase
        {
            add => _configEntry.OnValueChangedBase += value;
            remove => _configEntry.OnValueChangedBase -= value;
        }

        /// <summary>
        /// 构造双元素门面，内部委托给值类型为 <see cref="EntryValue{T1, T2}"/> 的 <see cref="ConfigEntry{T}"/>。
        /// <paramref name="description"/> 与两个分元素说明按语言各自用换行拼接，分元素行带「值1：/值2：」
        /// （英文 <c>Value1: /Value2: </c>，半角冒号后带空格）前缀，作为整体说明写入内部配置项，供文件注释呈现总说明与各元素含义；
        /// 某语言的分元素说明为空时省略该语言的前缀行（仅保留空行），避免文件注释出现无文本的结构标签；
        /// 未拼接的原始文本则分别存入 <see cref="BaseDescription"/> 与 <see cref="ValueDescription"/>，供 GUI 分级提示使用。
        /// </summary>
        /// <param name="entry">包含当前编码值且已设置所属表键名的文件项。</param>
        /// <param name="defaultValue1">第一个元素的声明默认值。</param>
        /// <param name="defaultValue2">第二个元素的声明默认值。</param>
        /// <param name="name">写入文件注释并供 UI 使用的名称；为 <c>null</c> 时以条目键作为中英文默认名称。</param>
        /// <param name="description">整体说明，与分元素说明拼接后写入内部配置项；为 <c>null</c> 时视为空说明。</param>
        /// <param name="valueDescription1">第一个元素的说明；为 <c>null</c> 时视为空说明。</param>
        /// <param name="valueDescription2">第二个元素的说明；为 <c>null</c> 时视为空说明。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">值类型提示、默认值无法编码，或文件项当前值无法解码。</exception>
        public ConfigEntry(
            ConfigFileEntry entry,
            T1 defaultValue1,
            T2 defaultValue2,
            Translator name,
            Translator description = null,
            Translator valueDescription1 = null,
            Translator valueDescription2 = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (description == null)
                description = new Translator();
            if (valueDescription1 == null)
                valueDescription1 = new Translator();
            if (valueDescription2 == null)
                valueDescription2 = new Translator();

            // 为空的分元素说明跳过前缀行（仅保留空行），避免拼接出只有「值1：」前缀而没有内容的行。
            var missingChineseDescription1 = string.IsNullOrWhiteSpace(valueDescription1.Chinese);
            var missingEnglishDescription1 = string.IsNullOrWhiteSpace(valueDescription1.English);
            var missingChineseDescription2 = string.IsNullOrWhiteSpace(valueDescription2.Chinese);
            var missingEnglishDescription2 = string.IsNullOrWhiteSpace(valueDescription2.English);
            _configEntry = new ConfigEntry<EntryValue<T1, T2>>(
                entry,
                new EntryValue<T1, T2>(defaultValue1, defaultValue2),
                name ?? new Translator(entry.Key, entry.Key),
                new Translator(
                    $"{description.Chinese}{Environment.NewLine}" +
                    $"{(missingChineseDescription1 ? "" : $"值1：{valueDescription1.Chinese}")}{Environment.NewLine}" +
                    $"{(missingChineseDescription2 ? "" : $"值2：{valueDescription2.Chinese}")}",
                    $"{description.English}{Environment.NewLine}" +
                    $"{(missingEnglishDescription1 ? "" : $"Value1: {valueDescription1.English}")}{Environment.NewLine}" +
                    $"{(missingEnglishDescription2 ? "" : $"Value2: {valueDescription2.English}")}")
                );

            BaseDescription = description;
            ValueDescription = new Translator[] { valueDescription1, valueDescription2 };
        }

        /// <inheritdoc/>
        public void ApplyBind(EntryChangePlan plan)
        {
            _configEntry.ApplyBind(plan ?? throw new ArgumentNullException(nameof(plan)));
        }

        /// <inheritdoc/>
        public bool PrepareBind(ConfigFileEntry candidate, out EntryChangePlan plan, out string errorMessage)
        {
            return _configEntry.PrepareBind(candidate, out plan, out errorMessage);
        }

        /// <inheritdoc/>
        public void PublishBind(EntryChangePlan plan)
        {
            _configEntry.PublishBind(plan ?? throw new ArgumentNullException(nameof(plan)));
        }

        /// <inheritdoc/>
        public void RollbackBind(EntryChangePlan plan)
        {
            _configEntry.RollbackBind(plan ?? throw new ArgumentNullException(nameof(plan)));
        }
    }
}
