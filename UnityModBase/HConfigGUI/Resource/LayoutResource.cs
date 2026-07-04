using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Resource
{
    /// <summary>
    /// 配置界面布局尺寸计算器。
    /// 当前主要负责根据所有配置项名称计算统一标签列宽，避免不同表之间输入控件左右跳动。
    /// </summary>
    public class LayoutResource
    {
        public IUnityGuiProvider UnityGui { get; }

        public LayoutResource(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        public float GetEntryLabelWidth(GroupBinding root)
        {
            if (UnityGui == null || root == null)
                return 0f;

            float maxWidth = 0f;
            foreach (var entry in EnumerateEntries(root))
            {
                var width = UnityGui.LabelStyle.CalcSize(UnityGui.GetContent(entry.Name)).x;
                if (width > maxWidth)
                    maxWidth = width;
            }

            return maxWidth + 10f;
        }

        public float GetGroupButtonWidth(GroupBinding root)
        {
            if (UnityGui == null || root == null)
                return 0f;

            float maxWidth = 0f;
            foreach (var node in root.Children)
            {
                if (!(node is GroupBinding group))
                    continue;

                var width = UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(group.Name)).x;
                if (width > maxWidth)
                    maxWidth = width;
            }

            return maxWidth + 60f;
        }

        private static IEnumerable<IEntryBinding> EnumerateEntries(GroupBinding group)
        {
            foreach (var child in group.Children)
            {
                if (child is IEntryBinding entry)
                {
                    yield return entry;
                    continue;
                }

                if (!(child is GroupBinding childGroup))
                    continue;

                foreach (var descendant in EnumerateEntries(childGroup))
                    yield return descendant;
            }
        }
    }
}
