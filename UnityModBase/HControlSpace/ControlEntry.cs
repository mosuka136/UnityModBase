using System;
using UnityModBase.BSpace;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 保存实时控制项的 getter、界面缓存和变化事件，不进行文件读写。
    /// 缓存值只经两条路径变化：按更新策略推进的 <c>Update</c> 调用 getter 刷新（不触发事件），
    /// 或界面经 <c>SetBoxedValueFromGui</c> 提交新值（触发事件）。
    /// 值类型必须是非多元素的受支持条目值类型，构造时校验。
    /// 条目不提供并发保护，应由 GUI 宿主在主线程统一驱动刷新与界面写入。
    /// </summary>
    /// <typeparam name="T">受实时控制编辑器支持的值类型。</typeparam>
    public sealed class ControlEntry<T> : IControlEntryInternal
    {
        // 按秒策略的非缩放时间累计（秒），满 1 秒刷新并清零；不可见期间持续清零，见 UpdateWhenVisibleEverySecond。
        private float _elapsedSeconds;
        private T _value;

        /// <inheritdoc/>
        public string TableKey { get; }

        /// <inheritdoc/>
        public string Key { get; }

        /// <inheritdoc/>
        public Translator Name { get; }

        /// <inheritdoc/>
        public Translator Description { get; }

        /// <inheritdoc/>
        public Type ValueType => typeof(T);

        /// <inheritdoc/>
        public IUiMetadata Metadata { get; }

        /// <inheritdoc/>
        public ControlUpdatePolicy UpdatePolicy { get; }

        /// <summary>获取用于读取监听对象当前值的委托。</summary>
        public Func<T> ValueGetter { get; }

        /// <summary>获取当前界面缓存值。</summary>
        public T Value => _value;

        /// <inheritdoc/>
        public object BoxedValue => _value;

        /// <summary>
        /// 用户通过界面提交不同的新值后同步触发。外部 getter 刷新不会触发本事件。
        /// </summary>
        public event EventHandler<T> OnValueChanged;

        /// <summary>
        /// 校验键名和值类型后创建条目，并立即调用一次 getter 取得初始缓存值。
        /// 校验或 getter 失败会在条目加入任何表之前抛出，由 <see cref="ControlService.Bind{T}"/> 统一保证失败时不改动模型。
        /// </summary>
        /// <exception cref="ArgumentException">表键或条目键不满足共享键名规则。</exception>
        /// <exception cref="InvalidOperationException">值类型不受支持或为多元素类型。</exception>
        internal ControlEntry(
            string tableKey,
            string key,
            Func<T> valueGetter,
            ControlUpdatePolicy updatePolicy,
            Translator name,
            Translator description = null,
            IUiMetadata metadata = null)
        {
            if (!EntryModel.IsValidTableKey(tableKey))
                throw new ArgumentException($"Invalid control table key: {tableKey}.", nameof(tableKey));
            if (!EntryModel.IsValidEntryKey(key))
                throw new ArgumentException($"Invalid control entry key: {key}.", nameof(key));
            var valueType = typeof(T);
            if (EntryModel.IsEntryMultipleValueType(valueType))
                throw new InvalidOperationException($"Type {valueType.FullName} is not a supported single value type.");
            if (!EntryModel.IsEntryValueType(valueType))
                throw new InvalidOperationException($"Unsupported control entry type: {valueType.FullName}.");

            TableKey = tableKey;
            Key = key;
            ValueGetter = valueGetter ?? throw new ArgumentNullException(nameof(valueGetter));
            UpdatePolicy = updatePolicy ?? throw new ArgumentNullException(nameof(updatePolicy));
            Name = name ?? new Translator(key, key);
            Description = description ?? new Translator();
            Metadata = metadata;

            var initialValue = ValueGetter();
            _value = initialValue;
        }

        void IControlEntryInternal.SetBoxedValueFromGui(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var valueType = value.GetType();
            if (EntryModel.IsEntryMultipleValueType(valueType))
                throw new InvalidOperationException($"Control entry type {valueType.FullName} is not a supported single value type.");
            if (!EntryModel.IsEntryValueType(valueType))
                throw new InvalidOperationException($"Control entry type {valueType.FullName} is not a supported value type.");
            if (!typeof(T).IsAssignableFrom(valueType))
                throw new InvalidOperationException($"Control entry type {valueType.FullName} is not assignable to expected type {typeof(T).FullName}.");

            var typedValue = (T)value;
            if (EntryModel.ValueEqual(_value, typedValue))
                return;

            _value = typedValue;
            foreach (var handler in OnValueChanged.GetInvocationListOrEmpty())
            {
                try
                {
                    handler.Invoke(this, typedValue);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Control value handler '{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}' failed. Entry='{TableKey}.{Key}', Value='{typedValue}'.", ex);
                }
            }
        }

        void IControlEntryInternal.Update(float unscaledDeltaTime, bool isVisible, bool becameVisible)
        {
            switch (UpdatePolicy.Kind)
            {
                case ControlUpdateKind.EveryFrame:
                    Refresh();
                    break;
                case ControlUpdateKind.EverySecond:
                    UpdateEverySecond(unscaledDeltaTime);
                    break;
                case ControlUpdateKind.WhenVisibleEveryFrame:
                    if (isVisible)
                        Refresh();
                    break;
                case ControlUpdateKind.WhenVisibleEverySecond:
                    UpdateWhenVisibleEverySecond(unscaledDeltaTime, isVisible, becameVisible);
                    break;
                case ControlUpdateKind.When:
                    if (UpdatePolicy.Condition())
                        Refresh();
                    break;
                case ControlUpdateKind.WhenVisible:
                    if (isVisible && UpdatePolicy.Condition())
                        Refresh();
                    break;
                case ControlUpdateKind.Never:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 按秒策略使用非缩放时间累计，不受游戏暂停和时间缩放影响。
        private void UpdateEverySecond(float unscaledDeltaTime)
        {
            _elapsedSeconds += unscaledDeltaTime;
            if (_elapsedSeconds < 1f)
                return;

            _elapsedSeconds = 0f;
            Refresh();
        }

        // 不可见期间持续清零计时器：隐藏时残余的不足 1 秒不应计入重新显示后的等待；
        // 重新可见的第一帧立即刷新一次，保证界面恢复显示时不展示过期缓存值。
        private void UpdateWhenVisibleEverySecond(float unscaledDeltaTime, bool isVisible, bool becameVisible)
        {
            if (!isVisible)
            {
                _elapsedSeconds = 0f;
                return;
            }

            if (becameVisible)
            {
                _elapsedSeconds = 0f;
                Refresh();
                return;
            }

            UpdateEverySecond(unscaledDeltaTime);
        }

        private void Refresh()
        {
            var value = ValueGetter();
            if (!EntryModel.ValueEqual(_value, value))
                _value = value;
        }

        /// <summary>
        /// 清除全部界面变化订阅；不会释放 getter 或 getter 所引用的业务对象。
        /// </summary>
        void IDisposable.Dispose()
        {
            OnValueChanged = null;
        }
    }
}
