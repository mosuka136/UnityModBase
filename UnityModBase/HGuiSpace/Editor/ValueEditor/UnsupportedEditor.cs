using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 当没有注册编辑器支持配置类型时显示类型名称的只读占位实现。
    /// <see cref="Default"/> 可由多个注册表共享；该实现不修改配置，也不持有需释放状态。
    /// </summary>
    public sealed class UnsupportedEditor : IValueEditor
    {
        /// <summary>
        /// 获取显示不支持类型提示所用的全局 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 获取注册表未匹配到编辑器时使用的共享实例。
        /// </summary>
        public static UnsupportedEditor Default { get; } = new UnsupportedEditor();

        /// <summary>
        /// 使用当前全局 IMGUI 提供器创建占位编辑器。
        /// </summary>
        public UnsupportedEditor()
        {
            UnityGui = UnityGuiProvider.Instance;
        }

        /// <inheritdoc/>
        public bool CanEdit(IEntryBinding entry)
        {
            return false;
        }

        /// <inheritdoc/>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            UnityGui.Label("Unsupported type: " + entry.ValueType.FullName);
        }

        /// <inheritdoc/>
        public void DrawExtra(IEntryBinding entry, EditableGuiContext context)
        {
        }

        /// <summary>
        /// 释放编辑器；共享实现不持有需释放状态。
        /// </summary>
        public void Dispose()
        {
        }
    }
}
