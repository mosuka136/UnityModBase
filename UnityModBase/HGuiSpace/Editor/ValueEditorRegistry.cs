using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// 按注册顺序选择首个支持目标条目的值编辑器，并拥有所注册编辑器的生命周期。
    /// </summary>
    public sealed class ValueEditorRegistry : IDisposable
    {
        private readonly List<IValueEditor> _editors = new List<IValueEditor>();

        /// <summary>把值编辑器追加到解析顺序末尾。</summary>
        /// <param name="editor">要注册并由本注册表释放的编辑器。</param>
        public void RegisterEditor(IValueEditor editor)
        {
            _editors.Add(editor ?? throw new ArgumentNullException(nameof(editor)));
        }

        /// <summary>获取首个支持指定条目的值编辑器。</summary>
        /// <param name="entry">目标条目；为 null 时返回不支持类型编辑器。</param>
        /// <returns>匹配的值编辑器。</returns>
        public IValueEditor GetEditor(IEntryBinding entry)
        {
            if (entry == null)
                return UnsupportedEditor.Default;

            foreach (var editor in _editors)
            {
                if (editor.CanEdit(entry))
                    return editor;
            }
            return UnsupportedEditor.Default;
        }

        /// <summary>
        /// 创建标准运行时值编辑器集合。附加编辑器位于通用简单类型之后、集合、元组与双元素编辑器之前。
        /// </summary>
        /// <param name="unityService">供滑条编辑器做数值夹取、吸附和近似比较的 Unity 服务。</param>
        /// <param name="unityGui">绘制各值控件使用的 IMGUI 提供器。</param>
        /// <param name="styleProvider">滑条等控件使用的样式资源。</param>
        /// <param name="additionalEditors">追加到解析顺序中的可选编辑器；数组本身可为 <c>null</c>，元素为 <c>null</c> 时抛出异常。</param>
        /// <returns>已注册标准编辑器的新注册表；集合、元组与双元素编辑器最后注册并复用本注册表解析各自的子编辑器。</returns>
        public static ValueEditorRegistry CreateDefault(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            IEntryStyleResource styleProvider,
            params IValueEditor[] additionalEditors)
        {
            if (unityService == null)
                throw new ArgumentNullException(nameof(unityService));
            if (unityGui == null)
                throw new ArgumentNullException(nameof(unityGui));
            if (styleProvider == null)
                throw new ArgumentNullException(nameof(styleProvider));

            var registry = new ValueEditorRegistry();
            registry.RegisterEditor(new BooleanEditor(unityGui));
            registry.RegisterEditor(new StringEditor(unityGui));
            registry.RegisterEditor(new SliderEditor(unityGui, unityService, styleProvider));
            registry.RegisterEditor(new NumberEditor(unityGui));
            registry.RegisterEditor(new EnumEditor(unityGui));

            if (additionalEditors != null)
            {
                foreach (var editor in additionalEditors)
                    registry.RegisterEditor(editor);
            }

            registry.RegisterEditor(new CollectionEditor(unityGui, registry.GetEditor));
            registry.RegisterEditor(new TupleEditor(unityGui, registry.GetEditor));
            registry.RegisterEditor(new DualValueEditor(unityGui, registry.GetEditor));
            return registry;
        }

        /// <summary>逐个释放并清空已注册的值编辑器。</summary>
        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                try
                {
                    editor.Dispose();
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to dispose value editor. EditorType='{editor.GetType().FullName}'.", ex);
                }
            }
            _editors.Clear();
        }
    }
}
