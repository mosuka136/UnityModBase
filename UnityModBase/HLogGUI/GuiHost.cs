using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase
    {
        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new StyleResource(UnityGui);
                StyleProvider = styleProvider;
                base.Awake();

                GuiContextKey = nameof(HLogGUI);
                Users = UserManager.UserContexts;
                foreach (var user in Users)
                    RegisterContext(user);
                UserManager.OnUserRegistered += RegisterContext;
                CurrentContext = GetContext(_selectedUserKey);

                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider);
                userEditor.RegisterToastHandler(ToastEditor);
                Translator.OnDefaultLanguageChanged += (s, e) => userEditor.GroupEditor.IsColumnWidthDirty = true;
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

        public void RegisterContext(UserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "UserContext cannot be null.");

            var guiContext = new GuiContext();
            var userData = new GroupBinding();

            guiContext.UserData = userData;

            foreach (var log in context.Service.LogDatabase.Logs)
                userData.AddEntry(new EntryBinding(log));
            context.Service.LogDatabase.OnLogAdded += l => userData.AddEntry(new EntryBinding(l));
            context.Service.LogDatabase.OnLogRepeated += l => userData.AddEntry(new EntryBinding(l));

            context.AddContext(GuiContextKey, guiContext);
        }

        public void OnDestroy()
        {
            UserManager.OnUserRegistered -= RegisterContext;
        }

        public override void OnGUI()
        {
            var userEditor = UserEditor as UserEditor;
            var rect = WindowRect;
            rect.width = UnityService.Clamp(userEditor.GroupEditor.TotalColumnWidth, UnityGui.ScreenWidth * 0.5f, UnityGui.ScreenWidth * 0.9f);
            WindowRect = rect;

            base.OnGUI();
        }
    }
}
