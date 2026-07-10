using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase
    {
        private ConfigEntry<Hotkey> _uiHotkeyEntry;

        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new StyleResource(UnityGui);
                StyleProvider = styleProvider;
                base.Awake();

                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider);
                UserEditor = userEditor;

                GuiContextKey = nameof(HLogGUI);
                Users = UserManager.UserContexts;
                foreach (var user in Users)
                    RegisterContext(user);
                UserManager.OnUserRegistered += RegisterContext;
                CurrentContext = GetContext(_selectedUserKey);
                Translator.OnDefaultLanguageChanged += OnDefaultLanguageChanged;

                _uiHotkeyEntry = BConfigManager.LogUIHotkey;
                UIHotkey = _uiHotkeyEntry.Value;
                _uiHotkeyEntry.OnValueChanged += OnLogUIHotkeyChanged;

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

        public void RegisterContext(UserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "UserContext cannot be null.");

            var userEditor = UserEditor as UserEditor;
            var guiContext = new GuiContext();
            var userData = new GroupBinding();

            guiContext.UserData = userData;

            foreach (var log in context.Service.LogDatabase.Logs)
                userData.AddEntry(new EntryBinding(log));

            guiContext.RegisterLogHandlers(context.Service.LogDatabase, userEditor);
            guiContext.SubscribeToastNotifications(ToastEditor);

            context.AddContext(GuiContextKey, guiContext);
        }

        public void OnDestroy()
        {
            UserManager.OnUserRegistered -= RegisterContext;
            Translator.OnDefaultLanguageChanged -= OnDefaultLanguageChanged;

            if (_uiHotkeyEntry != null)
            {
                _uiHotkeyEntry.OnValueChanged -= OnLogUIHotkeyChanged;
                _uiHotkeyEntry = null;
            }

            foreach (var user in Users ?? Array.Empty<UserContext>())
            {
                var context = user.GetContext(GuiContextKey) as GuiContext;
                context?.Dispose();
            }
        }

        public override void OnGUI()
        {
            var context = CurrentContext as GuiContext;
            var rect = WindowRect;
            rect.width = UnityService.Clamp(context.TotalColumnWidth, UnityGui.ScreenWidth * 0.5f, UnityGui.ScreenWidth * 0.9f);
            WindowRect = rect;

            base.OnGUI();
        }

        private void OnDefaultLanguageChanged(object sender, LanguageType language)
        {
            UserEditor.SetStatusDirty(CurrentContext);
        }

        private void OnLogUIHotkeyChanged(object sender, Hotkey hotkey)
        {
            UIHotkey = hotkey;
        }
    }
}
