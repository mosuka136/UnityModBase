using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 管理单个用户的纯内存实时控制表、条目和自动刷新调度，不读取或写入配置文件。
    /// </summary>
    public sealed class ControlService : IDisposable
    {
        private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double)
        };

        private bool _disposed;

        /// <summary>获取当前运行时控制表模型。</summary>
        public ControlSheet Sheet { get; private set; } = new ControlSheet();

        /// <summary>控制表或条目成功增加后同步触发。</summary>
        public event Action OnStructureChanged;

        /// <summary>
        /// 创建新的实时控制表。表键只允许 Unicode 字母、数字和下划线。
        /// </summary>
        public void CreateTable(string key, Translator name, Translator description = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ControlService));
            if (!ControlKeyValidator.IsValidTableKey(key))
                throw new ArgumentException($"Invalid control table key: {key}.", nameof(key));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (Sheet.Contains(key))
                throw new InvalidOperationException($"Control table already exists: {key}.");

            Sheet.Add(key, new ControlTable(key, name, description));
            InvokeStructureChanged();
        }

        /// <summary>
        /// 创建实时控制项并立即通过 getter 取得初始值。条目只进入内存模型，不建立配置文件绑定。
        /// </summary>
        public ControlEntry<T> Bind<T>(
            string tableKey,
            string key,
            Func<T> valueGetter,
            ControlUpdatePolicy updatePolicy,
            Translator name,
            Translator description,
            IUiMetadata metadata = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ControlService));
            if (string.IsNullOrWhiteSpace(tableKey))
                throw new ArgumentNullException(nameof(tableKey));
            if (!ControlKeyValidator.IsValidEntryKey(key))
                throw new ArgumentException($"Invalid control entry key: {key}.", nameof(key));
            if (valueGetter == null)
                throw new ArgumentNullException(nameof(valueGetter));
            if (updatePolicy == null)
                throw new ArgumentNullException(nameof(updatePolicy));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));

            var table = Sheet[tableKey] ?? throw new ArgumentException($"Control table does not exist: {tableKey}.", nameof(tableKey));
            if (table.Contains(key))
                throw new InvalidOperationException($"Control entry already exists: {tableKey}.{key}.");

            ValidateSupportedType(typeof(T), metadata);
            var entry = new ControlEntry<T>(tableKey, key, valueGetter, updatePolicy, name, description, metadata);
            table.Add(entry);
            InvokeStructureChanged();
            return entry;
        }

        /// <summary>
        /// 推进全部条目的更新策略。单个条件或 getter 失败不会阻止其余条目。
        /// </summary>
        public void Update(float unscaledDeltaTime, bool isVisible, bool becameVisible = false)
        {
            if (_disposed)
                return;

            var entries = GetEntrySnapshot();
            foreach (var entry in entries)
            {
                try
                {
                    entry.Update(unscaledDeltaTime, isVisible, becameVisible);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to refresh control entry. Entry='{entry.TableKey}.{entry.Key}', Policy='{entry.UpdatePolicy.Kind}'.", ex);
                }
            }
        }

        internal static void ValidateValue(Type valueType, object value, string parameterName)
        {
            if (value == null || !valueType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Invalid control value. Expected {valueType.FullName}, got {value?.GetType().FullName ?? "<null>"}.", parameterName);

            if (IsDualValueType(valueType))
            {
                var value1 = valueType.GetProperty(nameof(ControlEntryValue<int, int>.Value1)).GetValue(value, null);
                var value2 = valueType.GetProperty(nameof(ControlEntryValue<int, int>.Value2)).GetValue(value, null);
                var types = valueType.GetGenericArguments();
                ValidateValue(types[0], value1, parameterName);
                ValidateValue(types[1], value2, parameterName);
            }
        }

        private static void ValidateSupportedType(Type valueType, IUiMetadata metadata)
        {
            if (IsDualValueType(valueType))
            {
                var elementTypes = valueType.GetGenericArguments();
                foreach (var elementType in elementTypes)
                {
                    if (IsDualValueType(elementType) || !IsSimpleSupportedType(elementType))
                        throw new ArgumentException($"Unsupported control entry element type: {elementType.FullName}.", nameof(valueType));
                }

                if (metadata != null && !(metadata is UiCompositeMetadata))
                    throw new ArgumentException("Dual-value control entries require UiCompositeMetadata or null metadata.", nameof(metadata));

                var slotMetadatas = (metadata as UiCompositeMetadata)?.Metadatas;
                if (slotMetadatas == null)
                    return;
                if (slotMetadatas.Length < 2)
                    throw new ArgumentException("Dual-value control metadata must contain two slots.", nameof(metadata));
                ValidateSimpleMetadata(elementTypes[0], slotMetadatas[0]);
                ValidateSimpleMetadata(elementTypes[1], slotMetadatas[1]);
                return;
            }

            if (!IsSimpleSupportedType(valueType))
                throw new ArgumentException($"Unsupported control entry type: {valueType.FullName}.", nameof(valueType));
            ValidateSimpleMetadata(valueType, metadata);
        }

        private static bool IsSimpleSupportedType(Type type)
        {
            return type == typeof(bool) || type == typeof(string) || type.IsEnum || NumericTypes.Contains(type);
        }

        private static bool IsDualValueType(Type type)
        {
            return type != null && type.IsGenericType && !type.ContainsGenericParameters && type.GetGenericTypeDefinition() == typeof(ControlEntryValue<,>);
        }

        private static void ValidateSimpleMetadata(Type valueType, IUiMetadata metadata)
        {
            if (metadata == null)
                return;
            if (!(metadata is UiSliderMetadata) || !NumericTypes.Contains(valueType))
                throw new ArgumentException($"Unsupported GUI metadata '{metadata.GetType().FullName}' for control value type '{valueType.FullName}'.", nameof(metadata));
        }

        private List<IControlEntryInternal> GetEntrySnapshot()
        {
            var entries = new List<IControlEntryInternal>();
            foreach (var table in Sheet)
            {
                foreach (var entry in table.Value)
                    entries.Add((IControlEntryInternal)entry);
            }
            return entries;
        }

        private void InvokeStructureChanged()
        {
            foreach (var handler in OnStructureChanged.GetInvocationListOrEmpty())
            {
                try
                {
                    handler.Invoke();
                }
                catch (Exception ex)
                {
                    BLog.Error($"Control structure handler '{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}' failed; remaining handlers will continue.", ex);
                }
            }
        }

        /// <summary>
        /// 清除结构订阅和条目事件，并释放内存模型；可重复调用。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            OnStructureChanged = null;
            foreach (var entry in GetEntrySnapshot())
                entry.Dispose();
            Sheet.Clear();
        }
    }
}
