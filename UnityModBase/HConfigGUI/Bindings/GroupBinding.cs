using System;
using System.Collections.Generic;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

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
            Key = key ?? throw new ArgumentNullException(nameof(key), "Key cannot be null.");
            Name = name ?? new Translator();
            Description = description ?? new Translator();

            if (children == null)
                return;

            foreach (var child in children)
                Add(child);
        }

        public static GroupBinding CreateRoot(UserContext context)
        {
            var userId = context.UserId;
            var root = new GroupBinding(userId, new Translator(userId, userId), new Translator());

            if (context?.Service?.Config == null)
                return root;

            foreach (var table in context.Service.Config.Sheet)
            {
                var group = new GroupBinding(table.Key, table.Value.Name, table.Value.Description);

                foreach (IConfigEntry entry in table.Value)
                    group.Add(new EntryBinding(context.Service.ConfigManagerType, entry));

                root.Add(group);
            }

            return root;
        }

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
