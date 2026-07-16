using UnityEngine;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 定义通用 GUI 浮层所需的最小样式集合。
    /// 各业务界面可提供更多样式，但必须让提示消息和工具提示共享同一 IMGUI 提供器。
    /// </summary>
    public interface IStyleResource
    {
        /// <summary>
        /// 获取创建和绘制样式时使用的 IMGUI 抽象。
        /// </summary>
        IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取短时提示消息样式。
        /// </summary>
        GUIStyle ToastStyle { get; }
        /// <summary>
        /// 获取鼠标悬停工具提示样式。
        /// </summary>
        GUIStyle TooltipStyle { get; }
    }
}
