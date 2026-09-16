using System;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// 按关键字判断绑定树节点是否应出现在过滤后的分组界面中。
    /// 空白查询视为未搜索；匹配对键、中英文名称和中英文说明做不区分大小写的子串比较，
    /// 中文还匹配无声调全拼与拼音首字母，不读取条目当前值。
    /// </summary>
    internal static class NodeSearch
    {
        /// <summary>
        /// 判断查询是否应启用过滤。
        /// </summary>
        /// <param name="query">用户输入的关键字；<c>null</c> 或全空白时视为未搜索。</param>
        /// <returns>存在非空白关键字时为 <c>true</c>。</returns>
        public static bool IsActive(string query)
        {
            return !string.IsNullOrWhiteSpace(query);
        }

        /// <summary>
        /// 判断节点自身的键、名称或说明是否包含关键字（含中文全拼与拼音首字母）。
        /// 查询未启用或节点为 <c>null</c> 时返回 <c>false</c>。
        /// </summary>
        /// <param name="node">待匹配的绑定节点。</param>
        /// <param name="query">用户输入的关键字。</param>
        /// <returns>节点自身匹配时为 <c>true</c>。</returns>
        public static bool Matches(INodeBinding node, string query)
        {
            if (node == null || !IsActive(query))
                return false;

            var trimmed = query.Trim();
            if (Contains(node.Key, trimmed))
                return true;
            if (ContainsTranslator(node.Name, trimmed))
                return true;
            if (ContainsTranslator(node.Description, trimmed))
                return true;

            return false;
        }

        /// <summary>
        /// 判断节点或其任意后代是否应在当前查询下保持可见。
        /// 查询未启用时任意非 <c>null</c> 节点都可见；节点为 <c>null</c> 时返回 <c>false</c>。
        /// 分组在自身匹配或任一后代匹配时可见。
        /// </summary>
        /// <param name="node">待检查的绑定节点。</param>
        /// <param name="query">用户输入的关键字。</param>
        /// <returns>该节点在过滤结果中应保留时为 <c>true</c>。</returns>
        public static bool ContainsMatch(INodeBinding node, string query)
        {
            if (node == null)
                return false;

            if (!IsActive(query))
                return true;

            if (Matches(node, query))
                return true;

            var group = node as GroupBinding;
            if (group == null)
                return false;

            foreach (var child in group.Children)
            {
                if (ContainsMatch(child, query))
                    return true;
            }

            return false;
        }

        private static bool ContainsTranslator(Translator translator, string query)
        {
            if (translator == null)
                return false;

            return Contains(translator.Chinese, query) || Contains(translator.English, query);
        }

        // 先做原文的不区分大小写子串匹配，未命中再尝试拼音匹配；拼音侧会另行压缩查询中的空白。
        // 调用方（Matches）已对 query 做过去首尾空白处理。
        private static bool Contains(string text, string query)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || PinyinText.Contains(text, query);
        }
    }
}
