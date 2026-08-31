using System;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 使用本地化开关控件编辑布尔配置，并在切换发生时立即提交。
    /// </summary>
    public sealed class BooleanEditor : IValueEditor
    {
        /// <summary>
        /// 获取开关控件使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建布尔值编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制开关控件的 IMGUI 提供器。</param>
        public BooleanEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
        }

        /// <inheritdoc/>
        public bool CanEdit(IEntryBinding entry)
        {
            if (entry != null && entry.ValueType == typeof(bool) && entry.Metadata == null)
                return true;
            else
                return false;
        }

        /// <inheritdoc/>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            var value = ValueProvider.GetValidValue<bool>(entry);

            // 双元素槽位在父编辑器的复合横向区域内绘制，剩余宽度由该区域统一分配；
            // 开关若再展开占满宽度会挤压另一槽位，因此槽位绑定只按文本宽度渲染。
            // 槽位没有独立的名称标签，分元素说明改经 GUIContent 作为开关的悬停提示。
            var isDualValueSlot = entry is DualValueSlotBinding;

            bool newValue;
            if (isDualValueSlot)
                newValue = UnityGui.Toggle(
                    value,
                    UnityGui.GetContent(value ? TranslatorResource.On : TranslatorResource.Off, entry.Description),
                    UnityGui.ExpandWidth(false));
            else
                newValue = UnityGui.Toggle(
                    value,
                    value ? TranslatorResource.On : TranslatorResource.Off,
                    UnityGui.ExpandWidth(true));

            if (newValue != value)
                context.ChangeSink.SetValue(entry, newValue);
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
