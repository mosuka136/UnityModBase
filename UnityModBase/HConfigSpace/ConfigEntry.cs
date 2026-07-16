using System;
using System.Collections;
using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 运行时配置项的非泛型视图。
    /// UI 层通过该接口读取元数据并写入装箱值，而不需要在绑定阶段知道具体泛型类型。
    /// </summary>
    public interface IConfigEntry
    {
        /// <summary>
        /// 当前绑定文件项提供的多语言展示名称。
        /// </summary>
        Translator Name { get; }

        /// <summary>
        /// 当前绑定文件项提供的多语言说明。
        /// </summary>
        Translator Description { get; }

        /// <summary>
        /// 运行时声明所属的表键名；重新绑定文件项不会改变该值。
        /// </summary>
        string TableName { get; }

        /// <summary>
        /// 当前绑定文件项的键名。
        /// </summary>
        string Key { get; }

        /// <summary>
        /// 当前绑定的文件层模型；重载成功后可能替换为新实例。
        /// </summary>
        ConfigFileEntry Entry { get; }

        /// <summary>
        /// 装箱值必须遵循的运行时声明类型。
        /// </summary>
        Type ValueType { get; }

        /// <summary>
        /// 装箱后的当前值。赋值必须能直接转换为声明类型，并会执行与强类型赋值相同的编码和事件流程。
        /// </summary>
        object BoxedValue { get; set; }

        /// <summary>
        /// 装箱后的声明默认值；该值用于元数据，不表示读取失败时会自动回退。
        /// </summary>
        object BoxedDefaultValue { get; }

        /// <summary>
        /// 面向非泛型调用方的同步值变化事件。
        /// </summary>
        event EventHandler OnValueChangedBase;

        /// <summary>
        /// 替换文件层绑定，并尝试从新文件项恢复当前强类型值。
        /// </summary>
        /// <param name="entry">包含待恢复值的新文件项。</param>
        void RebindEntry(ConfigFileEntry entry);
    }

    /// <summary>
    /// 一个强类型运行时配置项。
    /// 它把文件层面的 <see cref="ConfigFileEntry"/> 与业务代码使用的 <typeparamref name="T"/> 值绑定起来，并在值变化时同步文件项文本与触发事件。
    /// 该类不直接写文件；写回时机由 <see cref="ConfigService"/> 订阅变化事件后决定。
    /// 实例不提供并发保护；赋值、重绑定和事件订阅应由调用方串行化。
    /// </summary>
    /// <typeparam name="T">配置项值类型。</typeparam>
    public class ConfigEntry<T> : IConfigEntry
    {
        private T _value;

        /// <summary>
        /// 当前配置值。
        /// 新值与旧值不等价时，会先编码并更新绑定文件项，再同步触发强类型和非泛型事件；订阅者异常只记录日志，不回滚已写入的内存状态。
        /// </summary>
        /// <exception cref="InvalidOperationException">新值无法按配置格式编码。</exception>
        /// <exception cref="NullReferenceException">实例尚未绑定文件项，且新值与当前值不等价。</exception>
        public T Value
        {
            get => _value;
            set
            {
                if (Equal(value, _value))
                    return;

                var valueResult = ConfigFileEntry.EncodeValue(value);
                if (!valueResult.Success)
                {
                    foreach (var error in valueResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                    throw new InvalidOperationException($"Failed to encode value for key: {Key}, value: {value}. Errors: {string.Join(", ", valueResult.Errors)}");
                }

                _value = value;

                Entry.Value = valueResult.Value;

                foreach(var handler in OnValueChanged.GetInvocationListOrEmpty())
                {
                    try
                    {
                        handler.Invoke(this, _value);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Exception in value changed event for key: {Key}, value: {_value}.", ex);
                    }
                }

                foreach (var handler in OnValueChangedBase.GetInvocationListOrEmpty())
                {
                    try
                    {
                        handler.Invoke(this, new EntryValueChangedEventArgs<T>(_value));
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Exception in value changed event for key: {Key}, value: {_value}.", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 当前绑定文件项的多语言展示名称。
        /// </summary>
        /// <exception cref="NullReferenceException">实例尚未绑定文件项。</exception>
        public Translator Name => Entry.Name;

        /// <summary>
        /// 当前绑定文件项的多语言说明。
        /// </summary>
        /// <exception cref="NullReferenceException">实例尚未绑定文件项。</exception>
        public Translator Description => Entry.Description;

        /// <inheritdoc/>
        public string TableName { get; private set; }

        /// <summary>
        /// 当前绑定文件项的键名。
        /// </summary>
        /// <exception cref="NullReferenceException">实例尚未绑定文件项。</exception>
        public string Key => Entry.Key;

        /// <summary>
        /// 声明配置项时提供的默认值，仅用于生成默认值元数据，不会覆盖文件中已有的有效值。
        /// </summary>
        public T DefaultValue { get; private set; }

        /// <inheritdoc/>
        public ConfigFileEntry Entry { get; private set; }

        /// <inheritdoc/>
        public Type ValueType => typeof(T);

        /// <summary>
        /// 面向非泛型调用方的当前值。设置时要求对象可直接转换为 <typeparamref name="T"/>。
        /// </summary>
        /// <exception cref="InvalidCastException">传入值不能转换为 <typeparamref name="T"/>。</exception>
        /// <exception cref="NullReferenceException">为不可空值类型传入 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">转换后的值无法按配置格式编码。</exception>
        public object BoxedValue
        {
            get => Value;
            set => Value = (T)value;
        }

        /// <inheritdoc/>
        public object BoxedDefaultValue => DefaultValue;

        /// <summary>
        /// 强类型值变化事件。只有新值与旧值不等价时才触发，并且先于 <see cref="OnValueChangedBase"/> 同步调用。
        /// 单个订阅者抛出的异常会被记录，不会阻止其余订阅者。
        /// </summary>
        public event EventHandler<T> OnValueChanged;

        /// <summary>
        /// 非泛型值变化事件，参数为 <see cref="EntryValueChangedEventArgs{T}"/> 包装的强类型新值。
        /// 该事件在全部强类型订阅者之后同步调用；单个订阅者异常不会阻止其余订阅者。
        /// </summary>
        public event EventHandler OnValueChangedBase;

        /// <summary>
        /// 创建尚未绑定文件项的实例。
        /// <see cref="Entry"/>、<see cref="TableName"/> 和默认值元数据保持默认状态，绑定完成前不应作为正常配置项使用。
        /// </summary>
        public ConfigEntry()
        {

        }

        /// <summary>
        /// 使用文件项现有的名称和说明创建运行时绑定。
        /// 文件中的当前值优先于 <paramref name="defaultValue"/>；默认值只写入元数据。
        /// </summary>
        /// <param name="tableKey">所属表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="entry">包含当前编码值的文件项。</param>
        /// <param name="defaultValue">声明默认值。</param>
        /// <exception cref="NullReferenceException"><paramref name="entry"/> 为 <c>null</c>；该重载会在委托构造前读取其元数据。</exception>
        /// <exception cref="InvalidOperationException">键名、表名或值格式非法，或者类型/默认值无法编码。</exception>
        public ConfigEntry(string tableKey, ConfigFileEntry entry, T defaultValue) :
            this(tableKey, entry, defaultValue, entry.Name, entry.Description)
        {
        }

        /// <summary>
        /// 创建运行时绑定，更新文件项的展示及类型元数据，并从文件项当前文本解码实际值。
        /// </summary>
        /// <param name="tableKey">所属表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="entry">包含当前编码值的文件项。</param>
        /// <param name="defaultValue">声明默认值；不会覆盖文件项当前值。</param>
        /// <param name="name">写入文件注释并供 UI 使用的名称。</param>
        /// <param name="description">写入文件注释并供 UI 使用的说明。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">键名、表名或值格式非法，或者类型/默认值无法编码。</exception>
        public ConfigEntry(string tableKey, ConfigFileEntry entry, T defaultValue, Translator name, Translator description)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            entry.Name = name;
            entry.Description = description;

            var valueTypeResult = ConfigFileEntry.EncodeValueType<T>();
            if (!valueTypeResult.Success)
            {
                foreach (var error in valueTypeResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to encode value type for key: {entry.Key}, type: {typeof(T).FullName}. Errors: {string.Join(", ", valueTypeResult.Errors)}");
            }
            entry.ValueType = valueTypeResult.Value;

            var acceptableValuesResult = ConfigFileEntry.EncodeAcceptableValues<T>();
            if (acceptableValuesResult.Success)
                entry.AcceptableValues = acceptableValuesResult.Value;

            var defaultValueResult = ConfigFileEntry.EncodeValue(defaultValue);
            if (!defaultValueResult.Success)
            {
                foreach (var error in defaultValueResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to encode default value for key: {entry.Key}, value: {defaultValue}. Errors: {string.Join(", ", defaultValueResult.Errors)}");
            }
            entry.DefaultValue = defaultValueResult.Value;

            if (!ConfigFileEntry.IsValidKeyName(entry.Key))
                throw new InvalidOperationException($"Invalid key name: {entry.Key}");

            if (!ConfigFileTable.IsValidTableName(tableKey))
                throw new InvalidOperationException($"Invalid table name: {tableKey}");

            TableName = tableKey;
            DefaultValue = defaultValue;
            RebindEntry(entry);
        }

        /// <summary>
        /// 将配置项重新绑定到另一个文件项，并从文件项的文本值解码当前值。
        /// 常用于重新读取配置文件后保留已有 <see cref="ConfigEntry{T}"/> 引用。
        /// 解码在替换绑定之前完成，因此失败时原文件项和值保持不变；若解码值与当前值等价，只替换绑定而不触发事件。
        /// </summary>
        /// <param name="entry">新的文件项；为 <c>null</c> 时不做处理。</param>
        /// <exception cref="InvalidOperationException">文件项的值无法解码为 <typeparamref name="T"/>。</exception>
        public void RebindEntry(ConfigFileEntry entry)
        {
            if (entry == null)
                return;
            var decodeResult = ConfigFileEntry.DecodeValue<T>(entry.Value);
            if (!decodeResult.Success)
            {
                foreach (var error in decodeResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to decode value for key: {entry.Key}, value: {entry.Value}. Errors: {string.Join(", ", decodeResult.Errors)}");
            }
            Entry = entry;
            Value = decodeResult.Value;
        }

        /// <summary>
        /// 比较两个配置值是否等价。
        /// 集合类型会按元素顺序深度比较，以避免数组/List 在引用变化但内容未变时触发多余写入。
        /// </summary>
        /// <param name="a">第一个配置值。</param>
        /// <param name="b">第二个配置值。</param>
        /// <returns>两个受支持值是否等价。</returns>
        public static bool Equal(T a, T b)
        {
            return EqualBoxed(a, b);
        }

        /// <summary>
        /// 非泛型等值比较实现。
        /// 当前只支持基础类型、字符串、枚举、数组和 IEnumerable；未知复杂对象按不相等处理。
        /// IEnumerable 会被完整、按顺序枚举并在可释放时释放枚举器，因此调用方应避免传入无限序列或带破坏性副作用的枚举源。
        /// </summary>
        /// <param name="a">第一个待比较对象。</param>
        /// <param name="b">第二个待比较对象。</param>
        /// <returns>类型相同且内容符合本配置模型等值规则时返回 <c>true</c>。</returns>
        public static bool EqualBoxed(object a, object b)
        {
            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            var type = a.GetType();

            if (type != b.GetType())
                return false;

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return object.Equals(a, b);

            if (type.IsArray)
            {
                var arrayA = a as Array;
                var arrayB = b as Array;

                if (arrayA == null || arrayB == null)
                    return false;

                if (arrayA.Length != arrayB.Length)
                    return false;

                for (int i = 0; i < arrayA.Length; i++)
                {
                    if (!EqualBoxed(arrayA.GetValue(i), arrayB.GetValue(i)))
                        return false;
                }

                return true;
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var enumA = (a as IEnumerable)?.GetEnumerator();
                var enumB = (b as IEnumerable)?.GetEnumerator();

                if (enumA == null || enumB == null)
                    return false;

                try
                {
                    while (true)
                    {
                        var hasNextA = enumA.MoveNext();
                        var hasNextB = enumB.MoveNext();

                        if (hasNextA != hasNextB)
                            return false;

                        if (!hasNextA)
                            break;

                        if (!EqualBoxed(enumA.Current, enumB.Current))
                            return false;
                    }

                    return true;
                }
                finally
                {
                    (enumA as IDisposable)?.Dispose();
                    (enumB as IDisposable)?.Dispose();
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 通过非泛型事件暴露强类型配置值变化时使用的事件参数。
    /// </summary>
    /// <typeparam name="T">变化后的值类型。</typeparam>
    public class EntryValueChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// 事件触发后的配置值。
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 创建非泛型事件使用的值包装。
        /// </summary>
        /// <param name="value">变化后的配置值。</param>
        public EntryValueChangedEventArgs(T value)
        {
            Value = value;
        }

        /// <summary>
        /// 从事件参数中直接取得强类型值。
        /// </summary>
        /// <param name="args">包含配置值的事件参数。</param>
        /// <returns>事件触发后的配置值。</returns>
        /// <exception cref="NullReferenceException"><paramref name="args"/> 为 <c>null</c>。</exception>
        public static implicit operator T(EntryValueChangedEventArgs<T> args) => args.Value;
    }
}
