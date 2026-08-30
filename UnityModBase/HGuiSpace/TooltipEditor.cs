using UnityEngine;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 将当前 IMGUI 控件提供的工具提示绘制在鼠标附近，并限制在宿主窗口范围内。
    /// 本类只消费 <see cref="IUnityGuiProvider.Tooltip"/>，不负责决定哪个控件提供提示文本。
    /// </summary>
    public sealed class TooltipEditor
    {
        /// <summary>
        /// 获取鼠标位置和边界计算所用的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }
        /// <summary>
        /// 获取当前工具提示文本和绘制接口。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取工具提示样式资源。
        /// </summary>
        public IStyleResource StyleProvider { get; }

        /// <summary>
        /// 创建工具提示编辑器。依赖项在后续绘制期间必须保持有效。
        /// </summary>
        /// <param name="unityService">提供鼠标位置和数学运算的 Unity 服务。</param>
        /// <param name="unityGui">提供当前工具提示及绘制接口的 IMGUI 提供器。</param>
        /// <param name="styleProvider">提供工具提示样式的资源。</param>
        public TooltipEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, IStyleResource styleProvider)
        {
            UnityService = unityService;
            UnityGui = unityGui;
            StyleProvider = styleProvider;
        }

        /// <summary>
        /// 绘制当前工具提示，宽度最多占窗口的 60%，并将位置夹在窗口边界内。
        /// </summary>
        /// <param name="windowRect">工具提示可使用的窗口坐标范围。</param>
        public void DrawTooltip(Rect windowRect)
        {
            if (string.IsNullOrEmpty(UnityGui.Tooltip))
                return;

            var tooltipContent = UnityGui.GetContent(UnityGui.Tooltip);
            float maxTooltipWidth = windowRect.width * 0.6f;
            float tooltipWidth = UnityService.Min(StyleProvider.TooltipStyle.CalcSize(tooltipContent).x + 30f, maxTooltipWidth);
            float tooltipHeight = StyleProvider.TooltipStyle.CalcHeight(tooltipContent, tooltipWidth);

            var mousePosition = UnityService.CurrentMousePosition;
            float x = UnityService.Clamp(mousePosition.x + 15f, 0f, windowRect.width - tooltipWidth);
            float y = UnityService.Clamp(mousePosition.y + 15f, 0f, windowRect.height - tooltipHeight);

            UnityGui.Label(UnityGui.GetRect(x, y, tooltipWidth, tooltipHeight), tooltipContent, StyleProvider.TooltipStyle);
        }
    }
}
