using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HLogGUI
{
    public class UserEditor : UserEditorBase
    {
        public StyleResource StyleProvider { get; }
        public ListEditor ListEditor { get; }

        public UserEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            StyleResource styleProvider,
            ToastEditor toastEditor) : base(unityService, unityGui)
        {
            StyleProvider = styleProvider;
            ListEditor = new ListEditor(unityGui, unityService, styleProvider, toastEditor);
        }

        public override void Draw<T>(UserBindingBase<T> user)
        {
            base.Draw(user);
            ListEditor.Render(user.GetData(SelectedKey) as EntryListBinding);
        }
    }
}
