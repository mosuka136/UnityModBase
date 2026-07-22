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
    /// 该组件在游戏启动后由 <see cref="GameBootRegistry"/> 创建，为用户挂载由其当前配置模型投影出的独立 GUI 上下文，
    /// 并负责在配置模型变化后重建绑定树，以及处理热键显隐、普通窗口与模态热键录制窗口的绘制。
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
        /// 初始化配置 GUI 依赖和窗口尺寸，为当前已注册用户投影配置上下文，并订阅后续配置模型、语言及热键变更事件。
        /// 当前用户尚无配置服务时仍会挂载空绑定树；宿主启动后新增的用户会在配置模型首次发出变化通知时创建 GUI 上下文。
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
                UserManager.OnConfigChanged += OnConfigChanged;
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

            var guiContext = new GuiContext() { UserData = GroupBinding.CreateRoot(context) };
            context.AddChildContext(GuiContextKey, guiContext);
            guiContext.SubscribeToastNotifications(ToastEditor);
        }

        /// <summary>
        /// 除处理界面热键外，使用非缩放帧增量推进当前用户的延迟配置提交。
        /// 当前用户尚未挂载配置 GUI 上下文时只处理通用热键，不读取帧增量或推进提交器。
        /// </summary>
        public override void Update()
        {
            base.Update();
            if (CurrentContext is GuiContext context)
                (UserEditor as UserEditor)?.Update(context, UnityService.UnscaledDeltaTime);
        }

        /// <summary>
        /// 可见时优先绘制当前上下文的模态弹窗；弹窗打开期间暂停普通配置窗口绘制。
        /// </summary>
        public override void OnGUI()
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

        // Unity 销毁组件时解除配置模型、语言及热键订阅，移除用户子上下文，并通过编辑器释放热键录制会话。
        private void OnDestroy()
        {
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
        /// 复用原上下文会保留该用户的提交器、弹窗和选择状态；绑定树按当前完整运行时模型重新创建。
        /// </summary>
        /// <param name="userContext">配置模型发生变化的用户；<c>null</c> 按空操作处理。</param>
        /// <remarks>
        /// 模型事件不会切换到 Unity 主线程，本方法也不提供并发保护；配置声明、重载和 GUI 生命周期必须由调用方串行化。
        /// 每次通知都会全量重建绑定树，且不会取消旧绑定上的延迟提交或弹窗回调；批量声明配置时可能连续执行多次。
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
                context.UserData = GroupBinding.CreateRoot(userContext);
                UserEditor?.SetStatusDirty(context);
            }
        }
    }
}
