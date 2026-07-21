using System;
using System.Collections.Generic;
using System.Threading;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 保存单个用户的日志 GUI 数据、派生布局状态以及日志数据库和复制提示的事件订阅。
    /// 上下文不拥有日志数据库或编辑器；<see cref="Dispose"/> 仅解除订阅，必须在移除用户子上下文前调用。
    /// </summary>
    /// <remarks>
    /// 日志回调在写入数据库的线程同步执行，可能并非 Unity GUI 线程。分组集合内部会串行化写入，
    /// 列宽失效状态使用原子版本协调后台写入和 GUI 测量；其他编辑器状态仍应由 Unity GUI 线程维护。
    /// </remarks>
    public class GuiContext : IUserContext
    {
        private LogDatabase _logDatabase;
        private UserEditor _userEditor;
        private ToastEditor _toastEditor;
        private bool _logHandlersRegistered = false;
        private bool _toastNotificationsSubscribed = false;

        // 日志回调每次使内容版本递增；GUI 仅确认自己开始测量时捕获的版本，避免覆盖测量期间到达的更新。
        private int _columnWidthVersion = 1;
        private int _measuredColumnWidthVersion = 0;

        /// <summary>
        /// 获取或设置当前用户的日志分组绑定。注册日志处理器前必须赋值，注册过程会把数据库快照合并到该对象，
        /// 但不会清除其中已有的绑定。注册完成后不应替换该对象，否则后续事件会写入新对象，而已回填的快照仍留在旧对象中。
        /// </summary>
        public GroupBinding UserData { get; set; }

        /// <summary>
        /// 获取或设置是否需要按当前日志内容和等级重新测量列宽。
        /// 后台线程置位不会被正在进行的 GUI 测量覆盖；设置为 false 只确认设置时已经观察到的版本。
        /// </summary>
        public bool IsColumnWidthDirty
        {
            get => Volatile.Read(ref _columnWidthVersion) != Volatile.Read(ref _measuredColumnWidthVersion);
            set
            {
                if (value)
                    Interlocked.Increment(ref _columnWidthVersion);
                else
                    CompleteColumnWidthMeasurement(CaptureColumnWidthVersion());
            }
        }
        /// <summary>
        /// 获取或设置该上下文是否已经显示过重复日志相关列。
        /// 当前绘制流程把该状态作为界面可见性的单向闩锁，日志移除后不会自动复位。
        /// </summary>
        public bool HasRepeatedEntry { get; set; } = false;
        /// <summary>
        /// 获取或设置该上下文是否已经显示过异常列。
        /// 当前绘制流程把该状态作为界面可见性的单向闩锁，日志移除后不会自动复位。
        /// </summary>
        public bool HasExceptionEntry { get; set; } = false;
        /// <summary>
        /// 获取或设置可见日志列和顶部菜单所需的总宽度，单位为像素。
        /// </summary>
        public float TotalColumnWidth { get; set; } = 0f;

        /// <summary>
        /// 将当前上下文绑定到日志数据库和用户编辑器。
        /// 注册期间会在数据库变更暂停时加载一致快照，再接收后续新增、重复和移除事件。
        /// </summary>
        /// <param name="logDatabase">要监听的用户日志数据库。</param>
        /// <param name="userEditor">接收布局失效通知的日志用户编辑器。</param>
        /// <remarks>
        /// 新增与重复事件共用同一处理器：重复事件中的底层日志已由数据库原地更新，分组会保留原绑定并刷新排序缓存。
        /// 参数校验在修改现有订阅前完成，因此无效依赖不会破坏当前注册状态。
        /// 重复调用会先从旧数据库和编辑器退订，再绑定新依赖；已有复制提示订阅会迁移到新编辑器。
        /// 重新注册不会清空 <see cref="UserData"/> 中的既有绑定，因此该对象会合并保留旧数据库条目与新数据库快照。
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
        /// <exception cref="InvalidOperationException"><see cref="UserData"/> 尚未赋值。</exception>
        public void RegisterLogHandlers(LogDatabase logDatabase, UserEditor userEditor)
        {
            if (UserData == null)
                throw new InvalidOperationException("UserData must be assigned before registering log handlers.");
            if (logDatabase == null)
                throw new ArgumentNullException(nameof(logDatabase));
            if (userEditor == null)
                throw new ArgumentNullException(nameof(userEditor));

            var toastEditor = _toastNotificationsSubscribed ? _toastEditor : null;

            if (_toastNotificationsSubscribed)
                UnsubscribeToastNotifications();

            if (_logHandlersRegistered)
                UnregisterLogHandlers();

            _logDatabase = logDatabase;
            _userEditor = userEditor;

            _logDatabase.SubscribeWithSnapshot(InitializeLogBindings, OnLogChanged, OnLogRemoved, OnLogChanged);

            _logHandlersRegistered = true;

            if (toastEditor != null)
                SubscribeToastNotifications(toastEditor);
        }

        /// <summary>
        /// 将整条日志或字段复制事件转发为短时提示。
        /// 必须先调用 <see cref="RegisterLogHandlers"/> 提供用户编辑器；重复订阅会替换旧提示编辑器。
        /// </summary>
        /// <param name="toastEditor">接收复制提示的短时消息编辑器。</param>
        /// <exception cref="InvalidOperationException">尚未注册用户编辑器。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="toastEditor"/> 为 null。</exception>
        public void SubscribeToastNotifications(ToastEditor toastEditor)
        {
            if (_toastNotificationsSubscribed)
                UnsubscribeToastNotifications();

            if (_userEditor == null)
                throw new InvalidOperationException("UserEditor must be registered before subscribing toast notifications.");

            _toastEditor = toastEditor ?? throw new ArgumentNullException(nameof(toastEditor), "ToastEditor cannot be null.");
            _userEditor.GroupEditor.OnLogCopied += OnLogCopied;
            _userEditor.GroupEditor.EntryEditor.OnEntryCopied += OnEntryCopied;

            _toastNotificationsSubscribed = true;
        }

        /// <summary>
        /// 解除日志数据库和复制提示的全部事件订阅；可重复调用。
        /// </summary>
        public void Dispose()
        {
            UnsubscribeToastNotifications();
            UnregisterLogHandlers();
        }

        private void UnregisterLogHandlers()
        {
            if (!_logHandlersRegistered)
                return;

            _logDatabase.Unsubscribe(OnLogChanged, OnLogRemoved, OnLogChanged);
            _logDatabase = null;
            _userEditor = null;

            _logHandlersRegistered = false;
        }

        /// <summary>
        /// 单独解除复制提示订阅；不会停止日志数据库继续更新绑定数据。
        /// </summary>
        public void UnsubscribeToastNotifications()
        {
            if (!_toastNotificationsSubscribed)
                return;

            _userEditor.GroupEditor.OnLogCopied -= OnLogCopied;
            _userEditor.GroupEditor.EntryEditor.OnEntryCopied -= OnEntryCopied;
            _toastEditor = null;

            _toastNotificationsSubscribed = false;
        }

        private void OnLogChanged(LogEntry log)
        {
            // 新增事件会追加绑定；重复事件携带数据库已原地更新的主条目，等价绑定仅用于刷新排序快照。
            UserData.AddEntry(new EntryBinding(log));
            _userEditor.SetStatusDirty(this);
        }

        private void OnLogRemoved(LogEntry log)
        {
            // 分组按底层日志的合并等价规则查找，临时绑定仅作为删除键，不会加入集合。
            UserData.RemoveEntry(new EntryBinding(log));
            _userEditor.SetStatusDirty(this);
        }

        /// <summary>
        /// 捕获本轮列宽测量要处理的日志内容版本。
        /// </summary>
        /// <returns>调用时观察到的列宽失效版本。</returns>
        internal int CaptureColumnWidthVersion()
        {
            return Volatile.Read(ref _columnWidthVersion);
        }

        /// <summary>
        /// 标记指定版本的列宽测量完成；捕获后到达的更新仍保持脏状态。
        /// </summary>
        /// <param name="measuredVersion">开始测量前由 <see cref="CaptureColumnWidthVersion"/> 捕获的版本。</param>
        internal void CompleteColumnWidthMeasurement(int measuredVersion)
        {
            Volatile.Write(ref _measuredColumnWidthVersion, measuredVersion);
        }

        private void InitializeLogBindings(IReadOnlyList<LogEntry> logs)
        {
            // 回调在数据库阻止后续提交的期间执行；先完成整批回填，后续事件才会开始更新同一分组。
            foreach (var log in logs)
                UserData.AddEntry(new EntryBinding(log));

            if (logs.Count > 0)
                _userEditor.SetStatusDirty(this);
        }

        private void OnLogCopied(string message)
        {
            _toastEditor?.SetToast(message);
        }

        private void OnEntryCopied(string message)
        {
            _toastEditor?.SetToast(message);
        }
    }
}
