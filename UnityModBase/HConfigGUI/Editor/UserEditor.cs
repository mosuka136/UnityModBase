using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;

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
            LayoutResource layoutProvider,
            GuiContext guiContext) : base(unityService, unityGui)
        {
            StyleProvider = styleProvider;
            GroupEditor = new GroupEditor(
                unityService,
                unityGui,
                guiContext,
                styleProvider,
                layoutProvider);
        }

        public override void Draw<T>(UserBindingBase<T> user)
        {
            base.Draw(user);
            GroupEditor.Draw(user.GetData(SelectedKey) as GroupBinding);
        }

        public void Update(float unscaledDeltaTime)
        {
            GroupEditor.Update(unscaledDeltaTime);
        }

        public void UpdateLayout()
        {
            GroupEditor.UpdateLayout();
        }
    }
}
