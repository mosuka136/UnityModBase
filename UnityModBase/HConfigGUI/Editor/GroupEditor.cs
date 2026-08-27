using System;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HProvider;
using SharedGroupEditor = UnityModBase.HGuiSpace.Editor.GroupEditor;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 为通用分组编辑器追加配置热键编辑器和重置操作。
    /// </summary>
    public class GroupEditor : SharedGroupEditor
    {
        private static readonly DualValueDescriptor ConfigDualValueDescriptor = new DualValueDescriptor(
            typeof(ConfigEntryValue<,>),
            nameof(ConfigEntryValue<int, int>.Value1),
            nameof(ConfigEntryValue<int, int>.Value2));

        /// <summary>获取配置界面专属的热键编辑器。</summary>
        public HotkeyEditor HotkeyEditor { get; }

        /// <summary>创建配置分组编辑器及其独立值编辑器注册表。</summary>
        public GroupEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider)
            : this(unityService, unityGui, styleProvider, new HotkeyEditor(unityGui, styleProvider))
        {
        }

        private GroupEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            StyleResource styleProvider,
            HotkeyEditor hotkeyEditor)
            : this(
                unityService,
                unityGui,
                styleProvider,
                hotkeyEditor,
                ValueEditorRegistry.CreateDefault(
                    unityService,
                    unityGui,
                    styleProvider,
                    ConfigDualValueDescriptor,
                    hotkeyEditor))
        {
        }

        private GroupEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            StyleResource styleProvider,
            HotkeyEditor hotkeyEditor,
            ValueEditorRegistry valueEditors)
            : base(
                unityService,
                unityGui,
                styleProvider,
                valueEditors,
                new EntryEditor(unityGui, valueEditors),
                hotkeyEditor.Session.CancelEdit)
        {
            HotkeyEditor = hotkeyEditor ?? throw new ArgumentNullException(nameof(hotkeyEditor));
        }
    }
}
