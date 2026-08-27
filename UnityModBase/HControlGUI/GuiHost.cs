using System;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;
using UnityModBase.HControlGUI.Bindings;
using UnityModBase.HControlGUI.Editor;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;
using ControlTranslatorResource = UnityModBase.HControlGUI.Resource.TranslatorResource;

namespace UnityModBase.HControlGUI
{
    /// <summary>
    /// 实时控制界面的 Unity 宿主。负责独立热键显隐、所有用户的刷新调度和纯内存控制树绘制。
    /// </summary>
    [RegisterOnGameBoot]
    public class GuiHost : GuiHostBase
    {
        private ConfigEntry<Hotkey> _uiHotkeyEntry;
        private bool _wasVisible;

        /// <inheritdoc/>
        public override void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                var styleProvider = new EntryStyleResource(UnityGui);
                StyleProvider = styleProvider;
                base.Awake();

                var userEditor = new UserEditor(UnityService, UnityGui, styleProvider);
                UserEditor = userEditor;

                GuiContextKey = nameof(HControlGUI);
                Users = UserManager.UserContexts;
                foreach (var user in Users)
                    RegisterContext(user);
                UserManager.OnUserRegistered += RegisterContext;
                CurrentContext = GetContext(SelectedUserKey);
                Translator.OnDefaultLanguageChanged += OnDefaultLanguageChanged;

                _uiHotkeyEntry = BConfigManager.ControlUIHotkey;
                UIHotkey = _uiHotkeyEntry.Value;
                _uiHotkeyEntry.OnValueChanged += OnControlUIHotkeyChanged;

                Title = ControlTranslatorResource.Title;
                SetCenteredWindowRect(0.35f, 0.7f);

                BLog.Debug($"Control GUI host initialized. WindowId={WindowID}, ContextKey='{GuiContextKey}', Size={WindowRect.width}x{WindowRect.height}.");
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to initialize Control GUI host. WindowId={WindowID}, ContextKey='{GuiContextKey}'.", ex);
                Destroy(this);
            }
        }

        /// <summary>
        /// 为用户挂载实时控制 GUI 上下文并订阅模型结构变化。
        /// </summary>
        public void RegisterContext(UserContext user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            var control = user.Service?.Control ?? throw new InvalidOperationException($"Control service is unavailable for user '{user.UserId}'.");

            var context = new GuiContext(control)
            {
                UserData = GroupBindingFactory.CreateRoot(user)
            };
            user.AddChildContext(GuiContextKey, context);
            context.SubscribeToastNotifications(ToastEditor);
            context.SubscribeStructureChanged(() => OnControlStructureChanged(user));
        }

        /// <summary>
        /// 先推进所有用户的延迟界面输入，再按策略刷新所有用户的实时控制项。
        /// </summary>
        public override void Update()
        {
            base.Update();

            var becameVisible = IsVisible && !_wasVisible;
            var deltaTime = UnityService.UnscaledDeltaTime;
            foreach (var user in (Users ?? Array.Empty<UserContext>()).ToArray())
            {
                var context = user?.GetChildContext(GuiContextKey) as GuiContext;
                if (context != null)
                    (UserEditor as UserEditor)?.Update(context, deltaTime);

                user?.Service?.Control?.Update(deltaTime, IsVisible, becameVisible);
            }
            _wasVisible = IsVisible;
        }

        /// <inheritdoc/>
        protected override bool IsContextValid(IUserContext context)
        {
            return context is GuiContext guiContext && guiContext.IsValid;
        }

        /// <summary>切换用户前提交旧上下文的有效延迟输入。</summary>
        protected override void OnCurrentContextChanging(IUserContext currentContext, IUserContext nextContext)
        {
            if (!(currentContext is GuiContext context) || ReferenceEquals(currentContext, nextContext))
                return;
            CommitPendingEdits(context);
        }

        /// <inheritdoc/>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            UserManager.OnUserRegistered -= RegisterContext;
            Translator.OnDefaultLanguageChanged -= OnDefaultLanguageChanged;

            if (_uiHotkeyEntry != null)
            {
                _uiHotkeyEntry.OnValueChanged -= OnControlUIHotkeyChanged;
                _uiHotkeyEntry = null;
            }

            UserEditor?.Dispose();

            foreach (var user in (Users ?? Array.Empty<UserContext>()).ToArray())
            {
                var context = user.GetChildContext(GuiContextKey) as GuiContext;
                context?.Dispose();
                user.RemoveChildContext(GuiContextKey);
            }
        }

        private void OnControlStructureChanged(UserContext user)
        {
            var context = user?.GetChildContext(GuiContextKey) as GuiContext;
            if (context == null)
                return;

            CommitPendingEdits(context);
            context.UserData = GroupBindingFactory.CreateRoot(user);
            UserEditor?.SetStatusDirty(context);
        }

        private void OnDefaultLanguageChanged(object sender, LanguageType language)
        {
            foreach (var user in (Users ?? Array.Empty<UserContext>()).ToArray())
            {
                if (user.GetChildContext(GuiContextKey) is GuiContext context)
                    UserEditor?.SetStatusDirty(context);
            }
        }

        private void OnControlUIHotkeyChanged(object sender, Hotkey hotkey)
        {
            UIHotkey = hotkey;
        }

        private static void CommitPendingEdits(GuiContext context)
        {
            try
            {
                context.ChangeSink.CommitPending();
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to commit pending control edits. SelectedGroup='{context.SelectedGroupKey}'.", ex);
            }
        }
    }
}
