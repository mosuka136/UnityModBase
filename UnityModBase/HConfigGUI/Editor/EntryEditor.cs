using System;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 绘制单个配置项的一行通用结构，并把具体值控件委托给全局注册表 <see cref="ValueEditorRegistry"/> 选出的 <see cref="ValueEditor.IValueEditor"/>。
    /// 本类负责名称、说明、重置按钮和变更入口，不负责选择分组或推进延迟提交。
    /// </summary>
    public class EntryEditor
    {
        /// <summary>
        /// 获取配置行绘制所用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建配置项行编辑器。
        /// </summary>
        /// <param name="unity">用于绘制配置行的 IMGUI 提供器。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unity"/> 为 null。</exception>
        public EntryEditor(IUnityGuiProvider unity)
        {
            UnityGui = unity ?? throw new ArgumentNullException(nameof(unity), "UnityGui cannot be null.");
        }

        /// <summary>
        /// 绘制配置项标签、匹配的值控件、重置按钮及编辑器附加区域。
        /// 重置操作会立即写回默认值并触发上下文变更通知；无效上下文只记录错误并停止绘制。
        /// </summary>
        /// <param name="entry">要绘制的配置项绑定。</param>
        /// <param name="context">提供布局状态和变更提交器的有效 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 或 <paramref name="context"/> 为 null。</exception>
        /// <remarks>
        /// 值编辑器或配置回调抛出异常时仍会闭合当前水平布局，异常随后继续传播；
        /// 只有通用配置行完整绘制后才会调用值编辑器的附加区域。
        /// </remarks>
        public void Draw(IEntryBinding entry, GuiContext context)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");
            
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Config entry draw skipped because the GUI context is invalid. Operation='{nameof(Draw)}'.");
                return;
            }

            var editor = ValueEditorRegistry.GetEditor(entry);

            UnityGui.BeginHorizontal();
            try
            {
                UnityGui.Label(UnityGui.GetContent(entry.Name, entry.Description), UnityGui.Width(context.GetEntryLabelWidth(context.SelectedGroupKey)));
                editor.DrawValue(entry, context);
                if (UnityGui.Button(TranslatorResource.Reset, UnityGui.ExpandWidth(false)))
                    context.ChangeSink.ResetValue(entry);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }

            editor.DrawExtra(entry, context);
        }
    }
}
