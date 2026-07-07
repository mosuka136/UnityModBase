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
    public class GuiHost : GuiHostBase
    {
        public PopupEditor PopupEditor { get; private set; }
        public LayoutResource LayoutProvider { get; private set; }

        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new StyleResource(UnityGui);
                StyleProvider = styleProvider;
                LayoutProvider = new LayoutResource(UnityGui);
                base.Awake();

                Translator.DefaultLanguage = BConfigManager.SetLanguage.Value;

                GuiContextKey = nameof(HConfigGUI);
                Users = UserManager.UserContexts;
                foreach (var context in Users)
                    RegisterContext(context);
                UserManager.OnUserRegistered += RegisterContext;
                CurrentContext = GetContext(_selectedUserKey);

                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider, LayoutProvider);
                Translator.OnDefaultLanguageChanged += (s, e) => userEditor.UpdateLayout();
                UserEditor = userEditor;

                PopupEditor = new PopupEditor(UnityGui, styleProvider);

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

        private void RegisterContext(UserContext context)
        {
            var guiContext = new GuiContext() { UserData = GroupBinding.CreateRoot(context) };
            guiContext.ChangeSink.OnEntryValueChanged += e => ToastEditor.SetToast(TranslatorResource.Changed + e.Name);
            guiContext.ChangeSink.OnEntryValueReset += e => ToastEditor.SetToast(TranslatorResource.ResetDone + e.Name);

            context.AddContext(GuiContextKey, guiContext);
        }

        public override void Update()
        {
            base.Update();
            (UserEditor as UserEditor)?.Update(CurrentContext as GuiContext, UnityService.UnscaledDeltaTime);
        }

        public override void DrawWindow(int id)
        {
            base.DrawWindow(id);
        }

        public override void OnGUI()
        {
            if (!IsVisible)
                return;

            var context = CurrentContext as GuiContext;
            if (context.Popup.IsOpen == true)
            {
                PopupEditor.DrawPopup(context);
                return;
            }

            base.OnGUI();
        }

        public void OnDestroy()
        {
            UserManager.OnUserRegistered -= RegisterContext;
        }
    }
}
