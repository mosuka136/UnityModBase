using System.Collections.Generic;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class GroupBinding : INodeBinding
    {
        private readonly List<INodeBinding> _children = new List<INodeBinding>();

        public string Key { get; }
        public Translator Name { get; }
        public Translator Description { get; }
        public IReadOnlyList<INodeBinding> Children => _children;

        public GroupBinding(string key, Translator name, Translator description, IEnumerable<INodeBinding> children = null)
        {
            Key = key ?? string.Empty;
            Name = name ?? new Translator();
            Description = description ?? new Translator();

            if (children == null)
                return;

            foreach (var child in children)
                Add(child);
        }

        public void Add(INodeBinding child)
        {
            if (child == null || ReferenceEquals(child, this) || _children.Contains(child))
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
