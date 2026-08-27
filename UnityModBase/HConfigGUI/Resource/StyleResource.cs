using UnityEngine;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Resource
{
    /// <summary>
    /// 在通用条目样式上补充配置弹窗和热键录制样式。
    /// </summary>
    public class StyleResource : EntryStyleResource
    {
        private GUIStyle _popupTitleStyle;
        private GUIStyle _recordingHotkeyLabelStyle;

        /// <summary>获取模态弹窗标题样式。</summary>
        public GUIStyle PopupTitleStyle => _popupTitleStyle ?? (_popupTitleStyle = CreatePopupTitleStyle());

        /// <summary>获取热键录制状态标签样式。</summary>
        public GUIStyle RecordingHotkeyLabelStyle => _recordingHotkeyLabelStyle ?? (_recordingHotkeyLabelStyle = CreateRecordingHotkeyLabelStyle());

        /// <summary>创建配置界面样式资源。</summary>
        public StyleResource(IUnityGuiProvider unityGui) : base(unityGui)
        {
        }

        private GUIStyle CreatePopupTitleStyle()
        {
            return new GUIStyle(UnityGui.LabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
        }

        private GUIStyle CreateRecordingHotkeyLabelStyle()
        {
            return new GUIStyle(UnityGui.LabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.BoldAndItalic,
                fontSize = 22
            };
        }
    }
}
