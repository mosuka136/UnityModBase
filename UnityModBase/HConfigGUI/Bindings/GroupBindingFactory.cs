using UnityModBase.HConfigSpace;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    /// <summary>
    /// 将用户配置表结构投影为通用可编辑 GUI 绑定树。
    /// </summary>
    internal static class GroupBindingFactory
    {
        /// <summary>
        /// 创建以用户标识为根键的配置绑定树；用户尚无配置服务时返回空根节点。
        /// </summary>
        internal static GroupBinding CreateRoot(UserContext context)
        {
            var userId = context.UserId;
            var root = new GroupBinding(userId, new Translator(userId, userId), new Translator());

            if (context?.Service?.Config == null)
                return root;

            foreach (var table in context.Service.Config.Sheet)
            {
                var group = new GroupBinding(table.Key, table.Value.Name, table.Value.Description);

                foreach (IConfigEntry entry in table.Value)
                {
                    // 多元素条目改用多元素绑定：在单值行为之外透出各元素说明，组合编辑器依赖它绘制带独立提示的槽位。
                    if (entry is IEntryMultiple entryMultiple)
                        group.Add(new EntryMultipleBinding(context.Service.ConfigManagerType, entryMultiple));
                    else
                        group.Add(new EntryBinding(context.Service.ConfigManagerType, entry));
                }

                root.Add(group);
            }

            return root;
        }
    }
}
