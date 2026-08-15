using System;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
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
        public virtual void DrawValue(IEntryBinding entry, GuiContext context)
        {
            if (!entry.ValueType.IsPrimitive || entry.ValueType == typeof(bool) || entry.ValueType == typeof(char))
                return;

            var valueString = ValueProvider.GetValue(entry).ToString();
            string newValueString = UnityGui.TextField(valueString, UnityGui.ExpandWidth(true));
            if (valueString != newValueString)
                context.ChangeSink.SetConvertedValue(entry, newValueString, DelayApplyDuration);
        }

        /// <inheritdoc/>
        public void DrawExtra(IEntryBinding entry, GuiContext context)
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
