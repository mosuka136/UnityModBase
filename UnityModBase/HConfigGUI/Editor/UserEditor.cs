using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 将通用用户选择器与配置分组编辑器组合为配置 GUI 的用户级内容编辑器。
    /// 延迟配置提交通过 <see cref="Update"/> 推进，布局失效状态保存在各用户自己的 <see cref="GuiContext"/> 中。
    /// </summary>
    public class UserEditor : UserEditorBase
    {
        /// <summary>
        /// 获取当前用户配置内容使用的分组编辑器。
        /// </summary>
        public GroupEditor GroupEditor { get; }

        /// <summary>
        /// 创建配置用户编辑器及其分组编辑器。
        /// </summary>
        /// <param name="unityService">用于布局和延迟更新的 Unity 服务。</param>
        /// <param name="unityGui">用于绘制用户与配置内容的 IMGUI 提供器。</param>
        /// <param name="styleProvider">提供配置界面样式的资源。</param>
        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider) : base(unityService, unityGui)
        {
            GroupEditor = new GroupEditor(unityService, unityGui, styleProvider);
        }

        /// <summary>
        /// 绘制用户选择区域和当前用户的配置分组。
        /// </summary>
        /// <param name="users">可选择用户序列。</param>
        /// <param name="selectedKey">当前用户键；选择变化时被更新。</param>
        /// <param name="guiContext">当前用户的配置 GUI 上下文。</param>
        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            base.Draw(users, ref selectedKey, guiContext);
            var context = guiContext as GuiContext;
            GroupEditor.Draw(context);
        }

        /// <summary>
        /// 使用非缩放时间增量推进指定用户上下文中的延迟配置提交。
        /// </summary>
        /// <param name="context">要更新的配置 GUI 上下文。</param>
        /// <param name="unscaledDeltaTime">非缩放帧间隔，单位为秒。</param>
        public void Update(GuiContext context, float unscaledDeltaTime)
        {
            GroupEditor.Update(context, unscaledDeltaTime);
        }

        /// <summary>
        /// 将配置上下文中的标签、分组按钮和重置按钮尺寸全部标记为待重算。
        /// </summary>
        /// <param name="context">要标记为布局失效的配置 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="context"/> 不是 <see cref="GuiContext"/>。</exception>
        public override void SetStatusDirty(IUserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            var guiContext = context as GuiContext ?? throw new ArgumentException($"The provided context is not of type {nameof(GuiContext)}.", nameof(context));
            if (!guiContext.IsValid)
            {
                BLog.Error($"Invalid GuiContext provided to {nameof(SetStatusDirty)}.");
                return;
            }

            guiContext.SetLayoutDirtyFlags();
        }

        /// <summary>
        /// 释放分组编辑器及其中可能仍在录制的热键编辑会话。
        /// </summary>
        public override void Dispose()
        {
            GroupEditor?.Dispose();
        }
    }
}
