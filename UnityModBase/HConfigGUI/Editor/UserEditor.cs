using System.Collections.Generic;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI.Editor
{
    public class UserEditor : UserEditorBase
    {
        public StyleResource StyleProvider { get; }
        public GroupEditor GroupEditor { get; }

        public UserEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            StyleResource styleProvider,
            LayoutResource layoutProvider) : base(unityService, unityGui)
        {
            StyleProvider = styleProvider;
            GroupEditor = new GroupEditor(
                unityService,
                unityGui,
                styleProvider,
                layoutProvider);
        }

        public override void Draw(IEnumerable<UserContext> users, ref string selectedKey)
        {
            base.Draw(users, ref selectedKey);
            var context = GuiHost.GetContext(selectedKey) as GuiContext;
            GroupEditor.Draw(context?.UserData, context);
        }

        public void Update(GuiContext context, float unscaledDeltaTime)
        {
            GroupEditor.Update(context, unscaledDeltaTime);
        }

        public void UpdateLayout()
        {
            GroupEditor.UpdateLayout();
        }
    }
}
