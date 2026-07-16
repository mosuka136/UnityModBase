using System;
using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor.ValueEditor;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 按注册顺序选择首个支持配置项的值编辑器，并统一释放所注册编辑器。
    /// 注册顺序具有语义：专用编辑器必须先于可匹配同一类型的通用编辑器注册。
    /// </summary>
    public class ValueEditorRegistry : IDisposable
    {
        private readonly List<IValueEditor> _editors = new List<IValueEditor>();

        /// <summary>
        /// 将编辑器追加到匹配序列；不会去重或校验 null，也不会接管注册前的其他所有权。
        /// </summary>
        /// <param name="editor">要追加的值编辑器。</param>
        public void RegisterEditor(IValueEditor editor)
        {
            _editors.Add(editor);
        }

        /// <summary>
        /// 返回首个可编辑目标配置项的编辑器；没有匹配项时返回共享的只读占位编辑器。
        /// 匹配器抛出的异常会直接传播，调用方应传入有效绑定并只注册有效编辑器。
        /// </summary>
        /// <param name="entry">要匹配的配置项绑定。</param>
        /// <returns>首个匹配编辑器，或共享的 <see cref="UnsupportedEditor.Default"/>。</returns>
        public IValueEditor GetEditor(IEntryBinding entry)
        {
            foreach (var editor in _editors)
            {
                if (editor.CanEdit(entry))
                    return editor;
            }
            return UnsupportedEditor.Default;
        }

        /// <summary>
        /// 尝试释放全部已注册编辑器。单个编辑器释放失败会被隔离，不阻断其余编辑器清理。
        /// </summary>
        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                try { editor.Dispose(); }
                catch { }
            }
        }
    }
}
