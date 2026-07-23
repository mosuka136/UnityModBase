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
        /// 初始化日志 GUI 依赖和窗口，为现有用户注册上下文，并订阅后续用户注册、语言和热键变更；
        /// 用户移除由通用宿主订阅并负责切换当前上下文。
        /// 初始化时必须至少存在一个注册用户，否则当前上下文无法解析，宿主会记录错误并销毁组件。
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
                CurrentContext = GetContext(SelectedUserKey);
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
        /// 同一模块键不会覆盖已有上下文，重复注册会抛出异常。
        /// </summary>
        /// <param name="context">要挂载日志 GUI 子上下文的用户上下文。</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="context"/>、该用户的日志数据库、日志编辑器或提示编辑器尚不可用时抛出。
        /// </exception>
        /// <exception cref="NullReferenceException"><paramref name="context"/> 的用户服务引用为 <c>null</c> 时抛出。</exception>
        /// <exception cref="ArgumentException"><see cref="GuiHostBase.GuiContextKey"/> 尚未初始化为非空白键时抛出。</exception>
        /// <exception cref="InvalidOperationException">该用户已挂载同键 GUI 上下文时抛出。</exception>
        /// <remarks>
        /// 只能在通用宿主和日志编辑器初始化完成、用户服务仍持有有效日志数据库且 <see cref="GuiHostBase.GuiContextKey"/> 已设置后调用。
        /// 新上下文会先建立数据库和提示订阅，再尝试挂载；挂载失败时不会解除临时上下文已经建立的外部事件订阅，
        /// 因此调用方应在调用前确保当前模块键尚未被占用。
        /// </remarks>
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

        /// <summary>
        /// 在 Unity 销毁宿主时通过基类解除用户移除订阅，再解除用户注册、语言及热键订阅，
        /// 并释放每个用户的日志 GUI 上下文，避免日志数据库继续回调已经失效的编辑器。
        /// </summary>
        /// <remarks>覆盖该生命周期方法时必须调用基类实现，以解除通用宿主建立的进程级用户移除订阅。</remarks>
        protected override void OnDestroy()
        {
            base.OnDestroy();

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
        /// 列宽只影响横向窗口尺寸，窗口位置和高度保持不变；当前模块上下文缺失时跳过自适应计算，沿用既有宽度进入通用绘制。
        /// </summary>
        public override void OnGUI()
        {
            if (CurrentContext is GuiContext context)
            {
                var rect = WindowRect;
                rect.width = UnityService.Clamp(context.TotalColumnWidth, UnityGui.ScreenWidth * 0.5f, UnityGui.ScreenWidth * 0.9f);
                WindowRect = rect;
            }

            base.OnGUI();
        }

        private void OnDefaultLanguageChanged(object sender, LanguageType language)
        {
            if (CurrentContext is GuiContext context)
                UserEditor.SetStatusDirty(context);
        }

        private void OnLogUIHotkeyChanged(object sender, Hotkey hotkey)
        {
            UIHotkey = hotkey;
        }
    }
}
