using System;
using System.Linq;
using UnityEngine;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 将单个日志字段绘制为可复制按钮。
    /// 多行内容只在单元格显示首个非空行，完整文本保留为工具提示并在点击时写入系统剪贴板。
    /// </summary>
    public class EntryEditor
    {
        /// <summary>
        /// 完整文本复制成功后触发，参数为供提示消息显示的“已复制 + 首行摘要”文本。
        /// </summary>
        public event Action<string> OnEntryCopied;

        /// <summary>
        /// 获取日志字段按钮使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取系统剪贴板写入所用的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }

        /// <summary>
        /// 创建日志字段编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制字段按钮的 IMGUI 提供器。</param>
        /// <param name="unityService">用于写入系统剪贴板的 Unity 服务。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public EntryEditor(IUnityGuiProvider unityGui, IUnityProvider unityService)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
        }

        /// <summary>
        /// 绘制固定宽度字段按钮。点击会把原始完整文本复制到系统剪贴板并发送通知。
        /// </summary>
        /// <param name="text">完整字段文本，不得为 null。</param>
        /// <param name="style">列样式，不得为 null。</param>
        /// <param name="width">单元格固定宽度，单位为像素。</param>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> 或 <paramref name="style"/> 为 null。</exception>
        public void Draw(string text, GUIStyle style, float width)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text), "Text cannot be null.");

            if (style == null)
                throw new ArgumentNullException(nameof(style), "Style cannot be null.");

            UnityGui.BeginHorizontal();

            var splitText = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            splitText = splitText.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var showText = splitText.Length > 0 ? splitText[0] : " ";

            var content = splitText.Length > 1 ? UnityGui.GetContent(showText, text) : UnityGui.GetContent(showText);
            if (UnityGui.Button(content, style, UnityGui.Width(width)))
            {
                UnityService.ClipboardCopy(text);
                OnEntryCopied?.Invoke(TranslatorResource.Copied + showText);
            }

            UnityGui.EndHorizontal();
        }
    }
}
