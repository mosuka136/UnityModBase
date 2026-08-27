using System;
using System.Collections;
using System.Collections.Generic;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 按声明顺序保存一组实时控制项。
    /// </summary>
    public sealed class ControlTable : IEnumerable<IControlEntry>
    {
        private readonly List<IControlEntry> _entries = new List<IControlEntry>();
        private readonly IReadOnlyList<IControlEntry> _entriesView;

        /// <summary>获取稳定表键。</summary>
        public string Key { get; }

        /// <summary>获取显示名称。</summary>
        public Translator Name { get; }

        /// <summary>获取显示说明。</summary>
        public Translator Description { get; }

        /// <summary>获取不可直接修改的实时条目视图。</summary>
        public IReadOnlyList<IControlEntry> Entries => _entriesView;

        internal ControlTable(string key, Translator name, Translator description)
        {
            Key = key;
            Name = name;
            Description = description ?? new Translator();
            _entriesView = _entries.AsReadOnly();
        }

        internal bool Contains(string key)
        {
            foreach (var entry in _entries)
            {
                if (entry.Key == key)
                    return true;
            }
            return false;
        }

        internal void Add(IControlEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (entry.TableKey != Key)
                throw new ArgumentException($"Control entry {entry.Key} belongs to table {entry.TableKey}, not {Key}.", nameof(entry));
            if (Contains(entry.Key))
                throw new InvalidOperationException($"Control entry already exists: {Key}.{entry.Key}.");
            _entries.Add(entry);
        }

        /// <inheritdoc/>
        public IEnumerator<IControlEntry> GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
