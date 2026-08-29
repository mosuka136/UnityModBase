using System;
using System.Collections;
using System.Collections.Generic;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 按声明顺序保存共享条目的基础表，供运行时配置表与实时控制表复用。
    /// 顺序即绑定顺序，GUI 按此顺序展示条目。条目通过 <see cref="Add"/> 登记时校验归属表键与键名唯一性；
    /// <see cref="Entries"/> 暴露只读视图以防止绕过校验的直接修改。
    /// 该类型不提供并发保护，绑定和遍历应由调用方串行化。
    /// </summary>
    /// <typeparam name="T">条目类型。</typeparam>
    public abstract class TableBase<T> : IEnumerable<T> where T : IEntry
    {
        /// <summary>保存表内条目的可变列表。</summary>
        protected readonly List<T> _entries = new List<T>();

        /// <summary>向调用方公开的只读条目视图。</summary>
        protected readonly IReadOnlyList<T> _entriesView;

        /// <summary>获取稳定表键。</summary>
        public string Key { get; }

        /// <summary>获取显示名称。</summary>
        public Translator Name { get; }

        /// <summary>获取显示说明。</summary>
        public Translator Description { get; }

        /// <summary>获取不可直接修改的实时条目视图。</summary>
        public virtual IReadOnlyList<T> Entries => _entriesView;

        /// <summary>获取表内条目数量。</summary>
        public int Count => _entries.Count;

        /// <summary>创建共享条目表。</summary>
        /// <param name="key">稳定表键。</param>
        /// <param name="name">显示名称。</param>
        /// <param name="description">显示说明；为 <c>null</c> 时使用空翻译。</param>
        public TableBase(string key, Translator name, Translator description = null)
        {
            if (!EntryModel.IsValidTableKey(key))
                throw new ArgumentException($"Invalid table name: {key}.", nameof(key));
            Key = key;
            Name = name ?? new Translator();
            Description = description ?? new Translator();
            _entriesView = _entries.AsReadOnly();
        }

        /// <summary>判断表内是否存在指定条目键。</summary>
        /// <param name="key">待查找的条目键。</param>
        /// <returns>存在时返回 <c>true</c>。</returns>
        public virtual bool Contains(string key)
        {
            foreach (var entry in _entries)
            {
                if (entry.Key == key)
                    return true;
            }
            return false;
        }

        /// <summary>向表中添加条目。</summary>
        /// <param name="entry">待添加条目，其 <c>TableKey</c> 必须等于本表键名。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException"><paramref name="entry"/> 声明归属其他表。</exception>
        /// <exception cref="InvalidOperationException">本表已存在同键条目。</exception>
        /// <remarks>本方法只修改运行时列表；配置条目的文件项创建与自动保存订阅由 <see cref="HConfigSpace.ConfigService"/> 的 <c>Bind</c> 重载负责。</remarks>
        public virtual void Add(T entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (entry.TableKey != Key)
                throw new ArgumentException($"Entry {entry.Key} belongs to table {entry.TableKey}, not {Key}.", nameof(entry));
            if (Contains(entry.Key))
                throw new InvalidOperationException($"Entry already exists: {Key}.{entry.Key}.");
            _entries.Add(entry);
        }

        /// <inheritdoc/>
        public virtual IEnumerator<T> GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
