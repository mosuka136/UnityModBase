using System.Collections.Generic;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    public class UserEditor : UserEditorBase
    {
        public StyleResource StyleProvider { get; }
        public ListEditor ListEditor { get; }
        public UserBinding UserBinding { get; }

        public UserEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            StyleResource styleProvider,
            ToastEditor toastEditor) : base(unityService, unityGui)
        {
            StyleProvider = styleProvider;
            UserBinding = new UserBinding(UserManager.UserContexts);
            ListEditor = new ListEditor(unityGui, unityService, styleProvider, toastEditor);
        }

        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey)
        {
            base.Draw(users, ref selectedKey);
            ListEditor.Render(UserBinding.GetData(selectedKey));
        }
    }
}
