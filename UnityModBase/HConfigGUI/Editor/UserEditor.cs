using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class UserEditor : UserEditorBase
    {
        public StyleResource StyleProvider { get; }
        public SheetEditor SheetEditor { get; }

        public UserEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            GuiStateStore guiStateStore,
            StyleResource styleProvider) : base(unityService, unityGui)
        {
            StyleProvider = styleProvider;
            SheetEditor = new SheetEditor(unityService, unityGui, guiStateStore, styleProvider);
        }

        public override void Draw<T>(UserBindingBase<T> user)
        {
            base.Draw(user);
            SheetEditor.DrawSheet(user.GetData(SelectedKey) as SheetBinding);
        }

        public void Update(float unscaledDeltaTime)
        {
            SheetEditor.Update(unscaledDeltaTime);
        }

        public void UpdateLayout()
        {
            SheetEditor.UpdateLayout();
        }
    }
}
