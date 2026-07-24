using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 为基于 Unity IMGUI 的用户级工具窗口提供宿主生命周期、用户上下文切换、热键显隐和浮层绘制能力。
    /// 当前选中用户被移除时，本类会尝试切换到用户注册表的默认项；派生类可拒绝不属于本模块的上下文。
    /// 派生类负责提供具体样式、窗口尺寸、用户数据编辑器及上下文注册；本类不创建或持久化业务数据。
    /// </summary>
    /// <remarks>
    /// <see cref="Awake"/> 会订阅进程级用户移除事件，<see cref="OnDestroy"/> 负责退订。
    /// 派生类覆盖销毁回调时必须调用基类实现，否则静态事件会继续持有已经销毁的宿主。
    /// 用户移除通知在调用 <see cref="UserManager.RemoveUser(string)"/> 的线程同步执行；宿主存活期间，
    /// 调用方必须将用户移除与 Unity 生命周期及 IMGUI 绘制串行化。
    /// 普通窗口绘制要求 <see cref="SelectedUserKey"/> 对应已注册用户；最后一个用户被移除后，
    /// 本类会清空选择和当前上下文。派生类必须先注册新用户及其模块上下文，才能再次绘制窗口。
    /// </remarks>
    public abstract class GuiHostBase : MonoBehaviour
    {
        /// <summary>
        /// 供 IMGUI 区分窗口的运行时标识；不作为持久标识，哈希碰撞未额外处理。
        /// </summary>
        public readonly int WindowID = Guid.NewGuid().GetHashCode();

        /// <summary>
        /// 当前选中用户标识的可变存储；用户选择流程会在窗口绘制结束时刷新 <see cref="CurrentContext"/>，
        /// 用户移除通知则会立即同步更新选择和上下文。
        /// </summary>
        protected string _selectedUserKey = string.Empty;
        /// <summary>
        /// 当前选中用户的标识。界面切换用户后，<see cref="CurrentContext"/> 会在本帧窗口绘制结束时同步更新；
        /// 当前用户被移除时会同步回退到剩余用户中的默认项，没有剩余用户时为空字符串。
        /// </summary>
        public string SelectedUserKey => _selectedUserKey;
        /// <summary>
        /// 存储在用户上下文中的子上下文键，由具体 GUI 模块在初始化时设置。
        /// </summary>
        public string GuiContextKey { get; protected set; } = string.Empty;

        /// <summary>
        /// 获取传给用户选择器的候选用户序列。本类不按模块上下文过滤该序列。
        /// </summary>
        public IEnumerable<UserContext> Users { get; protected set; }
        /// <summary>
        /// 获取当前选中用户在本 GUI 模块下的子上下文。
        /// 派生类初始化以及 <see cref="ChangeCurrentContext"/> 完成选择切换时会更新该引用。
        /// </summary>
        public IUserContext CurrentContext { get; protected set; }
        /// <summary>
        /// 获取负责用户选择和模块内容绘制的编辑器。
        /// </summary>
        public UserEditorBase UserEditor { get; protected set; }
        /// <summary>
        /// 获取短时提示消息编辑器。
        /// </summary>
        public ToastEditor ToastEditor { get; protected set; }
        /// <summary>
        /// 获取工具提示编辑器。
        /// </summary>
        public TooltipEditor TooltipEditor { get; protected set; }

        /// <summary>
        /// 获取 Unity 运行时服务抽象。
        /// </summary>
        public IUnityProvider UnityService { get; protected set; }
        /// <summary>
        /// 获取 IMGUI 调用抽象。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; protected set; }
        /// <summary>
        /// 获取当前模块提供的通用浮层样式资源。
        /// </summary>
        public IStyleResource StyleProvider { get; protected set; }

        /// <summary>
        /// 指示窗口是否参与当前帧绘制。
        /// </summary>
        public bool IsVisible { get; protected set; } = false;
        // 拖动释放产生的鼠标事件可能落在新窗口区域之外，继续自动隐藏会把正常拖动误判为失焦。
        /// <summary>
        /// 指示本次打开期间窗口是否移动过。移动后会禁用点击窗外自动隐藏，直至窗口再次隐藏。
        /// </summary>
        public bool HasDraggedWindowSinceOpen { get; protected set; } = false;

        /// <summary>
        /// 获取窗口标题的本地化文本。
        /// </summary>
        public Translator Title { get; protected set; }
        /// <summary>
        /// 获取当前窗口在屏幕坐标中的位置和尺寸。
        /// </summary>
        public Rect WindowRect { get; protected set; }
        /// <summary>
        /// 获取切换窗口显隐的运行时热键。
        /// </summary>
        public Hotkey UIHotkey { get; protected set; }

        /// <summary>
        /// 获取运行时依赖、创建通用浮层编辑器、选择默认用户，并订阅用户移除通知。
        /// 本方法不初始化 <see cref="Users"/>、<see cref="GuiContextKey"/>、<see cref="UserEditor"/> 或
        /// <see cref="CurrentContext"/>，这些模块级状态由派生类在调用后完成。
        /// 派生类必须先设置 <see cref="StyleProvider"/>；初始化失败时会记录错误并销毁当前组件。
        /// </summary>
        public virtual void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                UnityService = UnityProvider.Instance;

                ToastEditor = new ToastEditor(UnityService, UnityGui, StyleProvider);
                TooltipEditor = new TooltipEditor(UnityService, UnityGui, StyleProvider);

                _selectedUserKey = GetDefaultUserKey();
                UserManager.OnUserRemoved += OnUserRemoved;

                BLog.Debug($"[{WindowID}] GUI host created.");
            }
            catch (Exception ex)
            {
                BLog.Error($"[{WindowID}] Failed to create GUI host.", ex);
                Destroy(this);
            }
        }

        /// <summary>
        /// 在 Unity 更新阶段轮询界面热键，并切换窗口显隐状态。
        /// </summary>
        public virtual void Update()
        {
            if (UIHotkey?.WasPressedThisFrame() == true)
            {
                BLog.Debug($"[{WindowID}] Config GUI toggle hotkey pressed.");
                ToggleVisibility();
            }
        }

        /// <summary>
        /// 绘制可见窗口、记录拖动结果，并在满足条件时处理点击窗外自动隐藏。
        /// </summary>
        public virtual void OnGUI()
        {
            if (!IsVisible)
                return;

            var rect = GUI.Window(WindowID, WindowRect, DrawWindow, Title);

            if (!HasDraggedWindowSinceOpen && WindowRect.position != rect.position)
                HasDraggedWindowSinceOpen = true;
            WindowRect = rect;

            TryAutoHideOnFocusLost();
        }

        /// <summary>
        /// 绘制用户选择器、当前用户内容、提示浮层和可拖动区域。
        /// 用户选择器直接使用 <see cref="Users"/>；选择变化后才解析并验证对应模块上下文。
        /// </summary>
        /// <param name="id">Unity IMGUI 传入的窗口标识。</param>
        /// <remarks>
        /// 即使用户编辑器抛出异常，也会在传播异常前闭合 Area 布局；若编辑器已经改写选择，
        /// 清理阶段仍会尝试同步上下文。上下文切换本身再次失败时，后一个异常会替代原绘制异常。
        /// </remarks>
        public virtual void DrawWindow(int id)
        {
            var selectedUserKey = _selectedUserKey;

            UnityGui.BeginArea(new Rect(10f, 30f, WindowRect.width - 20f, WindowRect.height - 40f));
            try
            {
                UserEditor.Draw(Users, ref _selectedUserKey, CurrentContext);
            }
            finally
            {
                try
                {
                    UnityGui.EndArea();
                }
                finally
                {
                    if (selectedUserKey != _selectedUserKey)
                        ChangeCurrentContext(_selectedUserKey);
                }
            }

            ToastEditor.DrawToast(WindowRect);
            TooltipEditor.DrawTooltip(WindowRect);
            GUI.DragWindow();
        }

        /// <summary>
        /// 在窗口尚未被拖动时，将窗外鼠标按下视为失焦并隐藏窗口。
        /// 该方法依赖当前 IMGUI 事件，只应在 <see cref="OnGUI"/> 调用链中执行。
        /// </summary>
        public virtual void TryAutoHideOnFocusLost()
        {
            if (HasDraggedWindowSinceOpen)
                return;

            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.MouseDown)
                return;

            if (WindowRect.Contains(currentEvent.mousePosition))
                return;

            BLog.Debug($"[{WindowID}] GUI auto-hidden because focus was lost.");
            Hide();
            GUI.FocusControl(null);
        }

        /// <summary>
        /// 隐藏窗口并重置本次打开期间的拖动状态；重复调用不会产生额外状态变化。
        /// </summary>
        public virtual void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            HasDraggedWindowSinceOpen = false;

            BLog.Debug($"[{WindowID}] GUI hidden.");
        }

        /// <summary>
        /// 切换窗口可见性。由可见切换为隐藏时同时清除拖动状态。
        /// </summary>
        public virtual void ToggleVisibility()
        {
            if (IsVisible)
                Hide();
            else
            {
                IsVisible = true;
                BLog.Debug($"[{WindowID}] GUI shown.");
            }
        }

        /// <summary>
        /// 获取指定用户挂载的当前 GUI 模块上下文。
        /// 本方法只查询现有用户及其直接子上下文，不会创建用户或模块上下文。
        /// </summary>
        /// <param name="key">已注册用户的非空标识。</param>
        /// <returns>模块子上下文；用户不存在或尚未挂载对应子上下文时为 <c>null</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> 为 <c>null</c> 或空字符串时抛出。</exception>
        /// <exception cref="ArgumentException">已找到用户，但 <see cref="GuiContextKey"/> 尚未设置为非空白键时抛出。</exception>
        public IUserContext GetContext(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            var user = UserManager.GetUser(key);
            return user?.GetChildContext(GuiContextKey);
        }

        /// <summary>
        /// 获取用户管理器当前定义的默认用户标识，不检查该用户是否挂载了当前模块上下文。
        /// </summary>
        /// <returns>当前注册表枚举顺序中的首个用户标识；没有注册用户时为空字符串。</returns>
        public string GetDefaultUserKey()
        {
            return UserManager.GetDefaultUserId();
        }

        /// <summary>
        /// 判断挂载在用户下的上下文是否可以由当前模块使用。
        /// 该检查只在切换上下文时执行，不会从 <see cref="Users"/> 中移除候选用户。
        /// 派生宿主可以覆盖本方法，补充具体上下文类型或哨兵状态检查。
        /// </summary>
        /// <param name="context">候选模块上下文。</param>
        /// <returns>上下文可以安全用于当前模块时为 <c>true</c>。</returns>
        protected virtual bool IsContextValid(IUserContext context)
        {
            return context != null;
        }

        /// <summary>
        /// 在通过非空用户标识切换上下文或因候选无效而清空上下文前，
        /// 通知派生宿主处理待提交值和瞬态编辑状态。
        /// </summary>
        /// <param name="currentContext">切换前的模块上下文，可能为 null。</param>
        /// <param name="nextContext">即将采用的模块上下文，可能为 null。</param>
        /// <remarks>
        /// 用户注册表为空时，<see cref="ChangeCurrentContext"/> 会直接清空宿主状态，不调用本方法；
        /// 派生模块应通过其子上下文的 <see cref="IDisposable.Dispose"/> 处理随用户释放的资源。
        /// </remarks>
        protected virtual void OnCurrentContextChanging(IUserContext currentContext, IUserContext nextContext)
        {
        }

        /// <summary>
        /// 在 Unity 销毁宿主时解除用户移除订阅，避免进程级事件保留失效组件。
        /// </summary>
        /// <remarks>派生类覆盖此生命周期方法时必须调用基类实现。</remarks>
        protected virtual void OnDestroy()
        {
            UserManager.OnUserRemoved -= OnUserRemoved;
        }

        /// <summary>
        /// 响应用户移除通知。非当前用户的移除不会改变界面状态；当前用户被移除后，
        /// 优先切换到注册表默认用户的有效模块上下文，没有可用候选时清空选择和当前上下文。
        /// </summary>
        /// <param name="userId">已经完成释放并从用户注册表移除的用户标识。</param>
        /// <remarks>
        /// 该回调由 <see cref="UserManager.RemoveUser(string)"/> 在调用线程同步执行。
        /// 通知发生时原用户上下文已经释放并移出注册表。注册表为空时默认用户键为空，
        /// 此路径会直接清空宿主状态，不再查询用户管理器，也不会触发上下文切换回调。
        /// </remarks>
        private void OnUserRemoved(string userId)
        {
            if (_selectedUserKey == userId)
            {
                _selectedUserKey = GetDefaultUserKey();
                ChangeCurrentContext(_selectedUserKey);
            }
        }

        /// <summary>
        /// 解析并验证目标用户的模块上下文，在替换字段前通知派生宿主清理旧上下文状态。
        /// 无效候选会把选择键和当前上下文清空；本方法不会继续搜索其他用户。
        /// 空键表示用户注册表中已无回退项，会直接清空宿主状态。
        /// </summary>
        /// <param name="userKey">要解析的用户标识；null、空字符串或未知用户都会清空当前选择。</param>
        /// <exception cref="ArgumentException">
        /// 用户存在，但 <see cref="GuiContextKey"/> 尚未设置为非空白键时抛出。
        /// </exception>
        /// <remarks>
        /// null 或空字符串路径不执行 <see cref="IsContextValid"/>、<see cref="OnCurrentContextChanging"/>，
        /// 也不会标记用户编辑器状态；该路径仅用于没有默认用户可供切换的注册表状态。
        /// </remarks>
        private void ChangeCurrentContext(string userKey)
        {
            if (string.IsNullOrEmpty(userKey))
            {
                // 最后一个用户移除后默认键为空；避免把该哨兵值传给要求非空标识的 GetContext。
                _selectedUserKey = string.Empty;
                CurrentContext = null;
                return;
            }

            IUserContext nextContext = null;
            var candidate = GetContext(userKey);

            if (IsContextValid(candidate))
                nextContext = candidate;
            else
                userKey = string.Empty;

            // 派生宿主需要在字段替换前读取旧上下文，以提交缓冲并清理与其绑定的瞬态会话。
            OnCurrentContextChanging(CurrentContext, nextContext);
            _selectedUserKey = userKey;
            CurrentContext = nextContext;

            if (nextContext != null)
                UserEditor?.SetStatusDirty(nextContext);
        }
    }
}
