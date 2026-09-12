using System;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 使用文本框编辑除 bool 和 char 外的基础数值类型。
    /// 未完成或越界的输入会保留在编辑缓冲区供文本框回显，但延迟到期时不会写入底层配置。
    /// </summary>
    public class NumberEditor : IValueEditor
    {
        /// <summary>
        /// 获取或设置停止输入后的提交延迟，单位为秒；默认 0.5 秒，小于等于 0 时立即提交。
        /// </summary>
        public float DelayApplyDuration { get; set; } = 0.5f;

        /// <summary>
        /// 获取数值文本框使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建通用数值编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制文本框的 IMGUI 提供器。</param>
        public NumberEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
        }

        /// <inheritdoc/>
        public virtual bool CanEdit(IEntryBinding entry)
        {
            if (entry == null)
                return false;

            var type = entry.ValueType;
            if (type.IsPrimitive && type != typeof(bool) && type != typeof(char) && entry.Metadata == null)
                return true;
            else
                return false;
        }

        /// <inheritdoc/>
        public virtual void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            if (!entry.ValueType.IsPrimitive || entry.ValueType == typeof(bool) || entry.ValueType == typeof(char))
                return;

            var valueString = ValueProvider.GetValue(entry).ToString();
            // 元组元素按元组编辑器测量的本列宽度绘制，使同一列表内同列的各行元素等宽对齐。
            string newValueString = UnityGui.TextField(
                valueString,
                entry is TupleElementBinding tupleElement && tupleElement.TryGetSuggestedWidth(out var suggestedWidth)
                    ? UnityGui.Width(suggestedWidth)
                    : UnityGui.ExpandWidth(true));
            SetSlotTooltip(entry);
            if (valueString != newValueString)
                context.ChangeSink.SetConvertedValue(entry, newValueString, DelayApplyDuration);
        }

        /// <summary>
        /// 若绑定为双元素槽位，把其分元素说明设置为刚绘制控件的悬停提示；其他绑定不处理，
        /// 其提示由条目行的名称标签承担。必须在目标控件绘制后立即调用，供数值与滑条编辑器复用。
        /// </summary>
        protected void SetSlotTooltip(IEntryBinding entry)
        {
            if (entry is DualValueSlotBinding)
                UnityGui.SetLastControlTooltip(entry.Description);
        }

        /// <inheritdoc/>
        public void DrawExtra(IEntryBinding entry, EditableGuiContext context)
        {
        }

        /// <summary>
        /// 释放编辑器；此实现不持有需释放状态。
        /// </summary>
        public void Dispose()
        {
        }
    }
}
