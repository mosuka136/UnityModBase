using System;
using System.Collections.Generic;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 表示可编辑 GUI 中可嵌套的分组节点，并维护不重复添加同一节点引用、无环的子节点集合。
    /// 对外暴露的子节点视图不可直接修改，但会实时反映通过 <see cref="Add"/> 完成的后续添加。
    /// </summary>
    public class GroupBinding : INodeBinding
    {
        private readonly List<INodeBinding> _children = new List<INodeBinding>();
        private readonly IReadOnlyList<INodeBinding> _childrenView;

        /// <inheritdoc/>
        public string Key { get; }

        /// <inheritdoc/>
        public Translator Name { get; }

        /// <inheritdoc/>
        public Translator Description { get; }

        /// <summary>
        /// 获取不可直接修改但会实时反映后续添加的子节点视图。
        /// </summary>
        public IReadOnlyList<INodeBinding> Children => _childrenView;

        /// <summary>
        /// 创建分组。名称为 null 时以键作为中英文默认名称，说明为 null 时使用空翻译对象，子节点为 null 时创建空组。
        /// </summary>
        /// <param name="key">分组的稳定键。</param>
        /// <param name="name">分组显示名称；null 时以键作为中英文默认名称。</param>
        /// <param name="description">分组说明；null 时使用空翻译对象。</param>
        /// <param name="children">可选初始子节点序列。</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> 为 null。</exception>
        public GroupBinding(string key, Translator name, Translator description = null, IEnumerable<INodeBinding> children = null)
        {
            _childrenView = _children.AsReadOnly();
            Key = key ?? throw new ArgumentNullException(nameof(key), "Key cannot be null.");
            Name = name ?? new Translator(key, key);
            Description = description ?? new Translator();

            if (children == null)
                return;

            foreach (var child in children)
                Add(child);
        }

        /// <summary>
        /// 添加子节点。同一节点引用、自引用及会形成环的分组会被忽略；不同实例允许使用相同键。
        /// </summary>
        /// <param name="child">待添加节点。</param>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> 为 null。</exception>
        public void Add(INodeBinding child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child), "Child cannot be null.");

            if (ReferenceEquals(child, this) || _children.Contains(child))
                return;

            if (child is GroupBinding group && group.Contains(this))
                return;

            _children.Add(child);
        }

        private bool Contains(INodeBinding node)
        {
            foreach (var child in _children)
            {
                if (ReferenceEquals(child, node))
                    return true;

                if (child is GroupBinding group && group.Contains(node))
                    return true;
            }

            return false;
        }
    }
}
