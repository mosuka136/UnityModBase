using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor.ValueEditor;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 进程级共享的值编辑器注册表：按注册顺序选择首个支持配置项的编辑器，并统一释放所注册的编辑器。
    /// 注册顺序具有语义：专用编辑器必须先于可匹配同一类型的通用编辑器注册。
    /// </summary>
    /// <remarks>
    /// 全部编辑器保存在静态列表中且未加锁，注册、查询与释放须由调用方串行执行（生产环境对应 IMGUI 主线程）。
    /// <see cref="GroupEditor"/> 每次构造都会追加一套内置编辑器，列表不去重；
    /// 查询取首个匹配者，因此重复注册不影响匹配结果。
    /// </remarks>
    public static class ValueEditorRegistry
    {
        // 进程级共享的匹配序列，按注册顺序遍历；无内部同步，依赖调用方串行访问。
        private static readonly List<IValueEditor> _editors = new List<IValueEditor>();

        /// <summary>
        /// 将非空编辑器追加到进程级匹配序列末尾；不去重、不影响既有注册，也不接管编辑器注册前的其他所有权。
        /// </summary>
        /// <param name="editor">要追加的值编辑器。</param>
        /// <exception cref="ArgumentNullException"><paramref name="editor"/> 为 <c>null</c>。</exception>
        public static void RegisterEditor(IValueEditor editor)
        {
            _editors.Add(editor ?? throw new ArgumentNullException(nameof(editor)));
        }

        /// <summary>
        /// 返回首个可编辑目标配置项的编辑器；没有匹配项时返回共享的只读占位编辑器。
        /// 目标配置项为 <c>null</c> 时直接返回占位编辑器，不调用已注册匹配器；
        /// 非空目标的匹配器异常会直接传播。
        /// 注册表为空（如 <see cref="Dispose"/> 之后尚未重新注册）时，所有配置项均返回占位编辑器。
        /// </summary>
        /// <param name="entry">要匹配的配置项绑定，可为 <c>null</c>。</param>
        /// <returns>首个匹配编辑器，或共享的 <see cref="UnsupportedEditor.Default"/>。</returns>
        public static IValueEditor GetEditor(IEntryBinding entry)
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
        /// 释放全部已注册编辑器并清空注册表。单个编辑器释放失败会被隔离并记录日志，不阻断其余编辑器清理。
        /// </summary>
        /// <remarks>
        /// 注册表是进程级共享状态：清空后所有使用方（含仍存活的 <see cref="GroupEditor"/>）的查询
        /// 都会回退到占位编辑器，直到重新注册；已释放的编辑器实例不会被复用。
        /// </remarks>
        public static void Dispose()
        {
            foreach (var editor in _editors)
            {
                try
                {
                    editor.Dispose();
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to dispose value editor. EditorType='{editor.GetType().FullName}'; remaining editors will continue.", ex);
                }
            }
            _editors.Clear();
        }
    }
}
