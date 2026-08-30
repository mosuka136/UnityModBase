using UnityEngine;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 管理单条短时提示消息的计时、淡出与窗口内绘制。
    /// 计时使用不受游戏时间缩放影响的实时钟；新消息会覆盖尚未结束的旧消息。
    /// </summary>
    public sealed class ToastEditor
    {
        /// <summary>
        /// 获取用于实时计时和数值运算的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }
        /// <summary>
        /// 获取用于绘制提示的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取提示消息样式资源。
        /// </summary>
        public IStyleResource StyleProvider { get; }

        /// <summary>
        /// 获取或设置当前消息；null 或空字符串表示没有可绘制消息。
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// 获取或设置消息从设置到过期的持续时间，单位为秒，默认 2 秒；小于等于 0 时消息在下次绘制即过期。
        /// </summary>
        public float Duration { get; set; } = 2f;
        /// <summary>
        /// 获取或设置消息结束前的淡出时长，单位为秒，默认 0.5 秒；小于等于 0 时不进入淡出分支。
        /// </summary>
        public float FadeDuration { get; set; } = 0.5f;
        /// <summary>
        /// 获取或设置基于实时启动时钟的过期时间点，单位为秒。
        /// </summary>
        public float EndTime { get; set; }

        /// <summary>
        /// 创建提示消息编辑器。依赖项在后续绘制期间必须保持有效。
        /// </summary>
        /// <param name="unityService">提供实时启动时钟和数学运算的 Unity 服务。</param>
        /// <param name="unityGui">用于绘制提示的 IMGUI 提供器。</param>
        /// <param name="styleProvider">提供提示消息样式的资源。</param>
        public ToastEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, IStyleResource styleProvider)
        {
            UnityService = unityService;
            UnityGui = unityGui;
            StyleProvider = styleProvider;
        }

        /// <summary>
        /// 替换当前消息，并从调用时刻重新计算完整显示期限。
        /// </summary>
        /// <param name="message">待显示消息；null 或空字符串会被保存，但绘制时忽略。</param>
        public void SetToast(string message)
        {
            Message = message;
            EndTime = UnityService.RealtimeSinceStartup + Duration;
        }

        /// <summary>
        /// 在给定窗口底部居中绘制当前消息，并在临近过期时降低透明度。
        /// 绘制会临时修改 GUI 颜色，结束前恢复调用方原有颜色；过期消息会被清空。
        /// </summary>
        /// <param name="windowRect">用于约束提示宽度和位置的窗口矩形。</param>
        public void DrawToast(Rect windowRect)
        {
            if (string.IsNullOrEmpty(Message))
                return;

            float remaining = EndTime - UnityService.RealtimeSinceStartup;
            if (remaining <= 0f)
            {
                Message = null;
                return;
            }

            float alpha = remaining < FadeDuration ? remaining / FadeDuration : 1f;
            var previousColor = UnityGui.Color;
            UnityGui.Color = UnityGui.GetColor(1f, 1f, 1f, alpha);

            var content = UnityGui.GetContent(Message);
            var size = StyleProvider.ToastStyle.CalcSize(content);
            float toastWidth = UnityService.Min(size.x + 20f, windowRect.width - 20f);
            float toastHeight = size.y + 4f;
            float x = (windowRect.width - toastWidth) / 2f;
            float y = UnityService.Max(0f, windowRect.height - toastHeight - 30f);

            UnityGui.Label(UnityGui.GetRect(x, y, toastWidth, toastHeight), content, StyleProvider.ToastStyle);
            UnityGui.Color = previousColor;
        }
    }
}
