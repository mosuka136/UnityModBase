using System;
using System.Collections.Generic;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class UserBinding : UserBindingBase<GroupBinding>
    {
        public UserBinding() : this(UserManager.UserContexts)
        {
        }

        public UserBinding(IEnumerable<UserContext> contexts) : base(contexts, CreateRoot)
        {
        }

        private static GroupBinding CreateRoot(UserContext context)
        {
            var userId = context?.UserId ?? throw new ArgumentNullException(nameof(context));
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
    }
}
