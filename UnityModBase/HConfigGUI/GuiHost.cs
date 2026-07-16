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
    /// 该组件在游戏启动后由 <see cref="GameBootRegistery"/> 创建，为每个已注册配置的用户挂载独立 GUI 上下文，
    /// 并负责热键显隐、普通窗口与模态热键录制窗口的绘制，以及销毁时解除全局事件订阅。
    /// 底层配置注册和持久化仍由用户服务与配置管理器负责。
    /// </summary>
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase
    {
        private ConfigEntry<Hotkey> _uiHotkeyEntry;

        /// <summary>
        /// 获取负责绘制当前配置模态窗口的编辑器。
        /// </summary>
        public PopupEditor PopupEditor { get; private set; }

        /// <summary>
        /// 初始化配置 GUI 依赖和窗口尺寸，为现有用户注册上下文，并订阅后续配置注册、语言及热键变更事件。
        /// 初始化失败时记录错误并销毁组件；<see cref="OnDestroy"/> 负责释放已完成的订阅。
        /// </summary>
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

        /// <summary>
        /// 为用户当前配置结构创建绑定树和提示订阅，并以模块键挂载到该用户上下文。
        /// 调用方应避免对同一用户重复注册同一模块键。
        /// </summary>
        /// <param name="context">配置已注册的用户上下文。</param>
        public void RegisterContext(UserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "UserContext cannot be null.");

            var guiContext = new GuiContext() { UserData = GroupBinding.CreateRoot(context) };
            guiContext.SubscribeToastNotifications(ToastEditor);

            context.AddChildContext(GuiContextKey, guiContext);
        }

        /// <summary>
        /// 除处理界面热键外，使用非缩放帧增量推进当前用户的延迟配置提交。
        /// </summary>
        public override void Update()
        {
            base.Update();
            (UserEditor as UserEditor)?.Update(CurrentContext as GuiContext, UnityService.UnscaledDeltaTime);
        }

        /// <summary>
        /// 可见时优先绘制当前上下文的模态弹窗；弹窗打开期间暂停普通配置窗口绘制。
        /// </summary>
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

        // Unity 销毁组件时解除全局订阅和用户子上下文，并通过编辑器释放热键录制会话。
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
