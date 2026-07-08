using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI.Editor
{
    public class UserEditor : UserEditorBase
    {
        public GroupEditor GroupEditor { get; }

        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider) : base(unityService, unityGui)
        {
            GroupEditor = new GroupEditor(unityService, unityGui, styleProvider);
        }

        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            base.Draw(users, ref selectedKey, guiContext);
            var context = guiContext as GuiContext;
            GroupEditor.Draw(context);
        }

        public void Update(GuiContext context, float unscaledDeltaTime)
        {
            GroupEditor.Update(context, unscaledDeltaTime);
        }

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
    }
}
