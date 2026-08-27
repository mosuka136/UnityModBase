using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 组合通用用户选择器与可编辑分组内容，并统一推进延迟提交和布局失效。
    /// </summary>
    public class EditableUserEditorBase : UserEditorBase
    {
        /// <summary>获取负责分组内容绘制和值提交的编辑器。</summary>
        public GroupEditor GroupEditor { get; }

        /// <summary>创建通用用户级编辑器。</summary>
        /// <param name="unityService">Unity 运行时提供器。</param>
        /// <param name="unityGui">Unity IMGUI 提供器。</param>
        /// <param name="groupEditor">分组编辑器。</param>
        public EditableUserEditorBase(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            GroupEditor groupEditor)
            : base(unityService, unityGui)
        {
            GroupEditor = groupEditor ?? throw new ArgumentNullException(nameof(groupEditor));
        }

        /// <inheritdoc/>
        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            base.Draw(users, ref selectedKey, guiContext);
            if (guiContext is EditableGuiContext context && context.IsValid)
                GroupEditor.Draw(context);
        }

        /// <summary>推进延迟值提交。</summary>
        /// <param name="context">当前可编辑 GUI 上下文。</param>
        /// <param name="unscaledDeltaTime">未缩放的帧间隔。</param>
        public void Update(EditableGuiContext context, float unscaledDeltaTime)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            GroupEditor.Update(context, unscaledDeltaTime);
        }

        /// <inheritdoc/>
        public override void SetStatusDirty(IUserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var editableContext = context as EditableGuiContext ??
                throw new ArgumentException($"The provided context is not of type {nameof(EditableGuiContext)}.", nameof(context));

            if (!editableContext.IsValid)
            {
                BLog.Error($"Editor status was not invalidated because the GUI context is invalid. Operation='{nameof(SetStatusDirty)}'.");
                return;
            }

            editableContext.SetLayoutDirtyFlags();
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            GroupEditor.Dispose();
        }
    }
}
