using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HControlGUI.Bindings
{
    /// <summary>
    /// 把用户的实时控制服务投影为通用值编辑器可绘制的分组树。
    /// </summary>
    internal static class GroupBindingFactory
    {
        internal static GroupBinding CreateRoot(UserContext context)
        {
            var userId = context.UserId;
            var root = new GroupBinding(userId, new Translator(userId, userId), new Translator());
            var service = context.Service?.Control;
            if (service == null)
                return root;

            foreach (var tableEntry in service.Sheet)
            {
                var table = tableEntry.Value;
                var group = new GroupBinding(table.Key, table.Name, table.Description);
                foreach (var entry in table)
                    group.Add(new EntryBinding(entry));
                root.Add(group);
            }

            return root;
        }
    }
}
