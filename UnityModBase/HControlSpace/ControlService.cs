using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 管理单个用户的纯内存实时控制表、条目和自动刷新调度，不读取或写入配置文件。
    /// 键名与值类型按共享的 <see cref="EntryModel"/> 规则校验，与配置空间保持一致判定；
    /// 表和条目只经 <see cref="CreateTable"/> 与 <see cref="Bind{T}"/> 登记，结构变化经 <see cref="OnStructureChanged"/> 通知控制 GUI 重建绑定树。
    /// <see cref="Update"/> 应由 GUI 宿主每帧调用一次并传入界面可见性；本服务及全部模型不提供并发保护，须由调用方串行化。
    /// </summary>
    public sealed class ControlService : IDisposable
    {
        private bool _disposed;

        /// <summary>获取当前运行时控制表模型。</summary>
        public ControlSheet Sheet { get; private set; } = new ControlSheet();

        /// <summary>控制表或条目成功增加后同步触发。</summary>
        public event Action OnStructureChanged;

        /// <summary>
        /// 创建新的实时控制表并同步触发 <see cref="OnStructureChanged"/>。
        /// </summary>
        /// <param name="key">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="name">运行时展示名称；为 <c>null</c> 时以表键作为中英文默认名称。</param>
        /// <param name="description">展示说明；为 <c>null</c> 时使用空说明。</param>
        /// <exception cref="ObjectDisposedException">服务已释放。</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> 不符合表键名语法（含 <c>null</c> 或空白）。</exception>
        /// <exception cref="InvalidOperationException">运行时控制表中已存在同键表。</exception>
        public void CreateTable(string key, Translator name, Translator description = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ControlService));
            if (!EntryModel.IsValidTableKey(key))
                throw new ArgumentException($"Invalid control table key: {key}.", nameof(key));
            if (Sheet.Contains(key))
                throw new InvalidOperationException($"Control table already exists: {key}.");
            if (name == null)
                name = new Translator(key, key);

            Sheet.Add(key, new ControlTable(key, name, description));
            InvokeStructureChanged();
        }

        /// <summary>
        /// 创建实时控制项并加入指定表。条目只进入内存模型，不建立配置文件绑定；
        /// 构造时立即调用一次 <paramref name="valueGetter"/> 取得初始缓存值，之后的读取由更新策略调度。
        /// </summary>
        /// <typeparam name="T">值类型，必须是受共享条目模型支持的非多元素条目值类型（多元素类型如 <see cref="EntryValue{T1, T2}"/> 会被拒绝）。</typeparam>
        /// <param name="tableKey">已有控制表键名，须先经 <see cref="CreateTable"/> 建立。</param>
        /// <param name="key">条目键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="valueGetter">读取监听对象当前值的委托，调用时机由 <paramref name="updatePolicy"/> 决定。</param>
        /// <param name="updatePolicy">自动刷新策略。</param>
        /// <param name="name">展示名称；为 <c>null</c> 时以条目键作为中英文默认名称。</param>
        /// <param name="description">展示说明；为 <c>null</c> 时使用空说明。</param>
        /// <param name="metadata">可选 GUI 元数据，影响值编辑器的控件选择和展示。</param>
        /// <returns>新创建并已登记到目标表的控制条目。</returns>
        /// <exception cref="ObjectDisposedException">服务已释放。</exception>
        /// <exception cref="ArgumentException"><paramref name="tableKey"/> 或 <paramref name="key"/> 不符合键名语法，值类型不受共享条目模型支持，或目标表不存在。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="valueGetter"/> 或 <paramref name="updatePolicy"/> 为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">值类型为多元素条目值类型，或同一表键和条目键已存在绑定。</exception>
        /// <remarks>
        /// getter 的返回值不做逐次类型校验，刷新时只按共享等值规则跳过未变化的值；
        /// 返回 <c>null</c> 或与声明类型不符的对象会原样进入界面缓存，由值编辑器自行处理。
        /// </remarks>
        public ControlEntry<T> Bind<T>(
            string tableKey,
            string key,
            Func<T> valueGetter,
            ControlUpdatePolicy updatePolicy,
            Translator name,
            Translator description = null,
            IUiMetadata metadata = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ControlService));
            if (!EntryModel.IsValidTableKey(tableKey))
                throw new ArgumentException($"Invalid control table key: {tableKey}.", nameof(tableKey));
            if (!EntryModel.IsValidEntryKey(key))
                throw new ArgumentException($"Invalid control entry key: {key}.", nameof(key));
            var valueType = typeof(T);
            if (!EntryModel.IsEntryValueType(valueType))
                throw new ArgumentException($"Unsupported control entry type: {valueType.FullName}.", nameof(valueType));
            if (valueGetter == null)
                throw new ArgumentNullException(nameof(valueGetter));
            if (updatePolicy == null)
                throw new ArgumentNullException(nameof(updatePolicy));
            if (name == null)
                name = new Translator(key, key);
            if (description == null)
                description = new Translator();

            var table = Sheet[tableKey] ?? throw new ArgumentException($"Control table does not exist: {tableKey}.", nameof(tableKey));
            if (table.Contains(key))
                throw new InvalidOperationException($"Control entry already exists: {tableKey}.{key}.");

            var entry = new ControlEntry<T>(tableKey, key, valueGetter, updatePolicy, name, description, metadata);
            table.Add(entry);
            InvokeStructureChanged();
            return entry;
        }

        /// <summary>
        /// 推进全部条目的更新策略。单个条件或 getter 失败不会阻止其余条目。
        /// 调用方应每帧调用一次并传入当前界面可见性，否则按秒和可见性策略无法正确工作。
        /// </summary>
        /// <param name="unscaledDeltaTime">非缩放帧间隔（秒）。</param>
        /// <param name="isVisible">实时控制界面当前是否可见。</param>
        /// <param name="becameVisible">本帧是否刚由隐藏转为可见。</param>
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

        // 遍历前先复制条目快照：刷新过程中的 getter 或事件回调若再登记条目，直接遍历运行时集合会被修改异常中断。
        private List<IControlEntryInternal> GetEntrySnapshot()
        {
            var entries = new List<IControlEntryInternal>();
            foreach (var table in Sheet.Values)
            {
                foreach (var entry in table.Entries)
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
