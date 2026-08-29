using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityModBase.BSpace;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 一个强类型运行时配置项。
    /// 它把文件层面的 <see cref="ConfigFileEntry"/> 与业务代码使用的 <typeparamref name="T"/> 值绑定起来，并在值变化时同步文件项文本与触发事件。
    /// 该类不直接写文件；写回时机由 <see cref="ConfigService"/> 订阅变化事件后决定。
    /// 初次绑定由构造函数内联完成，批量重载则先生成不发布事件的可回滚计划再由配置服务统一提交；两条路径都只覆盖单个配置项本身，不提供跨配置项的原子性。
    /// 实例不提供并发保护；赋值、重绑定和事件订阅应由调用方串行化。
    /// </summary>
    /// <typeparam name="T">配置项值类型。</typeparam>
    public class ConfigEntry<T> : IConfigEntry
    {
        private T _value;

        // 同一配置项的每次有效值变化（包括重载提交）都会递增该版本。重载发布阶段用它识别已被事件处理器后续赋值取代的候选值；
        // 该字段不承担跨线程同步职责。
        private long _changeVersion;

        // 事件处理器允许同步修改本项。队列把这种重入赋值延后到当前一轮订阅者通知结束后，
        // 确保每轮处理器收到同一个值，并按赋值顺序发布后续变化。
        private readonly Queue<T> _pendingValueChanges = new Queue<T>();

        // 仅标识当前调用栈是否正在排空事件队列，不是线程安全锁。
        private bool _publishingValueChanged;

        /// <summary>
        /// 当前配置值。
        /// 新值与旧值不等价时，会先完成编码并同步运行时值与绑定文件项，再触发强类型和非泛型事件；
        /// 订阅者异常只记录日志，不回滚已写入的内存状态。
        /// </summary>
        /// <remarks>
        /// 事件处理器再次设置本项时，新变化会排到当前一轮通知之后发布，避免递归通知使后续订阅者观察到错乱的值快照。
        /// 队列仍在当前赋值线程上同步排空；处理器若持续产生不同值，会延长当前调用，且可能使通知无法结束。
        /// 未绑定文件项时，当前实现会先更新运行时值，再因写入 <see cref="Entry"/> 失败而抛出异常；该失败不会发布事件。
        /// </remarks>
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
                _changeVersion++;
                PublishValueChanged();
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
        public string TableKey => Entry.TableKey;

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
        /// <remarks>
        /// 批量重载先提交全部配置项再发布事件。若较早发布的处理器已经改写尚未发布的配置项，
        /// 后者不会再发布已被取代的重载候选值，而由普通赋值流程发布处理器写入的新值。
        /// </remarks>
        public event EventHandler<T> OnValueChanged;

        /// <summary>
        /// 非泛型值变化事件，参数为 <see cref="EntryValueChangedEventArgs{T}"/> 包装的强类型新值。
        /// 该事件在对应值的全部强类型订阅者之后同步调用；单个订阅者异常不会阻止其余订阅者。
        /// 批量重载中的过期候选值遵循 <see cref="OnValueChanged"/> 的抑制规则。
        /// </summary>
        public event EventHandler OnValueChangedBase;

        /// <summary>
        /// 创建运行时绑定，更新文件项的展示及类型元数据，并从文件项当前文本解码实际值。
        /// 绑定过程复用重载事务三阶段：先 <see cref="PrepareBind"/> 完成可能失败的解码与规范化编码，
        /// 成功后依次 <see cref="ApplyBind"/> 切换绑定、<see cref="PublishBind"/> 发布事件。
        /// 所属表键名不在本构造函数校验，由 <paramref name="entry"/> 的 <see cref="ConfigFileEntry.TableKey"/> 随绑定带入。
        /// </summary>
        /// <param name="entry">包含当前编码值且已设置所属表键名的文件项。</param>
        /// <param name="defaultValue">声明默认值；不会覆盖文件项当前值。</param>
        /// <param name="name">写入文件注释并供 UI 使用的名称；为 <c>null</c> 时以条目键作为中英文默认名称。</param>
        /// <param name="description">写入文件注释并供 UI 使用的说明；为 <c>null</c> 时使用空说明。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">值类型提示、默认值无法编码，或文件项当前值无法解码为 <typeparamref name="T"/>。</exception>
        public ConfigEntry(ConfigFileEntry entry, T defaultValue, Translator name, Translator description = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            entry.Name = name ?? new Translator(entry.Key, entry.Key);
            entry.Description = description ?? new Translator();

            var valueTypeResult = ConfigFileModel.EncodeValueType<T>();
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

            DefaultValue = defaultValue;

            if (!PrepareBind(entry, out var plan, out var errorMessage))
            {
                BLog.Error(errorMessage, null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException(errorMessage);
            }
            ApplyBind(plan);
            PublishBind(plan);
        }

        /// <summary>
        /// 汇总编解码器返回的全部结构化错误，供重载协调器一次记录完整诊断。
        /// </summary>
        /// <param name="candidate">产生错误的候选配置项。</param>
        /// <param name="operation">失败的编解码操作名称，用于诊断文本。</param>
        /// <param name="errors">编解码器返回的结构化错误集合。</param>
        /// <returns>包含配置键、原始值和全部底层错误的多行诊断。</returns>
        private static string CreatePreparationError(ConfigFileEntry candidate, string operation, IReadOnlyList<ConfigFileError> errors)
        {
            var sb = new StringBuilder();
            sb.Append($"Failed to {operation} value for key: {candidate.Key}, value: {candidate.Value}.");

            foreach (var error in errors)
            {
                sb.AppendLine();
                sb.Append(error.GetFullMessage());
            }

            return sb.ToString();
        }

        /// <summary>
        /// 按强类型、非泛型的固定顺序同步通知订阅者，并隔离单个订阅者异常。
        /// 调用前绑定文件项和运行时值必须已经一致；通知失败不会回滚该状态。
        /// 重入赋值只追加到待发布队列，由最外层调用依次排空，避免递归改变当前轮次的事件参数。
        /// </summary>
        private void PublishValueChanged()
        {
            _pendingValueChanges.Enqueue(_value);
            if (_publishingValueChanged)
                return;

            _publishingValueChanged = true;
            try
            {
                while (_pendingValueChanges.Count > 0)
                {
                    var publishedValue = _pendingValueChanges.Dequeue();

                    foreach (var handler in OnValueChanged.GetInvocationListOrEmpty())
                    {
                        try
                        {
                            handler.Invoke(this, publishedValue);
                        }
                        catch (Exception ex)
                        {
                            BLog.Error($"Config value handler '{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}' failed. Entry='{TableKey}.{Key}', Value='{publishedValue}', Event='{nameof(OnValueChanged)}'.", ex);
                        }
                    }

                    foreach (var handler in OnValueChangedBase.GetInvocationListOrEmpty())
                    {
                        try
                        {
                            handler.Invoke(this, new EntryValueChangedEventArgs<T>(publishedValue));
                        }
                        catch (Exception ex)
                        {
                            BLog.Error($"Config value handler '{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}' failed. Entry='{TableKey}.{Key}', Value='{publishedValue}', Event='{nameof(OnValueChangedBase)}'.", ex);
                        }
                    }
                }
            }
            finally
            {
                _pendingValueChanges.Clear();
                _publishingValueChanged = false;
            }
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
        /// 当前支持基础类型、字符串、枚举、数组、<see cref="IEnumerable"/> 以及实现 <see cref="IConfigEntryValue"/> 的自定义值类型；未知复杂对象按不相等处理。
        /// </summary>
        /// <param name="a">第一个待比较对象。</param>
        /// <param name="b">第二个待比较对象。</param>
        /// <returns>类型相同且内容符合本配置模型等值规则时返回 <c>true</c>。</returns>
        /// <remarks>
        /// <see cref="IEnumerable"/> 会被完整、按顺序枚举并在可释放时释放枚举器，因此调用方应避免传入无限序列或带破坏性副作用的枚举源。
        /// </remarks>
        public static bool EqualBoxed(object a, object b)
        {
            return EntryModel.ValueEqual(a, b);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 配置服务批量重载时，候选项属于尚未生效的新文件模型，因此准备阶段可以向其复制当前运行时元数据；
        /// 构造函数也复用该预检建立首次绑定，并在成功后立即应用返回的计划。活动绑定、当前值和变化事件在本阶段保持不变。
        /// </remarks>
        public bool PrepareBind(ConfigFileEntry candidate, out EntryChangePlan plan, out string errorMessage)
        {
            plan = null;

            if (candidate == null)
            {
                errorMessage = "Candidate config entry cannot be null.";
                return false;
            }

            try
            {
                // 首次构造阶段 Entry 仍为 null，候选项即待绑定项，键必然一致；
                // 重新绑定阶段校验候选项键与当前绑定键一致，避免跨配置项错误复用候选项。
                if (Entry != null)
                {
                    if (candidate.TableKey != TableKey)
                    {
                        errorMessage = $"Candidate table key: {candidate.TableKey} does not match current table key: {TableKey}.";
                        return false;
                    }

                    if (candidate.Key != Entry.Key)
                    {
                        errorMessage = $"Candidate key: {candidate.Key} does not match current key: {Entry.Key}.";
                        return false;
                    }
                }

                var decodeResult = ConfigFileEntry.DecodeValue<T>(candidate.Value);
                if (!decodeResult.Success)
                {
                    errorMessage = CreatePreparationError(candidate, "decode", decodeResult.Errors);
                    return false;
                }

                var changed = !Equal(decodeResult.Value, _value);
                var encodedValue = candidate.Value;

                if (changed)
                {
                    // 在预检阶段完成规范化编码，确保 Apply 只包含可回滚的内存赋值。
                    // 等价值沿用用户原始文本，与 Value 的等值短路规则保持一致。
                    var encodeResult = ConfigFileEntry.EncodeValue(decodeResult.Value);
                    if (!encodeResult.Success)
                    {
                        errorMessage = CreatePreparationError(candidate, "encode", encodeResult.Errors);
                        return false;
                    }

                    encodedValue = encodeResult.Value;
                }

                // false 表示只把运行时声明的名称、说明和类型约束写入候选项，不覆盖用户刚读取的值。
                if (Entry != null && !Entry.CopyTo(candidate, false))
                {
                    errorMessage = $"Failed to copy metadata for config entry: {TableKey}.{candidate.Key}.";
                    return false;
                }

                plan = new EntryChangePlan(this, Entry, _value, candidate, decodeResult.Value, encodedValue, _changeVersion, changed);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected error while preparing config entry {TableKey}.{candidate.Key}: {ex}";
                return false;
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> 为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException"><see cref="EntryChangePlan.NewValue"/> 类型与 <see cref="ValueType"/> 不匹配。</exception>
        public void ApplyBind(EntryChangePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException();

            if (plan.NewValue.GetType() != ValueType)
                throw new ArgumentException($"Plan value type {plan.NewValue.GetType()} does not match entry value type {ValueType}.");

            plan.Applied = true;

            if (plan.Changed)
                plan.NewEntry.Value = plan.EncodedValue;

            Entry = plan.NewEntry;
            if (plan.Changed)
            {
                _value = (T)plan.NewValue;
                _changeVersion++;
                plan.AppliedChangeVersion = _changeVersion;
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> 为 <c>null</c>。</exception>
        public void PublishBind(EntryChangePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException();

            // 其他配置项的处理器可能已经再次修改本项；此时普通赋值流程已经发布了更新后的值，
            // 不再发布本计划捕获的旧变化，避免重复或失真的通知。
            if (plan.Applied && plan.Changed && _changeVersion == plan.AppliedChangeVersion)
                PublishValueChanged();
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> 为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException"><see cref="EntryChangePlan.OldValue"/> 类型与 <see cref="ValueType"/> 不匹配。</exception>
        public void RollbackBind(EntryChangePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException();

            if (plan.OldValue.GetType() != ValueType)
                throw new ArgumentException($"Plan value type {plan.OldValue.GetType()} does not match entry value type {ValueType}.");

            if (!plan.Applied)
                return;

            Entry = plan.OldEntry;
            _value = (T)plan.OldValue;
            _changeVersion = plan.OldChangeVersion;
            plan.NewEntry.Value = plan.OriginalCandidateValue;
            plan.Applied = false;
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
