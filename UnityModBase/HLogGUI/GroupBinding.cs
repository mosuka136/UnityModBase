using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 保存一个用户的日志绑定，并为每个可排序字段维护预索引的 <see cref="SortedSet{T}"/>。
    /// 写入操作会同步更新原始顺序和全部排序集合；公开集合及 <see cref="SortedGroup"/> 均为实时视图，不是快照，调用方不得直接修改，
    /// 也不得在日志事件线程写入期间并发枚举。
    /// </summary>
    public class GroupBinding
    {
        // 日志事件可能来自不同线程，此锁只串行化 Add/Remove 对多组集合的联合写入；读取路径没有取得读锁。
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// 获取当前集合中是否存在累计出现多次的日志。
        /// </summary>
        public bool HasRepeatedEntry { get; private set; } = false;
        /// <summary>
        /// 获取当前集合中是否存在带异常的日志。
        /// </summary>
        public bool HasExceptionEntry { get; private set; } = false;
        /// <summary>
        /// 获取按首次加入顺序保存的内部日志列表。调用方不得直接修改或在写入期间枚举。
        /// </summary>
        public List<EntryBinding> OriginalGroup { get; } = new List<EntryBinding>();
        /// <summary>
        /// 获取或设置当前排序字段；<see cref="EntryContentType.None"/> 表示原始顺序。
        /// </summary>
        public EntryContentType SortOrder { get; set; } = EntryContentType.None;
        /// <summary>
        /// 获取或设置是否反向枚举当前排序结果。
        /// </summary>
        public bool IsSortDescending { get; set; } = false;
        /// <summary>
        /// 获取各字段对应的内部排序索引。调用方不得直接修改集合或其排序集。
        /// </summary>
        public Dictionary<EntryContentType, SortedSet<EntryBinding>> SortedGroups { get; }
        /// <summary>
        /// 获取当前排序结果。<see cref="EntryContentType.None"/> 返回原始插入顺序；降序通过延迟反向枚举实现。
        /// 返回值直接引用内部集合，枚举期间不能并发写入。
        /// </summary>
        public IEnumerable<EntryBinding> SortedGroup
        {
            get
            {
                if (SortedGroups.TryGetValue(SortOrder, out var sortedSet))
                    return IsSortDescending ? sortedSet.Reverse() : sortedSet;
                else
                    return OriginalGroup;
            }
        }

        /// <summary>
        /// 创建日志分组，并为所有支持的字段建立带 ID 次级排序的索引。
        /// ID 次级排序既保证结果稳定，也避免主字段相同的不同日志被 <see cref="SortedSet{T}"/> 合并。
        /// </summary>
        public GroupBinding()
        {
            var comparerById = Comparer<EntryBinding>.Create(CompareId);
            var comparerByTimestamp = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.Timestamp.CompareTo(b.Entry.Timestamp);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByThreadId = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.ThreadId.CompareTo(b.Entry.ThreadId);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByFrame = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.Frame.CompareTo(b.Entry.Frame);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByScene = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = string.Compare(a.Scene, b.Scene, StringComparison.Ordinal);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByLevel = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.Level.CompareTo(b.Entry.Level);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByMessage = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = string.Compare(a.Message, b.Message, StringComparison.Ordinal);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByFile = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = string.Compare(a.File, b.File, StringComparison.Ordinal);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByLine = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.Line.CompareTo(b.Entry.Line);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByMember = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = string.Compare(a.Member, b.Member, StringComparison.Ordinal);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByException = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = string.Compare(a.Exception, b.Exception, StringComparison.Ordinal);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByLastRepeatTime = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.LastRepeatTime.CompareTo(b.Entry.LastRepeatTime);
                return CompareByIdFallback(a, b, result);
            });
            var comparerByRepeatCount = Comparer<EntryBinding>.Create((a, b) =>
            {
                var result = a.Entry.RepeatCount.CompareTo(b.Entry.RepeatCount);
                return CompareByIdFallback(a, b, result);
            });

            SortedGroups = new Dictionary<EntryContentType, SortedSet<EntryBinding>>
            {
                { EntryContentType.Id, new SortedSet<EntryBinding>(comparerById) },
                { EntryContentType.Timestamp, new SortedSet<EntryBinding>(comparerByTimestamp) },
                { EntryContentType.ThreadId, new SortedSet<EntryBinding>(comparerByThreadId) },
                { EntryContentType.Frame, new SortedSet<EntryBinding>(comparerByFrame) },
                { EntryContentType.Scene, new SortedSet<EntryBinding>(comparerByScene) },
                { EntryContentType.Level, new SortedSet<EntryBinding>(comparerByLevel) },
                { EntryContentType.Message, new SortedSet<EntryBinding>(comparerByMessage) },
                { EntryContentType.File, new SortedSet<EntryBinding>(comparerByFile) },
                { EntryContentType.Line, new SortedSet<EntryBinding>(comparerByLine) },
                { EntryContentType.Member, new SortedSet<EntryBinding>(comparerByMember) },
                { EntryContentType.Exception, new SortedSet<EntryBinding>(comparerByException) },
                { EntryContentType.LastRepeatTime, new SortedSet<EntryBinding>(comparerByLastRepeatTime) },
                { EntryContentType.RepeatCount, new SortedSet<EntryBinding>(comparerByRepeatCount) }
            };
        }

        /// <summary>
        /// 添加日志绑定并更新全部排序索引。
        /// 等价日志已存在时保留原绑定，但会从排序集合移除后重新加入，以反映底层重复时间和次数等已变排序字段。
        /// </summary>
        /// <param name="entry">要加入或刷新排序位置的日志绑定。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void AddEntry(EntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _lock.EnterWriteLock();

            if (!string.IsNullOrEmpty(entry.Exception))
                HasExceptionEntry = true;

            if (entry.IsRepeated)
                HasRepeatedEntry = true;

            var existingEntryList = OriginalGroup.Where(e => e.Equals(entry));
            if (existingEntryList.Any())
            {
                var existing = existingEntryList.First();
                // SortedSet 不会感知元素内部排序字段变化，必须先移除再加入以重建其树中位置。
                foreach (var sortedSet in SortedGroups.Values)
                    sortedSet.Remove(existing);

                HasRepeatedEntry = true;

                foreach (var sortedSet in SortedGroups.Values)
                    sortedSet.Add(existing);

                _lock.ExitWriteLock();
                return;
            }

            OriginalGroup.Add(entry);
            foreach (var sortedSet in SortedGroups.Values)
                sortedSet.Add(entry);
            _lock.ExitWriteLock();
        }

        /// <summary>
        /// 从原始集合和全部排序索引移除日志，并根据剩余项重新计算重复与异常标志。
        /// </summary>
        /// <param name="entry">要移除的日志绑定。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void RemoveEntry(EntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _lock.EnterWriteLock();

            OriginalGroup.Remove(entry);
            foreach (var sortedSet in SortedGroups.Values)
                sortedSet.Remove(entry);
            HasRepeatedEntry = OriginalGroup.Any(e => e.IsRepeated);
            HasExceptionEntry = OriginalGroup.Any(e => !string.IsNullOrEmpty(e.Exception));

            _lock.ExitWriteLock();
        }

        /// <summary>
        /// 主字段比较相等时按日志 ID 比较，以形成稳定且不会丢项的全序关系。
        /// </summary>
        /// <param name="a">左侧日志绑定。</param>
        /// <param name="b">右侧日志绑定。</param>
        /// <param name="result">主字段比较结果。</param>
        /// <returns>非零主比较结果，或主字段相等时的 ID 比较结果。</returns>
        public static int CompareByIdFallback(EntryBinding a, EntryBinding b, int result)
        {
            return result != 0 ? result : CompareId(a, b);
        }

        /// <summary>
        /// 按日志数据库分配的 ID 升序比较两个绑定。
        /// </summary>
        /// <param name="a">左侧日志绑定。</param>
        /// <param name="b">右侧日志绑定。</param>
        /// <returns>符合比较器约定的 ID 比较结果。</returns>
        public static int CompareId(EntryBinding a, EntryBinding b)
        {
            return a.Entry.Id.CompareTo(b.Entry.Id);
        }
    }
}
