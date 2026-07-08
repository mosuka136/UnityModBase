using System;
using System.Collections.Generic;
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

        public void SetLayoutDirty(GuiContext context)
        {
            GroupEditor.SetLayoutDirty(context);
        }
    }
}
