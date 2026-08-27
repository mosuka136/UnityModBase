using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 配置界面的用户级编辑器。
    /// </summary>
    public class UserEditor : EditableUserEditorBase
    {
        /// <summary>获取配置专用分组编辑器。</summary>
        public new GroupEditor GroupEditor => (GroupEditor)base.GroupEditor;

        /// <summary>创建配置界面的用户级编辑器。</summary>
        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider)
            : base(unityService, unityGui, new GroupEditor(unityService, unityGui, styleProvider))
        {
        }
    }
}
