using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 按插入顺序保存共享条目表的基础集合，供运行时配置表集合与实时控制表集合复用。
    /// 顺序对 GUI 分组展示和文件写出的稳定性至关重要，因此底层使用有序字典而非哈希容器。
    /// 集合不提供并发保护，登记与遍历应由调用方串行化。
    /// </summary>
    /// <typeparam name="TKey">表键类型。</typeparam>
    /// <typeparam name="TValue">表值类型。</typeparam>
    public abstract class SheetBase<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>保存表键和值的有序字典。</summary>
        protected readonly OrderedDictionary _tables = new OrderedDictionary();

        /// <summary>获取表数量。</summary>
        public int Count => _tables.Count;

        /// <summary>获取指定键的表；不存在时返回 null。</summary>
        public TValue this[TKey key] => (TValue)_tables[key];

        /// <summary>按插入顺序枚举表键。</summary>
        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (DictionaryEntry entry in _tables)
                    yield return (TKey)entry.Key;
            }
        }

        /// <summary>按插入顺序枚举表值。</summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (DictionaryEntry entry in _tables)
                    yield return (TValue)entry.Value;
            }
        }

        /// <summary>判断是否存在指定表键。</summary>
        public virtual bool Contains(TKey key)
        {
            return _tables.Contains(key);
        }

        /// <summary>添加一个表。</summary>
        /// <param name="key">用于索引表的键。</param>
        /// <param name="table">要添加的表。</param>
        /// <exception cref="System.ArgumentException">键已存在（沿用 <see cref="OrderedDictionary.Add(object, object)"/> 的约束）。</exception>
        public virtual void Add(TKey key, TValue table)
        {
            _tables.Add(key, table);
        }

        /// <summary>移除全部表。</summary>
        public virtual void Clear()
        {
            _tables.Clear();
        }

        /// <inheritdoc/>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _tables)
                yield return new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
