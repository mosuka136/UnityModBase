using System;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 条目值控件的统一契约。注册表先调用 <see cref="CanEdit"/> 选择编辑器，再在同一条目行中依次调用
    /// <see cref="DrawValue"/> 和 <see cref="DrawExtra"/>；实现应通过上下文变更提交器写值，不应绕过编辑缓冲区。
    /// </summary>
    public interface IValueEditor : IDisposable
    {
        /// <summary>
        /// 判断当前实现是否支持目标条目；注册表采用首个返回 true 的实现。
        /// </summary>
        /// <param name="entry">待匹配的条目绑定。</param>
        /// <returns>当前实现支持该条目时返回 true。</returns>
        bool CanEdit(IEntryBinding entry);

        /// <summary>
        /// 绘制条目行内的主值控件。
        /// </summary>
        /// <param name="entry">要绘制的条目绑定。</param>
        /// <param name="context">提供编辑状态和变更提交器的 GUI 上下文。</param>
        void DrawValue(IEntryBinding entry, EditableGuiContext context);

        /// <summary>
        /// 绘制条目行之后的可选扩展区域；无需扩展内容的实现可以留空。
        /// </summary>
        /// <param name="entry">要绘制附加内容的条目绑定。</param>
        /// <param name="context">提供编辑状态和变更提交器的 GUI 上下文。</param>
        void DrawExtra(IEntryBinding entry, EditableGuiContext context);
    }
}
