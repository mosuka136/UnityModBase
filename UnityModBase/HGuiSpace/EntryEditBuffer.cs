using System;
using System.Collections.Generic;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 保存单个可编辑条目来自一个或多个编辑控件的暂存值，并以单调递增序号确定最后一次输入。
    /// 缓冲区既保留无效文本用于回显，也阻止其写入底层值；实例不提供线程安全保证。
    /// </summary>
    public class EntryEditBuffer
    {
        private readonly Dictionary<string, OrderedEntry> _buffer = new Dictionary<string, OrderedEntry>();

        /// <summary>
        /// 获取无来源键暂存槽中的值及其顺序信息。
        /// </summary>
        public OrderedEntry Value { get; private set; }

        /// <summary>
        /// 获取下次无键或有键写入将使用的顺序号；清空后重置为 0。
        /// </summary>
        public int Seq { get; private set; }

        /// <summary>
        /// 指示缓冲区自上次清空后是否接收过输入，包括无效输入。
        /// </summary>
        public bool IsUsing { get; private set; }

        /// <summary>
        /// 创建处于未使用状态的空编辑缓冲区。
        /// </summary>
        public EntryEditBuffer()
        {
            Value = OrderedEntry.Empty;
            Seq = 0;
            IsUsing = false;
        }

        /// <summary>
        /// 写入无来源键暂存槽，并使其成为当前最新输入。
        /// </summary>
        /// <param name="value">要暂存的输入值。</param>
        /// <param name="isValid">该输入是否可提交到底层条目。</param>
        public void SetValue(object value, bool isValid)
        {
            Value = new OrderedEntry(value, Seq++, isValid);
            IsUsing = true;
        }

        /// <summary>
        /// 写入指定来源槽，并使其成为当前最新输入。
        /// </summary>
        /// <param name="key">区分输入来源的非空键。</param>
        /// <param name="value">要暂存的输入值。</param>
        /// <param name="isValid">该输入是否可提交到底层条目。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 null 或空字符串。</exception>
        public void SetValue(string key, object value, bool isValid)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            _buffer[key] = new OrderedEntry(value, Seq++, isValid);
            IsUsing = true;
        }

        /// <summary>
        /// 将调用方提供的有序值放入指定来源槽；其顺序号会原样保留，不推进 <see cref="Seq"/>。
        /// </summary>
        /// <param name="key">区分输入来源的非空键。</param>
        /// <param name="entry">包含顺序和有效性的暂存值。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 null 或空字符串。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void SetValue(string key, OrderedEntry entry)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            _buffer[key] = entry ?? throw new ArgumentNullException(nameof(entry));
            IsUsing = true;
        }

        /// <summary>
        /// 获取指定来源槽；不存在时返回共享的 <see cref="OrderedEntry.Empty"/> 哨兵。
        /// </summary>
        /// <param name="key">要查询的来源键。</param>
        /// <returns>来源槽中的值，或共享空值哨兵。</returns>
        public OrderedEntry GetValue(string key)
        {
            if (_buffer.TryGetValue(key, out var entry))
                return entry;
            return OrderedEntry.Empty;
        }

        /// <summary>
        /// 在无键槽和全部有键槽之间选择顺序号最大的值。
        /// </summary>
        /// <returns>最新有序值；空缓冲区返回共享空值哨兵。</returns>
        public OrderedEntry GetLatestValue()
        {
            OrderedEntry latestEntry = Value;
            foreach (var entry in _buffer.Values)
            {
                if (entry.Order > latestEntry.Order)
                    latestEntry = entry;
            }
            return latestEntry;
        }

        /// <summary>
        /// 提交全缓冲区中的最新有效值，可在写入前做一次转换；方法正常完成后，无论是否写入都会清空整个缓冲区。
        /// </summary>
        /// <param name="entry">接收提交值的条目绑定。</param>
        /// <param name="transform">写入前的可选值转换函数。</param>
        /// <returns>仅当目标条目值实际变化时返回 true。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public bool Commit(IEntryBinding entry, Func<object, object> transform = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var result = false;
            var latestEntry = GetLatestValue();
            if (!latestEntry.IsEmpty && latestEntry.IsValid)
            {
                var value = transform != null ? transform(latestEntry.Value) : latestEntry.Value;
                if (!Equals(entry.Value, value))
                {
                    entry.Value = value;
                    result = true;
                }
            }

            Clear();
            return result;
        }

        /// <summary>
        /// 仅选择指定来源槽的有效值提交，但方法正常完成后仍会清空其他来源的暂存值。
        /// </summary>
        /// <param name="key">要提交的来源键。</param>
        /// <param name="entry">接收提交值的条目绑定。</param>
        /// <param name="transform">写入前的可选值转换函数。</param>
        /// <returns>仅当目标条目值实际变化时返回 true。</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 null 或空字符串。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public bool Commit(string key, IEntryBinding entry, Func<object, object> transform = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var result = false;
            var latestEntry = GetValue(key);
            if (!latestEntry.IsEmpty && latestEntry.IsValid)
            {
                var value = transform != null ? transform(latestEntry.Value) : latestEntry.Value;
                if (!Equals(entry.Value, value))
                {
                    entry.Value = value;
                    result = true;
                }
            }

            Clear();
            return result;
        }

        /// <summary>
        /// 丢弃全部暂存值并重置顺序号和使用状态。
        /// </summary>
        public void Clear()
        {
            Value = OrderedEntry.Empty;
            _buffer.Clear();
            Seq = 0;
            IsUsing = false;
        }

        /// <summary>
        /// 封装暂存值、跨来源比较所需的写入顺序及有效性。
        /// </summary>
        public class OrderedEntry
        {
            /// <summary>
            /// 获取或设置用于跨来源比较先后的顺序号；数值越大表示越新。
            /// </summary>
            public int Order { get; set; }
            /// <summary>
            /// 获取或设置暂存的输入值。
            /// </summary>
            public object Value { get; set; }
            /// <summary>
            /// 获取或设置该输入是否允许提交。
            /// </summary>
            public bool IsValid { get; set; }

            /// <summary>
            /// 表示来源槽不存在的共享哨兵。调用方不应修改其可写属性。
            /// </summary>
            public static readonly OrderedEntry Empty = new OrderedEntry(int.MinValue, int.MinValue, false);

            /// <summary>
            /// 指示当前实例是否为共享的空值哨兵。
            /// </summary>
            public bool IsEmpty => ReferenceEquals(this, Empty);

            /// <summary>
            /// 创建带顺序和有效性标记的暂存值。
            /// </summary>
            /// <param name="value">暂存输入值。</param>
            /// <param name="order">跨来源比较使用的顺序号。</param>
            /// <param name="isValid">该输入是否允许提交。</param>
            public OrderedEntry(object value, int order, bool isValid)
            {
                Value = value;
                Order = order;
                IsValid = isValid;
            }
        }
    }
}
