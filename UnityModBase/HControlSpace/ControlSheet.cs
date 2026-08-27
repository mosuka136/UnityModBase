using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 按声明顺序保存实时控制表。
    /// </summary>
    public sealed class ControlSheet : IEnumerable<KeyValuePair<string, ControlTable>>
    {
        private readonly OrderedDictionary _tables = new OrderedDictionary();

        /// <summary>获取表数量。</summary>
        public int Count => _tables.Count;

        /// <summary>获取指定键的表；不存在时返回 null。</summary>
        public ControlTable this[string key] => (ControlTable)_tables[key];

        /// <summary>判断是否存在指定表键。</summary>
        public bool Contains(string key)
        {
            return _tables.Contains(key);
        }

        internal void Add(string key, ControlTable table)
        {
            _tables.Add(key, table);
        }

        internal void Clear()
        {
            _tables.Clear();
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, ControlTable>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _tables)
                yield return new KeyValuePair<string, ControlTable>((string)entry.Key, (ControlTable)entry.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
