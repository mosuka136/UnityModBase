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
    /// <summary>
    /// 日志查看器的 Unity 宿主组件。
    /// 游戏启动时为每个用户创建日志绑定和数据库订阅，负责热键显隐、按内容宽度调整窗口并绘制表格；
    /// 不负责日志的内存收集、去重、容量控制或文件写入；前者由用户的日志数据库负责，文件持久化由日志写入器负责。
    /// </summary>
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase
    {
        private ConfigEntry<Hotkey> _uiHotkeyEntry;

        /// <summary>
        /// 初始化日志 GUI 依赖和窗口，为现有用户注册上下文，并订阅后续用户、语言和热键变更。
        /// 初始化失败时记录错误并销毁组件。
        /// </summary>
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

        /// <summary>
        /// 为用户建立当前日志快照、后续数据库事件订阅和复制提示订阅，并挂载日志 GUI 子上下文。
        /// 调用方应避免对同一用户重复注册相同模块键。
        /// </summary>
        /// <param name="context">要挂载日志 GUI 子上下文的用户上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void RegisterContext(UserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "UserContext cannot be null.");

            var userEditor = UserEditor as UserEditor;
            var guiContext = new GuiContext();
            var userData = new GroupBinding();

            guiContext.UserData = userData;

            guiContext.RegisterLogHandlers(context.Service.LogDatabase, userEditor);
            guiContext.SubscribeToastNotifications(ToastEditor);

            context.AddChildContext(GuiContextKey, guiContext);
        }

        // Unity 销毁组件时解除全局事件和每个用户的日志订阅，避免数据库继续回调已失效的编辑器。
        private void OnDestroy()
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
                var context = user.GetChildContext(GuiContextKey) as GuiContext;
                context?.Dispose();
                user.RemoveChildContext(GuiContextKey);
            }
        }

        /// <summary>
        /// 根据当前列总宽度调整窗口宽度到屏幕的 50%～90%，再交由通用宿主绘制。
        /// 列宽只影响横向窗口尺寸，窗口位置和高度保持不变。
        /// </summary>
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
