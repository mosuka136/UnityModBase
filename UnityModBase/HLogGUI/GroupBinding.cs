using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 为单个日志 GUI 分组维护按日志等价规则去重的绑定、特殊内容标志和排序缓存。
    /// 本类不累计重复次数；日志数据库会先原地更新 <see cref="EntryBinding.Entry"/>，再通过等价绑定通知本类刷新排序结果。
    /// </summary>
    /// <remarks>
    /// 添加、移除和排序快照重建通过实例锁串行化。<see cref="SortedGroup"/> 返回内部缓存的物化结果，
    /// 调用方只能枚举，不得通过类型转换修改该集合。调用方应在 GUI 线程读取或修改排序设置并读取派生标志；
    /// 这些访问不提供与日志回调线程之间的线性一致快照。
    /// </remarks>
    public class GroupBinding
    {
        // 串行化绑定列表、派生标志，以及 AddEntry/RemoveEntry 与 SortedGroup 之间的缓存失效和重建。
        // SortOrder、IsSortDescending 和派生标志的读取不取得本锁，也不防止调用方修改返回的集合。
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // 按首次接收顺序保存每组等价日志的首个绑定；后续等价绑定只用于使排序缓存失效。
        private readonly List<EntryBinding> _originalGroup = new List<EntryBinding>();
        private EntryContentType _sortOrder = EntryContentType.None;

        // 缓存当前字段对应的物化排序快照；快照保留绑定引用，但条目字段变化不会自动调整其既有位置。
        // null 表示尚未创建快照，或字段、绑定集合、重复次数等可能影响排序的状态已经变化。
        private IEnumerable<EntryBinding> _sortedGroup = null;

        /// <summary>
        /// 获取分组是否已观察到累计出现多次的日志。
        /// 添加时会依据传入绑定置位，包括仅用于刷新缓存的等价绑定；移除时按剩余绑定重算。
        /// </summary>
        public bool HasRepeatedEntry { get; private set; } = false;

        /// <summary>
        /// 获取分组是否已观察到带异常的日志。
        /// 添加时会依据传入绑定置位，包括仅用于刷新缓存的等价绑定；移除时按剩余绑定重算。
        /// </summary>
        public bool HasExceptionEntry { get; private set; } = false;

        /// <summary>
        /// 获取或设置当前排序字段。<see cref="EntryContentType.None"/>、<see cref="EntryContentType.Id"/> 和未知值均回退为按日志 ID 排序。
        /// 更换字段会丢弃已缓存的排序快照；该属性应与 <see cref="SortedGroup"/> 在同一 GUI 线程访问。
        /// </summary>
        public EntryContentType SortOrder
        {
            get => _sortOrder;
            set
            {
                if (_sortOrder != value)
                {
                    _sortOrder = value;
                    _sortedGroup = null;
                }
            }
        }

        /// <summary>
        /// 获取或设置是否反向枚举当前排序快照。该属性不重建快照，应由 GUI 线程修改。
        /// </summary>
        public bool IsSortDescending { get; set; } = false;

        /// <summary>
        /// 获取当前字段对应的物化排序快照。
        /// 除 ID 排序外，主字段相同时以日志 ID 作为次级键；字符串字段使用 LINQ 默认比较器，因此受当前区域性影响。
        /// 降序会反转完整结果，主字段相同时的 ID 顺序也随之反转。
        /// </summary>
        /// <remarks>
        /// 缓存失效后的首次访问会在写锁内完成排序和复制。快照保留 <see cref="EntryBinding"/> 引用，
        /// 因而字段文本可反映底层日志的后续更新，但排序位置要等 <see cref="AddEntry"/>、<see cref="RemoveEntry"/>
        /// 或 <see cref="SortOrder"/> 使缓存失效后才会重算。升序结果的运行时对象是内部列表，调用方不得将返回值
        /// 转换为可变集合，否则会破坏当前缓存；降序结果是该列表的延迟反向枚举。
        /// </remarks>
        public IEnumerable<EntryBinding> SortedGroup
        {
            get
            {
                var sorted = _sortedGroup;

                _lock.EnterWriteLock();
                try
                {
                    if (_sortedGroup == null)
                    {
                        switch (_sortOrder)
                        {
                            case EntryContentType.Timestamp:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Timestamp).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.ThreadId:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.ThreadId).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Frame:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Frame).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Scene:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Scene).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Level:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Level).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Message:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Message).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.File:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.File).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Line:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Line).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Member:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Member).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.Exception:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Exception).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.LastRepeatTime:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.LastRepeatTime).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.RepeatCount:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.RepeatCount).ThenBy(e => e.Entry.Id).ToList();
                                break;
                            case EntryContentType.None:
                            case EntryContentType.Id:
                            default:
                                _sortedGroup = _originalGroup.OrderBy(e => e.Entry.Id).ToList();
                                break;
                        }
                    }

                    sorted = _sortedGroup;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                return IsSortDescending ? sorted.Reverse() : sorted;
            }
        }

        /// <summary>
        /// 创建空日志分组；排序快照在首次读取 <see cref="SortedGroup"/> 时按需创建。
        /// </summary>
        public GroupBinding()
        {
        }

        /// <summary>
        /// 在不存在等价日志时追加绑定，并根据新条目置位异常和重复标志。
        /// 若已存在等价日志，则保留原绑定、依据传入绑定更新特殊内容标志并使排序快照失效；本类不会合并重复次数或替换绑定。
        /// </summary>
        /// <param name="entry">要加入或用于刷新排序缓存的绑定。分组保存该实例，不创建副本。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        /// <exception cref="NullReferenceException"><paramref name="entry"/> 的底层日志为 null。</exception>
        public void AddEntry(EntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _lock.EnterWriteLock();
            try
            {
                if (!string.IsNullOrEmpty(entry.Exception))
                    HasExceptionEntry = true;

                if (entry.IsRepeated)
                    HasRepeatedEntry = true;

                if (GetSameEntries(entry).Any())
                    return;

                _originalGroup.Add(entry);
            }
            finally
            {
                _sortedGroup = null;
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 移除所有与指定日志满足合并等价条件的绑定，并根据剩余项重算重复与异常标志。
        /// 没有匹配项或底层日志为 null 时仅重算标志并使排序快照失效。
        /// </summary>
        /// <param name="entry">提供底层日志等价条件的绑定。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void RemoveEntry(EntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _lock.EnterWriteLock();
            try
            {
                foreach (var e in GetSameEntries(entry))
                    _originalGroup.Remove(e);
                HasRepeatedEntry = _originalGroup.Any(e => e.IsRepeated);
                HasExceptionEntry = _originalGroup.Any(e => !string.IsNullOrEmpty(e.Exception));
            }
            finally
            {
                _sortedGroup = null;
                _lock.ExitWriteLock();
            }
        }

        private bool EntryEquals(EntryBinding a, EntryBinding b)
        {
            if (a == null || b == null)
                return false;

            if (a.Entry == null || b.Entry == null)
                return false;

            return a.Entry.Equals(b.Entry);
        }

        private IEnumerable<EntryBinding> GetSameEntries(EntryBinding entry)
        {
            if (entry == null || entry.Entry == null)
                return Enumerable.Empty<EntryBinding>();

            // 先物化匹配项，避免 RemoveEntry 修改源列表时使 Where 枚举器失效。
            return _originalGroup.Where(e => EntryEquals(e, entry)).ToList();
        }
    }
}
