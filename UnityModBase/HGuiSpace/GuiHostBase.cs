using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HConfigSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 为基于 Unity IMGUI 的用户级工具窗口提供宿主生命周期、用户上下文切换、热键显隐和浮层绘制能力。
    /// 当前选中用户被移除时，本类会尝试切换到用户注册表的默认项；派生类可拒绝不属于本模块的上下文。
    /// 派生类负责提供具体样式、窗口尺寸、用户数据编辑器及上下文注册；本类不创建或持久化业务数据，
    /// 仅在派生类注入持久化条目后，负责窗口布局与选中用户的恢复和写回
    /// （见 <see cref="InitializeWindowRect(ConfigEntry{float},ConfigEntry{float},ConfigEntry{float},ConfigEntry{float})"/> 与 <see cref="InitializeSelectedUser"/>）。
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
        /// 注入持久化条目后（见 <see cref="InitializeSelectedUser"/>），启动时从条目恢复该值，之后每次变化由基类写回。
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

        /// <summary>
        /// 指示本次打开期间窗口是否被拖动或拉伸过。此后会禁用点击窗外自动隐藏，直至窗口再次隐藏：
        /// 拖动释放产生的鼠标事件可能落在新窗口区域之外，继续自动隐藏会把正常拖动误判为失焦。
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
        /// 获取窗口允许鼠标拉伸的边缘与角，默认全部方向。
        /// 派生类可覆写以收窄方向，例如宽度按内容自适应的窗口只开放纵向拉伸。
        /// </summary>
        protected virtual WindowResizeEdge AllowedResizeEdges
        {
            get { return WindowResizeEdge.All; }
        }

        /// <summary>
        /// 获取窗口可被拉伸到的最小尺寸，默认 280×180。
        /// </summary>
        protected virtual Vector2 MinWindowSize
        {
            get { return new Vector2(280f, 180f); }
        }

        /// <summary>
        /// 获取切换窗口显隐的运行时热键。
        /// </summary>
        public Hotkey UIHotkey { get; protected set; }

        // 内容区相对窗口客户区的边距；标题栏高度决定内容区起始位置。
        private const float WindowContentPadding = 10f;
        private const float WindowTitleBarHeight = 30f;

        // 拉伸事件在窗口回调内计算，结果经该字段暂存，由 OnGUI 在 GUI.Window 返回后统一写回，
        // 避免被 GUI.Window 的返回矩形覆盖。
        private bool _isWindowResizing;
        private WindowResizeEdge _resizeEdge;
        private WindowResizeAnchors _resizeAnchors;
        private Rect? _pendingWindowResizeRect;

        // 本次打开后是否已经用 Layout 事件调用过 GUI.Window。未完成前跳过 KeyDown/Repaint，
        // 避免热键在 Update 中打开窗口后，同一帧用没有配对 Layout 的事件画出错位内容。
        private bool _hasLaidOutSinceShow;
        // 是否仍在等待 GUI.Window 返回矩形收敛。窗口隐藏期间引擎内部布局状态失效，
        // 重新打开首帧的返回矩形可能未经校正，且抖动可能持续多个事件趟；
        // 收敛（返回位置与请求一致）之前的位置差异一律视为引擎抖动，不写回。
        private bool _awaitingReturnedRectSettle;
        // 打开窗口的帧序号；该帧内的窗外 MouseDown 不触发自动隐藏。
        private int _shownOnFrame = int.MinValue;

        // 窗口布局持久化条目，由 InitializeWindowRect 注入；四项同时为 null 或同时非 null。
        private ConfigEntry<float> _windowXEntry;
        private ConfigEntry<float> _windowYEntry;
        private ConfigEntry<float> _windowWidthEntry;
        private ConfigEntry<float> _windowHeightEntry;
        // 选中用户持久化条目，由 InitializeSelectedUser 注入；为 null 时选中用户不持久化。
        private ConfigEntry<string> _selectedUserEntry;
        // 最近一次写入或从持久化恢复的矩形；保存时与之相同则跳过全部写入。
        private Rect _lastSavedWindowRect;
        // 窗口布局脏标记与停顿计时。位置或尺寸变化时置脏并重置计时，持续变化期间不写盘，
        // 停止变化超过 WindowLayoutSaveDelaySeconds 后由 Update 统一写一次。
        // 不能仅依赖鼠标事件判断交互结束：DragWindow/热控件消费过的释放事件在外层已不是 MouseUp。
        private bool _windowLayoutDirty;
        private float _windowLayoutDirtyDelay;
        private const float WindowLayoutSaveDelaySeconds = 0.5f;
        // 界面显隐热键的触发去抖门：防止引擎把一次物理按下重复报告为连续多帧边沿时窗口连开连关。
        private readonly HotkeyTriggerGate _uiHotkeyGate = new HotkeyTriggerGate();

        /// <summary>
        /// 按屏幕比例设置居中的窗口矩形。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">任一比例不在 (0, 1] 范围内。</exception>
        protected void SetCenteredWindowRect(float widthRatio, float heightRatio)
        {
            if (widthRatio <= 0f || widthRatio > 1f)
                throw new ArgumentOutOfRangeException(nameof(widthRatio));
            if (heightRatio <= 0f || heightRatio > 1f)
                throw new ArgumentOutOfRangeException(nameof(heightRatio));

            var width = UnityGui.ScreenWidth * widthRatio;
            var height = UnityGui.ScreenHeight * heightRatio;
            WindowRect = new Rect(
                (UnityGui.ScreenWidth - width) / 2f,
                (UnityGui.ScreenHeight - height) / 2f,
                width,
                height);
        }

        /// <summary>
        /// 按屏幕比例设置默认居中矩形，再从持久化配置项恢复窗口位置和尺寸。
        /// 其余行为与四参重载一致。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">任一比例不在 (0, 1] 范围内。</exception>
        protected void InitializeWindowRect(
            ConfigEntry<float> xEntry,
            ConfigEntry<float> yEntry,
            ConfigEntry<float> widthEntry,
            ConfigEntry<float> heightEntry,
            float widthRatio,
            float heightRatio)
        {
            SetCenteredWindowRect(widthRatio, heightRatio);
            InitializeWindowRect(xEntry, yEntry, widthEntry, heightEntry);
        }

        /// <summary>
        /// 从持久化配置项恢复窗口位置和尺寸，并订阅四个配置项的变化以响应配置重载等外部修改。
        /// 调用前 <see cref="WindowRect"/> 应已持有默认矩形；宽或高非正数时保持默认尺寸，
        /// 位置为负数时仅对未设置的方向采用屏幕居中，恢复结果始终钳制到最小尺寸与屏幕可见范围。
        /// 派生类在 Awake 中调用一次即可；重复调用会先解除旧订阅再重新绑定。
        /// 窗口位置和尺寸会在拉伸结束、隐藏和销毁时由基类统一写回这些配置项。
        /// </summary>
        /// <exception cref="ArgumentNullException">任一配置项为 <c>null</c>。</exception>
        protected void InitializeWindowRect(
            ConfigEntry<float> xEntry,
            ConfigEntry<float> yEntry,
            ConfigEntry<float> widthEntry,
            ConfigEntry<float> heightEntry)
        {
            UnsubscribeWindowRectEntries();
            _windowXEntry = xEntry ?? throw new ArgumentNullException(nameof(xEntry));
            _windowYEntry = yEntry ?? throw new ArgumentNullException(nameof(yEntry));
            _windowWidthEntry = widthEntry ?? throw new ArgumentNullException(nameof(widthEntry));
            _windowHeightEntry = heightEntry ?? throw new ArgumentNullException(nameof(heightEntry));
            xEntry.OnValueChangedBase += OnWindowRectEntryChanged;
            yEntry.OnValueChangedBase += OnWindowRectEntryChanged;
            widthEntry.OnValueChangedBase += OnWindowRectEntryChanged;
            heightEntry.OnValueChangedBase += OnWindowRectEntryChanged;

            ApplyPersistedWindowRect();
        }

        /// <summary>
        /// 从四个持久化条目的当前值重建窗口矩形；宽或高未持久化（非正数）时保持现状。
        /// 恢复成功会把保存基线更新为恢复结果。
        /// </summary>
        private void ApplyPersistedWindowRect()
        {
            if (WindowResizeHelper.TryBuildPersistedRect(
                    _windowXEntry.Value,
                    _windowYEntry.Value,
                    _windowWidthEntry.Value,
                    _windowHeightEntry.Value,
                    MinWindowSize,
                    new Vector2(UnityGui.ScreenWidth, UnityGui.ScreenHeight),
                    out var rect))
            {
                WindowRect = rect;
                _lastSavedWindowRect = rect;
            }
        }

        /// <summary>
        /// 注入选中用户持久化条目并从中恢复启动时选中的用户，同时订阅条目变化以响应配置重载等外部修改。
        /// 持久化键为空、无对应用户或模块上下文无效时保持默认用户，不写回修正；
        /// 有效性与 <see cref="ChangeCurrentContext"/> 一致，按 <see cref="IsContextValid(IUserContext)"/> 判定。
        /// 派生类应在 <see cref="GuiContextKey"/> 设置且现有用户的模块上下文注册完成后、
        /// 首次解析 <see cref="CurrentContext"/> 前调用一次；重复调用会先解除旧订阅再重新绑定。
        /// 之后每次选中用户变化都会由基类写回该条目。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        protected void InitializeSelectedUser(ConfigEntry<string> entry)
        {
            UnsubscribeSelectedUserEntry();
            _selectedUserEntry = entry ?? throw new ArgumentNullException(nameof(entry));
            entry.OnValueChangedBase += OnSelectedUserEntryChanged;

            var persistedKey = entry.Value;
            if (!string.IsNullOrEmpty(persistedKey) && IsContextValid(GetContext(persistedKey)))
                _selectedUserKey = persistedKey;
        }

        /// <summary>
        /// 把当前选中用户写回持久化条目；值未变化或未注入条目时为空操作。
        /// 写回触发的变化回调会因值与当前选择一致而直接返回，不会再次切换上下文。
        /// </summary>
        private void SaveSelectedUserKey()
        {
            if (_selectedUserEntry == null || _selectedUserEntry.Value == _selectedUserKey)
                return;

            _selectedUserEntry.Value = _selectedUserKey;
        }

        /// <summary>
        /// 响应选中用户条目的外部修改（如配置重载）：新键与当前选择相同（含本类自身的写回）时忽略；
        /// 新键能解析出有效模块上下文时切换到该用户，否则保持现状。
        /// </summary>
        private void OnSelectedUserEntryChanged(object sender, EventArgs args)
        {
            if (_selectedUserEntry == null)
                return;

            var key = _selectedUserEntry.Value;
            if (key == _selectedUserKey)
                return;

            if (!string.IsNullOrEmpty(key) && IsContextValid(GetContext(key)))
                ChangeCurrentContext(key);
        }

        /// <summary>
        /// 解除选中用户条目的变化订阅并清空引用；尚未初始化或已解绑时为空操作。
        /// </summary>
        private void UnsubscribeSelectedUserEntry()
        {
            if (_selectedUserEntry == null)
                return;

            _selectedUserEntry.OnValueChangedBase -= OnSelectedUserEntryChanged;
            _selectedUserEntry = null;
        }

        /// <summary>
        /// 获取运行时依赖、创建通用浮层编辑器、选择默认用户，并订阅用户移除通知。
        /// 本方法不初始化 <see cref="Users"/>、<see cref="GuiContextKey"/>、<see cref="UserEditor"/> 或
        /// <see cref="CurrentContext"/>，这些模块级状态由派生类在调用后完成。
        /// 派生类必须先设置 <see cref="StyleProvider"/>；初始化失败时会记录错误并销毁当前组件。
        /// </summary>
        protected virtual void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                UnityService = UnityProvider.Instance;

                ToastEditor = new ToastEditor(UnityService, UnityGui, StyleProvider);
                TooltipEditor = new TooltipEditor(UnityService, UnityGui, StyleProvider);

                _selectedUserKey = GetDefaultUserKey();
                UserManager.OnUserRemoved += OnUserRemoved;

                BLog.Debug($"GUI host created. Type='{GetType().FullName}', WindowId={WindowID}, InitialUser='{_selectedUserKey}'.");
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to create GUI host. Type='{GetType().FullName}', WindowId={WindowID}.", ex);
                Destroy(this);
            }
        }

        /// <summary>
        /// 在 Unity 更新阶段轮询界面热键、切换窗口显隐状态，并在窗口布局停止变化后写回持久化条目。
        /// 热键边沿经触发去抖门过滤：引擎把一次物理按下重复报告为连续多帧边沿时不会连开连关窗口。
        /// </summary>
        protected virtual void Update()
        {
            if (_uiHotkeyGate.ShouldTrigger(UIHotkey, () => UnityService.RealtimeSinceStartup))
            {
                BLog.Debug($"GUI visibility hotkey triggered. WindowId={WindowID}, Host='{GetType().Name}', WasVisible={IsVisible}.");
                ToggleVisibility();
            }

            if (_windowLayoutDirty)
            {
                _windowLayoutDirtyDelay -= UnityService.UnscaledDeltaTime;
                if (_windowLayoutDirtyDelay <= 0f)
                    SaveWindowLayout();
            }
        }

        /// <summary>
        /// 绘制可见窗口、记录拖动与拉伸结果，并在满足条件时处理点击窗外自动隐藏。
        /// 窗口刚打开时会等到 <see cref="EventType.Layout"/> 才开始调用 <c>GUI.Window</c>。
        /// </summary>
        protected virtual void OnGUI()
        {
            if (!IsVisible)
                return;

            var currentEvent = UnityService.EventCurrent;
            if (!ShouldDrawVisibleWindow(currentEvent?.type))
                return;

            var requested = WindowRect;
            var rect = GUI.Window(WindowID, requested, DrawWindow, Title);
            ApplyReturnedWindowRect(requested, rect);
            if (TryAutoHideOnFocusLost(currentEvent))
                GUI.FocusControl(null);
        }

        /// <summary>
        /// 判断当前 IMGUI 事件是否应绘制可见窗口。
        /// 刚打开时只接受 Layout，使第一次 <c>GUI.Window</c> 与布局测量成对；之后的事件照常绘制。
        /// </summary>
        /// <param name="eventType">当前 IMGUI 事件类型；没有事件时为 <c>null</c>。</param>
        /// <returns>应当调用 <c>GUI.Window</c> 时为 <c>true</c>。</returns>
        protected bool ShouldDrawVisibleWindow(EventType? eventType)
        {
            if (!IsVisible)
                return false;

            if (_hasLaidOutSinceShow)
                return true;

            if (eventType != EventType.Layout)
                return false;

            _hasLaidOutSinceShow = true;
            return true;
        }

        /// <summary>
        /// 把 <c>GUI.Window</c> 的返回矩形写回 <see cref="WindowRect"/>。
        /// 拉伸结果优先；窗口刚打开、返回矩形尚未收敛（返回位置与请求一致）时，
        /// 位置差异一律视为引擎首帧抖动而不写回——抖动可能持续同一帧的多个事件趟，不能只跳过一次。
        /// 收敛后的位置变化一律接受——拖动产生的位移不能按事件类型过滤，
        /// 真实引擎中 DragWindow 消费过的事件在外层已不是 <see cref="EventType.MouseDrag"/>。
        /// </summary>
        /// <param name="requested">传给 <c>GUI.Window</c> 的请求矩形。</param>
        /// <param name="returned"><c>GUI.Window</c> 的返回矩形。</param>
        protected void ApplyReturnedWindowRect(Rect requested, Rect returned)
        {
            if (_pendingWindowResizeRect.HasValue)
            {
                // 拉伸矩形在窗口回调内计算，GUI.Window 的返回值仍是拉伸前的矩形；这里直接采用计算结果。
                WindowRect = _pendingWindowResizeRect.Value;
                _pendingWindowResizeRect = null;
                MarkWindowLayoutDirty();
                return;
            }

            if (returned.position == requested.position)
            {
                _awaitingReturnedRectSettle = false;
                return;
            }

            if (_awaitingReturnedRectSettle)
                return;

            HasDraggedWindowSinceOpen = true;
            WindowRect = returned;
            MarkWindowLayoutDirty();
        }

        /// <summary>
        /// 标记窗口布局已变化并重置停顿计时；持续变化期间不写盘，由 <see cref="Update"/> 在停止变化后统一保存。
        /// </summary>
        private void MarkWindowLayoutDirty()
        {
            _windowLayoutDirty = true;
            _windowLayoutDirtyDelay = WindowLayoutSaveDelaySeconds;
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

            UnityGui.BeginArea(new Rect(
                WindowContentPadding,
                WindowTitleBarHeight,
                WindowRect.width - WindowContentPadding * 2f,
                WindowRect.height - WindowTitleBarHeight - WindowContentPadding));
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
            DrawWindowResizeGrip();
            HandleWindowResize();
            GUI.DragWindow();
        }

        /// <summary>
        /// 处理窗口边缘拉伸：按下时命中允许的边缘或角即以热控件捕获鼠标，拖动期间按锚点计算新矩形，
        /// 结果暂存到 <see cref="_pendingWindowResizeRect"/> 由 <see cref="OnGUI"/> 统一写回。
        /// 该方法依赖当前 IMGUI 事件，只应在 <see cref="DrawWindow"/> 调用链中先于 <see cref="GUI.DragWindow()"/> 执行；
        /// 热控件捕获保证拖动期间即使鼠标移出窗口，后续拖动与释放事件仍送达本窗口。
        /// </summary>
        private void HandleWindowResize()
        {
            var currentEvent = UnityService.EventCurrent;
            if (currentEvent == null)
                return;

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var eventType = currentEvent.GetTypeForControl(controlId);

            switch (eventType)
            {
                case EventType.MouseDown:
                {
                    if (currentEvent.button != 0)
                        return;

                    var edge = WindowResizeHelper.HitTest(WindowRect.size, currentEvent.mousePosition, AllowedResizeEdges);
                    if (edge == WindowResizeEdge.None)
                        return;

                    _isWindowResizing = true;
                    _resizeEdge = edge;
                    _resizeAnchors = WindowResizeHelper.GetGrabAnchors(WindowRect, GUIUtility.GUIToScreenPoint(currentEvent.mousePosition));
                    GUIUtility.hotControl = controlId;
                    HasDraggedWindowSinceOpen = true;
                    currentEvent.Use();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!_isWindowResizing || GUIUtility.hotControl != controlId)
                        return;

                    _pendingWindowResizeRect = WindowResizeHelper.Resize(
                        WindowRect,
                        _resizeEdge,
                        _resizeAnchors,
                        GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                        MinWindowSize,
                        new Vector2(UnityGui.ScreenWidth, UnityGui.ScreenHeight));
                    currentEvent.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!_isWindowResizing || GUIUtility.hotControl != controlId)
                        return;

                    _isWindowResizing = false;
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                    SaveWindowLayout();
                    break;
                }
            }
        }

        /// <summary>
        /// 在允许拉伸的窗口边缘绘制半透明把手；运行时 IMGUI 不支持自定义鼠标指针，以此作为主要的拉伸视觉提示。
        /// </summary>
        private void DrawWindowResizeGrip()
        {
            var allowed = AllowedResizeEdges;
            var allowsDiagonal = (allowed & WindowResizeEdge.Right) != 0 && (allowed & WindowResizeEdge.Bottom) != 0;
            var allowsVertical = (allowed & WindowResizeEdge.Top) != 0 || (allowed & WindowResizeEdge.Bottom) != 0;
            if (!allowsDiagonal && !allowsVertical)
                return;

            var gripColor = new Color(1f, 1f, 1f, 0.4f);
            if (allowsDiagonal)
            {
                // 右下角斜向排列的三个小方块，位于内容区边距内。
                for (var i = 0; i < 3; i++)
                {
                    var inset = 6f + i * 6f;
                    var rect = new Rect(WindowRect.width - inset - 4f, WindowRect.height - inset - 4f, 4f, 4f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, gripColor, 0f, 0f);
                }
            }
            else
            {
                // 底边中央的短横条，提示仅可纵向拉伸。
                for (var i = 0; i < 3; i++)
                {
                    var rect = new Rect(WindowRect.width / 2f - 10f, WindowRect.height - 9f + i * 3f, 20f, 2f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, gripColor, 0f, 0f);
                }
            }
        }

        /// <summary>
        /// 使用当前 IMGUI 事件判断是否应因点击窗外而隐藏。打开当帧不会自动隐藏。
        /// 隐藏成功时返回 <c>true</c>，调用方再清除 GUI 焦点。
        /// </summary>
        /// <param name="currentEvent">当前 IMGUI 事件。</param>
        protected bool TryAutoHideOnFocusLost(Event currentEvent)
        {
            return TryAutoHideOnFocusLost(currentEvent?.type, currentEvent?.mousePosition ?? Vector2.zero);
        }

        /// <summary>
        /// 判断失焦自动隐藏的纯判定核：事件不是 <see cref="EventType.MouseDown"/>（含没有事件）或
        /// 按下位置落在窗口内时不隐藏；打开当帧不隐藏。
        /// 事件类型与位置拆成独立参数是因为 <see cref="Event.type"/> 属于引擎内部调用，
        /// 引用了它的方法在无 Unity 运行时的单元测试进程中无法被 JIT。
        /// </summary>
        /// <param name="eventType">当前 IMGUI 事件类型；没有事件时为 <c>null</c>。</param>
        /// <param name="mousePosition">当前鼠标的窗口坐标。</param>
        protected bool TryAutoHideOnFocusLost(EventType? eventType, Vector2 mousePosition)
        {
            if (HasDraggedWindowSinceOpen)
                return false;

            if (UnityService.FrameCount == _shownOnFrame)
                return false;

            if (eventType != EventType.MouseDown)
                return false;

            if (WindowRect.Contains(mousePosition))
                return false;

            BLog.Debug($"GUI auto-hidden after losing focus. WindowId={WindowID}, Host='{GetType().Name}', User='{SelectedUserKey}'.");
            Hide();
            return true;
        }

        /// <summary>
        /// 隐藏窗口、写回窗口布局并重置本次打开期间的拖动状态；重复调用不会产生额外状态变化。
        /// </summary>
        public virtual void Hide()
        {
            if (!IsVisible)
                return;

            // 拉伸中途隐藏时释放热控件并丢弃未应用的计算结果，避免残留捕获状态影响下次打开。
            if (_isWindowResizing)
            {
                _isWindowResizing = false;
                _pendingWindowResizeRect = null;
                GUIUtility.hotControl = 0;
            }

            IsVisible = false;
            HasDraggedWindowSinceOpen = false;
            _hasLaidOutSinceShow = false;
            _awaitingReturnedRectSettle = false;
            SaveWindowLayout();

            BLog.Debug($"GUI hidden. WindowId={WindowID}, Host='{GetType().Name}', User='{SelectedUserKey}'.");
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
                _hasLaidOutSinceShow = false;
                _awaitingReturnedRectSettle = true;
                _shownOnFrame = UnityService.FrameCount;
                BLog.Debug($"GUI shown. WindowId={WindowID}, Host='{GetType().Name}', User='{SelectedUserKey}'.");
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
        /// 在 Unity 销毁宿主时写回窗口布局、解除用户移除订阅和布局及选中用户条目订阅，
        /// 避免进程级事件保留失效组件。
        /// </summary>
        /// <remarks>派生类覆盖此生命周期方法时必须调用基类实现。</remarks>
        protected virtual void OnDestroy()
        {
            UserManager.OnUserRemoved -= OnUserRemoved;
            SaveWindowLayout();
            UnsubscribeWindowRectEntries();
            UnsubscribeSelectedUserEntry();
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
        /// 最终选择键（含清空后的空字符串哨兵）会写回注入的持久化条目，未注入时跳过写回。
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
                SaveSelectedUserKey();
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
            SaveSelectedUserKey();

            if (nextContext != null)
                UserEditor?.SetStatusDirty(nextContext);
        }

        /// <summary>
        /// 响应布局条目的外部修改（如配置重载）：按四个条目的当前值重建窗口矩形。
        /// 本类自身的写回同样会触发本回调，但重建结果与刚写入的矩形一致，不产生可见变化。
        /// </summary>
        private void OnWindowRectEntryChanged(object sender, EventArgs args)
        {
            ApplyPersistedWindowRect();
        }

        /// <summary>
        /// 把当前窗口位置和尺寸写回持久化条目；与上次保存或恢复的结果一致时跳过全部写入。
        /// 写回前先把位置规范化到完全位于屏幕内，避免负坐标与位置哨兵（负数表示未持久化）冲突。
        /// 未调用 <see cref="InitializeWindowRect(ConfigEntry{float},ConfigEntry{float},ConfigEntry{float},ConfigEntry{float})"/> 时为空操作。
        /// </summary>
        private void SaveWindowLayout()
        {
            if (_windowWidthEntry == null)
                return;

            var normalized = WindowResizeHelper.NormalizeForPersist(WindowRect, new Vector2(UnityGui.ScreenWidth, UnityGui.ScreenHeight));
            if (normalized == _lastSavedWindowRect)
            {
                _windowLayoutDirty = false;
                return;
            }

            _lastSavedWindowRect = normalized;
            _windowXEntry.Value = normalized.x;
            _windowYEntry.Value = normalized.y;
            _windowWidthEntry.Value = normalized.width;
            _windowHeightEntry.Value = normalized.height;
            _windowLayoutDirty = false;
        }

        /// <summary>
        /// 解除四个布局条目的变化订阅并清空引用；尚未初始化或已解绑时为空操作。
        /// </summary>
        private void UnsubscribeWindowRectEntries()
        {
            if (_windowXEntry == null)
                return;

            _windowXEntry.OnValueChangedBase -= OnWindowRectEntryChanged;
            _windowYEntry.OnValueChangedBase -= OnWindowRectEntryChanged;
            _windowWidthEntry.OnValueChangedBase -= OnWindowRectEntryChanged;
            _windowHeightEntry.OnValueChangedBase -= OnWindowRectEntryChanged;
            _windowXEntry = null;
            _windowYEntry = null;
            _windowWidthEntry = null;
            _windowHeightEntry = null;
        }
    }
}
