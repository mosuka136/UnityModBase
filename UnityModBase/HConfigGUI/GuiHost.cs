using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HotkeyManager;
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
        private ConfigEntry<Hotkey> _uiHotkeyEntry;

        public PopupEditor PopupEditor { get; private set; }

        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new StyleResource(UnityGui);
                StyleProvider = styleProvider;
                base.Awake();

                Translator.DefaultLanguage = BConfigManager.SetLanguage.Value;

                GuiContextKey = nameof(HConfigGUI);
                Users = UserManager.UserContexts;
                foreach (var context in Users)
                    RegisterContext(context);
                UserManager.OnConfigRegistered += RegisterContext;
                CurrentContext = GetContext(_selectedUserKey);

                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider);
                UserEditor = userEditor;
                Translator.OnDefaultLanguageChanged += OnDefaultLanguageChanged;

                PopupEditor = new PopupEditor(UnityGui, styleProvider);

                _uiHotkeyEntry = BConfigManager.ConfigUIHotkey;
                UIHotkey = _uiHotkeyEntry.Value;
                _uiHotkeyEntry.OnValueChanged += OnConfigUIHotkeyChanged;

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

        public void RegisterContext(UserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "UserContext cannot be null.");

            var guiContext = new GuiContext() { UserData = GroupBinding.CreateRoot(context) };
            guiContext.SubscribeToastNotifications(ToastEditor);

            context.AddChildContext(GuiContextKey, guiContext);
        }

        public override void Update()
        {
            base.Update();
            (UserEditor as UserEditor)?.Update(CurrentContext as GuiContext, UnityService.UnscaledDeltaTime);
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

        private void OnDestroy()
        {
            UserManager.OnConfigRegistered -= RegisterContext;
            Translator.OnDefaultLanguageChanged -= OnDefaultLanguageChanged;

            if (_uiHotkeyEntry != null)
            {
                _uiHotkeyEntry.OnValueChanged -= OnConfigUIHotkeyChanged;
                _uiHotkeyEntry = null;
            }

            UserEditor?.Dispose();

            foreach (var user in Users ?? Array.Empty<UserContext>())
            {
                var context = user.GetChildContext(GuiContextKey) as GuiContext;
                context?.Dispose();
                user.RemoveChildContext(GuiContextKey);
            }
        }

        private void OnDefaultLanguageChanged(object sender, LanguageType language)
        {
            var context = CurrentContext as GuiContext;
            if (context != null)
                UserEditor?.SetStatusDirty(context);
        }

        private void OnConfigUIHotkeyChanged(object sender, Hotkey hotkey)
        {
            UIHotkey = hotkey;
        }
    }
}
