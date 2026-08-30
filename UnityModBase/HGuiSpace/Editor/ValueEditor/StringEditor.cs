using System;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 使用文本框编辑字符串配置。输入先进入配置项缓冲区，并在无新输入达到指定时长后提交，避免每次击键都写配置。
    /// </summary>
    public sealed class StringEditor : IValueEditor
    {
        /// <summary>
        /// 获取或设置停止输入后的提交延迟，单位为秒；默认 0.5 秒，小于等于 0 时立即提交。
        /// </summary>
        public float DelayApplyDuration { get; set; } = 0.5f;

        /// <summary>
        /// 获取文本框使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建字符串编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制文本框的 IMGUI 提供器。</param>
        public StringEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
        }

        /// <inheritdoc/>
        public bool CanEdit(IEntryBinding entry)
        {
            if (entry != null && entry.ValueType == typeof(string) && entry.Metadata == null)
                return true;
            else
                return false;
        }

        /// <inheritdoc/>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            var value = ValueProvider.GetValidValue<string>(entry);
            var newValue = UnityGui.TextField(value, UnityGui.ExpandWidth(true));
            if (newValue != value)
                context.ChangeSink.SetValue(entry, newValue, delay: DelayApplyDuration);
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
