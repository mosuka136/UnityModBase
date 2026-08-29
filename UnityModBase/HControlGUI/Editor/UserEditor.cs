using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HControlGUI.Editor
{
    /// <summary>
    /// 实时控制界面的用户级编辑器。
    /// </summary>
    public sealed class UserEditor : EditableUserEditorBase
    {
        /// <summary>创建实时控制界面的用户级编辑器及其独立值编辑器注册表。</summary>
        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, EntryStyleResource styleProvider)
            : this(
                unityService,
                unityGui,
                styleProvider,
                ValueEditorRegistry.CreateDefault(unityService, unityGui, styleProvider))
        {
        }

        private UserEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            EntryStyleResource styleProvider,
            ValueEditorRegistry valueEditors)
            : base(
                unityService,
                unityGui,
                new GroupEditor(unityService, unityGui, styleProvider, valueEditors))
        {
        }
    }
}
