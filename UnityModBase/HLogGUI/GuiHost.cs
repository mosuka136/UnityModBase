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
    public sealed class GuiHost : GuiHostBase
    {
        // 保留配置项引用仅为在 OnDestroy 中退订热键变更；实时热键值存于基类 UIHotkey。
        private ConfigEntry<Hotkey> _uiHotkeyEntry;

        /// <summary>
        /// 日志窗口宽度每帧按列总宽度自适应，横向拉伸会被覆盖，因此只开放纵向拉伸。
        /// </summary>
        protected override WindowResizeEdge AllowedResizeEdges
        {
            get { return WindowResizeEdge.Top | WindowResizeEdge.Bottom; }
        }

        /// <summary>
        /// 初始化日志 GUI 依赖和窗口，为现有用户注册上下文，并订阅后续用户注册、语言和热键变更；
        /// 用户移除由通用宿主订阅并负责切换当前上下文。
        /// 初始化时必须至少存在一个注册用户，否则当前上下文无法解析，宿主会记录错误并销毁组件。
        /// </summary>
        protected override void Awake()
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
                var width = UnityGui.ScreenWidth * 0.8f;
                var height = UnityGui.ScreenHeight * 0.5f;
                WindowRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);
                InitializeWindowRect(
                    BConfigManager.LogGuiX,
                    BConfigManager.LogGuiY,
                    BConfigManager.LogGuiWidth,
                    BConfigManager.LogGuiHeight);

                BLog.Debug($"Log GUI host initialized. WindowId={WindowID}, ContextKey='{GuiContextKey}', Size={width}x{height}.");
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to initialize Log GUI host. WindowId={WindowID}, ContextKey='{GuiContextKey}'.", ex);
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
        /// 只允许宿主切换到当前日志模块创建的上下文，避免把错误类型当作日志界面状态使用。
        /// </summary>
        /// <param name="context">通用宿主解析到的候选上下文。</param>
        /// <returns>候选项是日志 GUI 上下文时为 <c>true</c>。</returns>
        protected override bool IsContextValid(IUserContext context)
        {
            return context is GuiContext;
        }

        /// <summary>
        /// 在 Layout 事件先完成列宽测量再把窗口宽度调整到屏幕的 50%～90%，随后交由通用宿主绘制。
        /// 测量前置到 <c>GUI.Window</c> 之前，使窗口唤出的首个布局趟就携带最终宽度，
        /// 同帧 Repaint 直接以最终宽度渲染，避免首帧旧宽度、次帧自适应宽度的两段式跳变；
        /// 未完成第一次测量时保持已有宽度。宽度只在 Layout 事件写入，Layout/Repaint 不会使用不同宽度。
        /// 列宽只影响横向窗口尺寸，窗口位置和高度保持不变；当前模块上下文缺失时跳过自适应计算。
        /// </summary>
        protected override void OnGUI()
        {
            if (IsVisible && UnityService.EventCurrent?.type == EventType.Layout)
            {
                if (CurrentContext is GuiContext context)
                    (UserEditor as UserEditor)?.GroupEditor.UpdateLayoutIfNeeded(context);
                TryApplyMeasuredColumnWidth();
            }

            base.OnGUI();
        }

        /// <summary>
        /// 在已完成列宽测量时把窗口宽度限制到屏幕的 50%～90%。
        /// 尚未测量或缺少运行时依赖时保持现有宽度。
        /// </summary>
        private void TryApplyMeasuredColumnWidth()
        {
            if (!(CurrentContext is GuiContext context) || context.TotalColumnWidth <= 0f)
                return;

            var rect = WindowRect;
            rect.width = UnityService.Clamp(context.TotalColumnWidth, UnityGui.ScreenWidth * 0.5f, UnityGui.ScreenWidth * 0.9f);
            WindowRect = rect;
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
