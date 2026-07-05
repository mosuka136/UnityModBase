using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 配置界面的 Unity 宿主组件。
    /// 该组件在游戏启动后由 <see cref="GameBootRegistery"/> 创建，负责接收热键并在 OnGUI 中绘制窗口。
    /// </summary>
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase<GroupBinding>
    {
        public PopupEditor PopupEditor { get; private set; }

        public GuiContext GuiContext { get; private set; }
        public LayoutResource LayoutProvider { get; private set; }

        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new StyleResource(UnityGui);
                StyleProvider = styleProvider;
                base.Awake();

                Translator.DefaultLanguage = BConfigManager.SetLanguage.Value;

                GuiContext = new GuiContext();
                LayoutProvider = new LayoutResource(UnityGui);

                User = new UserBinding(UserManager.UserContexts);
                foreach (var context in UserManager.UserContexts)
                    context.AddContext(nameof(HConfigGUI), new GuiContext());
                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider, LayoutProvider, GuiContext);
                Translator.OnDefaultLanguageChanged += (s, e) => userEditor.UpdateLayout();
                UserEditor = userEditor;

                PopupEditor = new PopupEditor(UnityGui, styleProvider, GuiContext);
                GuiContext.SetBool(GuiContext.IsPopupOpenKey, false);

                GuiPipe.OnEntryValueChanged += e => ToastEditor.SetToast(TranslatorResource.Changed + e.Name);
                GuiPipe.OnEntryValueReset += e => ToastEditor.SetToast(TranslatorResource.ResetDone + e.Name);

                UIHotkey = BConfigManager.ConfigUIHotkey.Value;
                BConfigManager.ConfigUIHotkey.OnValueChanged += (s, e) => UIHotkey = e;

                Title = TranslatorResource.Title;
                float width = UnityGui.ScreenWidth * 0.35f;
                float height = UnityGui.ScreenHeight * 0.7f;
                WindowRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);

                BLog.Debug($"Config GUI host created. WindowId={WindowID}");
            }
            catch (Exception ex)
            {
                BLog.Error("Failed to create Config GUI host.", ex);
                Destroy(this);
            }
        }

        public override void Update()
        {
            base.Update();
            (UserEditor as UserEditor)?.Update(UnityService.UnscaledDeltaTime);
        }

        public override void OnGUI()
        {
            if (!IsVisible)
                return;

            if (GuiContext.GetBool(GuiContext.IsPopupOpenKey))
            {
                PopupEditor.DrawPopup(GuiPipe.PopupTitle, GuiPipe.PopupWindowAction, GuiPipe.ClosePopupWindowAction);
                return;
            }

            base.OnGUI();
        }
    }
}
