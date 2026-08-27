using System;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 保存实时控制项的 getter、界面缓存和变化事件，不进行文件读写。
    /// </summary>
    /// <typeparam name="T">受实时控制编辑器支持的值类型。</typeparam>
    public sealed class ControlEntry<T> : IControlEntryInternal
    {
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

        internal ControlEntry(
            string tableKey,
            string key,
            Func<T> valueGetter,
            ControlUpdatePolicy updatePolicy,
            Translator name,
            Translator description,
            IUiMetadata metadata)
        {
            TableKey = tableKey;
            Key = key;
            ValueGetter = valueGetter ?? throw new ArgumentNullException(nameof(valueGetter));
            UpdatePolicy = updatePolicy ?? throw new ArgumentNullException(nameof(updatePolicy));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Metadata = metadata;

            var initialValue = ValueGetter();
            ControlService.ValidateValue(typeof(T), initialValue, nameof(valueGetter));
            _value = initialValue;
        }

        void IControlEntryInternal.SetBoxedValueFromGui(object value)
        {
            ControlService.ValidateValue(typeof(T), value, nameof(value));
            var typedValue = (T)value;
            if (ControlValueComparer.Equal(_value, typedValue))
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

        private void UpdateEverySecond(float unscaledDeltaTime)
        {
            _elapsedSeconds += unscaledDeltaTime;
            if (_elapsedSeconds < 1f)
                return;

            _elapsedSeconds = 0f;
            Refresh();
        }

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
            ControlService.ValidateValue(typeof(T), value, nameof(ValueGetter));
            if (!ControlValueComparer.Equal(_value, value))
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
