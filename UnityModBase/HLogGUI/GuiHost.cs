using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HLogGUI
{
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase<EntryListBinding>
    {
        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new StyleResource(UnityGui);
                StyleProvider = styleProvider;
                base.Awake();

                User = new UserBinding();
                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider, ToastEditor);
                Translator.OnDefaultLanguageChanged += (s, e) => userEditor.ListEditor.IsColumnWidthDirty = true;
                UserEditor = userEditor;

                UIHotkey = BConfigManager.LogUIHotkey.Value;
                BConfigManager.LogUIHotkey.OnValueChanged += (s, e) => UIHotkey = e;

                Title = TranslatorResource.Title;
                float width = UnityGui.ScreenWidth * 0.8f;
                float height = UnityGui.ScreenHeight * 0.5f;
                WindowRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);

                BLog.Debug($"Log GUI host created. WindowId={WindowID}");
            }
            catch (Exception ex)
            {
                BLog.Error("Failed to create Log GUI host.", ex);
                Destroy(this);
            }
        }

        public void OnDestroy()
        {
        }

        public override void OnGUI()
        {
            var userEditor = UserEditor as UserEditor;
            var rect = WindowRect;
            rect.width = UnityService.Clamp(userEditor.ListEditor.TotalColumnWidth, UnityGui.ScreenWidth * 0.5f, UnityGui.ScreenWidth * 0.9f);
            WindowRect = rect;

            base.OnGUI();
        }
    }
}
