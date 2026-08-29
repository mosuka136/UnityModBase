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
        /// <summary>
        /// 以用户标识为根键创建绑定树：每个实时控制表映射为一个分组，组内按声明顺序排列条目绑定。
        /// 每次调用都重建整棵树，供模型结构变化后整体替换。
        /// </summary>
        /// <param name="context">提供实时控制服务的用户上下文；无控制服务时仅返回空根节点。</param>
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
