using System.Collections.Generic;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 将通用用户选择器与日志表格编辑器组合为日志 GUI 的用户级内容编辑器。
    /// 用户切换或日志变化时只标记列宽缓存失效，实际测量在后续绘制中完成。
    /// </summary>
    public class UserEditor : UserEditorBase
    {
        /// <summary>
        /// 获取当前用户日志内容使用的表格编辑器。
        /// </summary>
        public GroupEditor GroupEditor { get; }

        /// <summary>
        /// 创建日志用户编辑器及其表格编辑器。
        /// </summary>
        /// <param name="unityService">提供剪贴板和文本测量运算的 Unity 服务。</param>
        /// <param name="unityGui">用于绘制用户和日志表格的 IMGUI 提供器。</param>
        /// <param name="styleProvider">提供日志表格样式的资源。</param>
        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider) : base(unityService, unityGui)
        {
            GroupEditor = new GroupEditor(unityGui, unityService, styleProvider);
        }

        /// <summary>
        /// 绘制用户选择区域及当前用户的日志表格。
        /// 上下文类型不匹配时只绘制用户选择区域，不向日志表格编辑器传入空上下文。
        /// </summary>
        /// <param name="users">可选择用户序列。</param>
        /// <param name="selectedKey">当前用户键；选择变化时被更新。</param>
        /// <param name="guiContext">当前用户的日志 GUI 上下文。</param>
        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            base.Draw(users, ref selectedKey, guiContext);
            if (guiContext is GuiContext context)
                GroupEditor.Draw(context);
        }

        /// <summary>
        /// 标记指定日志 GUI 上下文的列宽需要重新测量。
        /// </summary>
        /// <param name="context">要标记为列宽失效的日志 GUI 上下文。</param>
        public override void SetStatusDirty(IUserContext context)
        {
            (context as GuiContext).IsColumnWidthDirty = true;
        }
    }
}
