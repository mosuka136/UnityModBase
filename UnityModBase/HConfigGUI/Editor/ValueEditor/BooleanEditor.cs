using System;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    /// <summary>
    /// 使用本地化开关控件编辑布尔配置，并在切换发生时立即提交。
    /// </summary>
    public class BooleanEditor : IValueEditor
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
        public void DrawValue(IEntryBinding entry, GuiContext context)
        {
            var value = ValueProvider.GetValidValue<bool>(entry);
            bool newValue = UnityGui.Toggle(value, value ? TranslatorResource.On : TranslatorResource.Off, UnityGui.ExpandWidth(true));
            if (newValue != value)
                context.ChangeSink.SetValue(entry, newValue);
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
