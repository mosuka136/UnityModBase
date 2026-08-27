using UnityEngine;

namespace UnityModBase.HGuiSpace.Resource
{
    /// <summary>
    /// 定义可编辑分组界面在通用浮层样式之外所需的样式集合。
    /// </summary>
    public interface IEntryStyleResource : IStyleResource
    {
        /// <summary>获取侧栏普通分组按钮样式。</summary>
        GUIStyle SidebarEntryStyle { get; }

        /// <summary>获取侧栏选中分组按钮样式。</summary>
        GUIStyle SidebarSelectedEntryStyle { get; }

        /// <summary>获取分组标题样式。</summary>
        GUIStyle TableTitleStyle { get; }

        /// <summary>获取滑动条轨道样式。</summary>
        GUIStyle SliderStyle { get; }

        /// <summary>获取滑动条手柄样式。</summary>
        GUIStyle SliderThumbStyle { get; }
    }
}
