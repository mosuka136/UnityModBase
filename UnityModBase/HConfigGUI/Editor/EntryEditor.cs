using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HProvider;
using SharedEntryEditor = UnityModBase.HGuiSpace.Editor.EntryEditor;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 在通用条目行尾部绘制配置恢复默认值操作。
    /// </summary>
    public class EntryEditor : SharedEntryEditor
    {
        /// <inheritdoc/>
        public override float TrailingActionWidth =>
            UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.Reset)).x;

        /// <summary>创建带恢复默认值操作的配置条目编辑器。</summary>
        public EntryEditor(IUnityGuiProvider unityGui, ValueEditorRegistry registry)
            : base(unityGui, registry)
        {
        }

        /// <inheritdoc/>
        protected override void DrawTrailingAction(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry is IResettableEntryBinding resettable &&
                UnityGui.Button(TranslatorResource.Reset, UnityGui.ExpandWidth(false)))
            {
                context.ChangeSink.ResetValue(resettable);
            }
        }
    }
}
