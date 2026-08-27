using System;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// 绘制单个条目的一行通用结构，并把具体值控件委托给注册表选出的 <see cref="IValueEditor"/>。
    /// 派生类可以在值控件之后追加业务操作。
    /// </summary>
    public class EntryEditor
    {
        private readonly Func<IEntryBinding, IValueEditor> _editorResolver;

        /// <summary>
        /// 获取条目行绘制所用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 获取值控件之后的业务操作占用宽度。
        /// </summary>
        public virtual float TrailingActionWidth => 0f;

        /// <summary>
        /// 创建条目行编辑器。
        /// </summary>
        /// <param name="unity">用于绘制条目行的 IMGUI 提供器。</param>
        /// <param name="registry">用于解析条目值编辑器的实例注册表。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unity"/> 为 null。</exception>
        public EntryEditor(IUnityGuiProvider unity, ValueEditorRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            UnityGui = unity ?? throw new ArgumentNullException(nameof(unity), "UnityGui cannot be null.");
            _editorResolver = registry.GetEditor;
        }

        /// <summary>
        /// 绘制条目标签、匹配的值控件、可选尾部操作及编辑器附加区域。
        /// 无效上下文只记录错误并停止绘制。
        /// </summary>
        /// <param name="entry">要绘制的条目绑定。</param>
        /// <param name="context">提供布局状态和变更提交器的有效 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 或 <paramref name="context"/> 为 null。</exception>
        /// <remarks>
        /// 值编辑器或提交回调抛出异常时仍会闭合当前水平布局，异常随后继续传播；
        /// 只有通用条目行完整绘制后才会调用值编辑器的附加区域。
        /// </remarks>
        public void Draw(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");
            
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Entry draw skipped because the GUI context is invalid. Operation='{nameof(Draw)}'.");
                return;
            }

            var editor = _editorResolver(entry);

            UnityGui.BeginHorizontal();
            try
            {
                UnityGui.Label(UnityGui.GetContent(entry.Name, entry.Description), UnityGui.Width(context.GetEntryLabelWidth(context.SelectedGroupKey)));
                editor.DrawValue(entry, context);
                DrawTrailingAction(entry, context);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }

            editor.DrawExtra(entry, context);
        }

        /// <summary>
        /// 在主值控件之后绘制可选业务操作。
        /// </summary>
        protected virtual void DrawTrailingAction(IEntryBinding entry, EditableGuiContext context)
        {
        }
    }
}
