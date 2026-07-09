using System.Collections.Generic;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    public class UserEditor : UserEditorBase
    {
        public GroupEditor GroupEditor { get; }

        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider) : base(unityService, unityGui)
        {
            GroupEditor = new GroupEditor(unityGui, unityService, styleProvider);
        }

        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            base.Draw(users, ref selectedKey, guiContext);
            GroupEditor.Draw(guiContext as GuiContext);
        }

        public override void SetStatusDirty(IUserContext context)
        {
            (context as GuiContext).IsColumnWidthDirty = true;
        }
    }
}
