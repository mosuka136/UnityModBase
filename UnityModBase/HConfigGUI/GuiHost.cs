using System;
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
    /// 该组件在游戏启动后由 <see cref="GameBootRegistry"/> 创建，为用户挂载由其当前配置模型投影出的独立 GUI 上下文，
    /// 并负责在配置模型变化后重建绑定树，以及处理热键显隐、普通窗口与模态热键录制窗口的绘制。
    /// 底层配置注册和持久化仍由用户服务与配置管理器负责。
    /// </summary>
    [RegisterOnGameBoot]
    public sealed class GuiHost : GuiHostBase
    {
        // 保留配置项引用仅为在 OnDestroy 中退订热键变更；实时热键值存于基类 UIHotkey。
        private ConfigEntry<Hotkey> _uiHotkeyEntry;

        /// <summary>
        /// 获取负责绘制当前配置模态窗口的编辑器。
        /// </summary>
        public PopupEditor PopupEditor { get; private set; }

        /// <summary>
        /// 初始化配置 GUI 依赖和窗口尺寸，为当前已注册用户投影配置上下文，并订阅配置模型、语言及热键变更事件；
        /// 用户移除由通用宿主订阅并负责切换当前上下文。
        /// 当前用户尚无配置服务时仍会挂载空绑定树；宿主启动后新增的用户会在配置模型首次发出变化通知时创建 GUI 上下文。
        /// 初始化时必须至少存在一个注册用户，否则当前上下文无法解析，宿主会记录错误并销毁组件。
        /// <see cref="OnDestroy"/> 负责释放初始化期间已经建立的订阅。
        /// </summary>
        protected override void Awake()
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
                UserManager.OnConfigChanged += OnConfigChanged;
                CurrentContext = GetContext(SelectedUserKey);

                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider);
                UserEditor = userEditor;
                Translator.OnDefaultLanguageChanged += OnDefaultLanguageChanged;

                PopupEditor = new PopupEditor(UnityGui, styleProvider);

                _uiHotkeyEntry = BConfigManager.ConfigUIHotkey;
                UIHotkey = _uiHotkeyEntry.Value;
                _uiHotkeyEntry.OnValueChanged += OnConfigUIHotkeyChanged;

                Title = TranslatorResource.Title;
                SetCenteredWindowRect(0.35f, 0.7f);

                BLog.Debug($"Config GUI host initialized. WindowId={WindowID}, ContextKey='{GuiContextKey}', Size={WindowRect.width}x{WindowRect.height}.");
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to initialize Config GUI host. WindowId={WindowID}, ContextKey='{GuiContextKey}'.", ex);
                Destroy(this);
            }
        }

        /// <summary>
        /// 为用户当前配置结构创建绑定树，以模块键挂载到用户上下文后再建立提示订阅。
        /// 该方法不替换已有同键上下文；重复调用会保留原绑定树并抛出异常，因此不会刷新已经挂载的配置结构。
        /// </summary>
        /// <param name="context">要投影当前配置结构的用户上下文；尚无配置服务时会创建空根节点。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <c>null</c>，或宿主的提示编辑器尚未初始化时抛出。</exception>
        /// <exception cref="ArgumentException"><see cref="GuiHostBase.GuiContextKey"/> 尚未初始化为非空白键时抛出。</exception>
        /// <exception cref="InvalidOperationException">该用户已挂载同键 GUI 上下文时抛出。</exception>
        /// <remarks>
        /// 只能在通用宿主初始化完成且 <see cref="GuiHostBase.GuiContextKey"/> 已设置后调用。
        /// 先挂载再订阅可确保同键冲突时不会让未被用户上下文接管的临时对象持有提示订阅。
        /// 如果提示编辑器不可用，异常发生前新上下文已经挂载，但不会建立提示订阅；调用方不应在宿主初始化完成前调用。
        /// </remarks>
        public void RegisterContext(UserContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "UserContext cannot be null.");

            var guiContext = new GuiContext() { UserData = GroupBindingFactory.CreateRoot(context) };
            context.AddChildContext(GuiContextKey, guiContext);
            guiContext.SubscribeToastNotifications(ToastEditor);
        }

        /// <summary>
        /// 除处理界面热键外，使用非缩放帧增量推进当前用户的延迟配置提交。
        /// 当前用户尚未挂载配置 GUI 上下文时只处理通用热键，不读取帧增量或推进提交器。
        /// </summary>
        protected override void Update()
        {
            base.Update();
            if (CurrentContext is GuiContext context)
                (UserEditor as UserEditor)?.Update(context, UnityService.UnscaledDeltaTime);
        }

        /// <summary>
        /// 可见时优先绘制当前上下文的模态弹窗；弹窗打开期间暂停普通配置窗口绘制。
        /// </summary>
        protected override void OnGUI()
        {
            if (!IsVisible)
                return;

            var context = CurrentContext as GuiContext;
            if (context?.Popup.IsOpen == true)
            {
                PopupEditor.DrawPopup(context);
                return;
            }

            base.OnGUI();
        }

        /// <summary>
        /// 在 Unity 销毁宿主时通过基类解除用户移除订阅，再解除配置模型、语言及热键订阅，移除配置 GUI 子上下文，
        /// 并通过编辑器结束仍未完成的热键录制会话。
        /// </summary>
        /// <remarks>覆盖该生命周期方法时必须调用基类实现，以解除通用宿主建立的进程级用户移除订阅。</remarks>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            UserManager.OnConfigChanged -= OnConfigChanged;
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

        /// <summary>
        /// 仅接受当前配置模块创建的非哨兵上下文，避免宿主切换到错误类型或共享无效实例。
        /// </summary>
        /// <param name="context">通用宿主解析到的候选上下文。</param>
        /// <returns>候选项是独立且有效的配置 GUI 上下文时为 <c>true</c>。</returns>
        protected override bool IsContextValid(IUserContext context)
        {
            return context is GuiContext guiContext && guiContext.IsValid;
        }

        /// <summary>
        /// 宿主通过非空目标用户切换上下文，或因目标上下文无效而清空当前上下文前，
        /// 立即提交有效延迟输入，并取消当前热键录制和模态弹窗。
        /// 提交失败只记录日志，不能阻止界面切换到仍然有效的用户。
        /// </summary>
        /// <param name="currentContext">即将离开的配置 GUI 上下文；类型不匹配时按空操作处理。</param>
        /// <param name="nextContext">即将采用的上下文；仅用于判断是否发生实际切换。</param>
        /// <remarks>
        /// 用户移除通知发生在原用户上下文释放之后，因此由移除触发时，延迟输入通常已由
        /// <see cref="EditableGuiContext.Dispose"/> 提交。注册表为空时基类会直接清空宿主状态而不调用本方法；
        /// 该路径的资源释放依赖用户上下文的级联释放。
        /// </remarks>
        protected override void OnCurrentContextChanging(IUserContext currentContext, IUserContext nextContext)
        {
            if (!(currentContext is GuiContext context) || ReferenceEquals(currentContext, nextContext))
                return;

            CommitPendingEdits(context);
            ClosePopup(context);
            (UserEditor as UserEditor)?.GroupEditor.HotkeyEditor.Session.CancelEdit();
        }

        // 语言切换后文案长度变化，标记当前用户的布局缓存失效以重新测量标签和按钮宽度。
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

        /// <summary>
        /// 响应用户配置模型变化：首次变化时挂载 GUI 上下文，后续变化仅替换根绑定并标记布局失效。
        /// 复用原上下文会保留该用户的提交器和选择状态；替换绑定树前会提交延迟输入，
        /// 当前用户的热键会话与弹窗则会取消，绑定树随后按当前完整运行时模型重新创建。
        /// </summary>
        /// <param name="userContext">配置模型发生变化的用户；<c>null</c> 按空操作处理。</param>
        /// <remarks>
        /// 模型事件不会切换到 Unity 主线程，本方法也不提供并发保护；配置声明、重载和 GUI 生命周期必须由调用方串行化。
        /// 每次通知都会全量重建绑定树；批量声明配置时可能连续执行多次。
        /// </remarks>
        private void OnConfigChanged(UserContext userContext)
        {
            if (userContext == null)
                return;

            var context = userContext.GetChildContext(GuiContextKey) as GuiContext;
            if (context == null)
            {
                RegisterContext(userContext);
                if (SelectedUserKey == userContext.UserId)
                    CurrentContext = GetContext(SelectedUserKey);
            }
            else
            {
                CommitPendingEdits(context);
                if (ReferenceEquals(CurrentContext, context))
                {
                    ClosePopup(context);
                    (UserEditor as UserEditor)?.GroupEditor.HotkeyEditor.Session.CancelEdit();
                }

                context.UserData = GroupBindingFactory.CreateRoot(userContext);
                UserEditor?.SetStatusDirty(context);
            }
        }

        private static void CommitPendingEdits(GuiContext context)
        {
            try
            {
                context.ChangeSink.CommitPending();
            }
            catch (Exception ex)
            {
                // 配置 setter 或变更订阅者失败不能阻止宿主离开已经失效或即将替换的上下文。
                BLog.Error($"Failed to commit pending config edits while changing GUI context. SelectedGroup='{context.SelectedGroupKey}'.", ex);
            }
        }

        private static void ClosePopup(GuiContext context)
        {
            var popup = context.Popup;
            var popupTitle = popup.Title?.ToString() ?? "<none>";
            var closeAction = popup.IsOpen ? popup.CloseAction : null;

            // 先断开弹窗状态再进入外部回调，避免回调重入绘制流程时再次观察到过期的绑定和委托。
            popup.IsOpen = false;
            popup.Title = null;
            popup.DrawAction = null;
            popup.CloseAction = null;

            try
            {
                closeAction?.Invoke();
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to run the config popup close callback while changing context. PopupTitle='{popupTitle}'.", ex);
            }
        }
    }
}
