using System;
using System.Linq;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 保存单个用户的日志 GUI 数据、派生布局状态以及日志数据库和复制提示的事件订阅。
    /// 上下文不拥有日志数据库或编辑器；<see cref="Dispose"/> 仅解除订阅，必须在移除用户子上下文前调用。
    /// </summary>
    public class GuiContext : IUserContext
    {
        private LogDatabase _logDatabase;
        private UserEditor _userEditor;
        private ToastEditor _toastEditor;
        private bool _logHandlersRegistered = false;
        private bool _toastNotificationsSubscribed = false;

        /// <summary>
        /// 获取或设置当前用户的日志分组绑定。
        /// </summary>
        public GroupBinding UserData { get; set; }

        /// <summary>
        /// 获取或设置是否需要按当前日志内容和等级重新测量列宽。
        /// </summary>
        public bool IsColumnWidthDirty { get; set; } = true;
        /// <summary>
        /// 获取或设置该上下文是否已启用重复日志相关列。
        /// </summary>
        public bool HasRepeatedEntry { get; set; } = false;
        /// <summary>
        /// 获取或设置该上下文是否已启用异常列。
        /// </summary>
        public bool HasExceptionEntry { get; set; } = false;
        /// <summary>
        /// 获取或设置可见日志列和顶部菜单所需的总宽度，单位为像素。
        /// </summary>
        public float TotalColumnWidth { get; set; } = 0f;

        /// <summary>
        /// 将当前上下文绑定到日志数据库和用户编辑器。
        /// 重复注册会先从旧数据库解除处理器，后续新增、重复和移除事件只更新最新绑定的数据。
        /// </summary>
        /// <param name="logDatabase">要监听的用户日志数据库。</param>
        /// <param name="userEditor">接收布局失效通知的日志用户编辑器。</param>
        /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
        public void RegisterLogHandlers(LogDatabase logDatabase, UserEditor userEditor)
        {
            if (_logHandlersRegistered)
                UnregisterLogHandlers();

            _logDatabase = logDatabase ?? throw new ArgumentNullException(nameof(logDatabase), "LogDatabase cannot be null.");
            _userEditor = userEditor ?? throw new ArgumentNullException(nameof(userEditor), "UserEditor cannot be null.");

            _logDatabase.OnLogAdded += OnLogChanged;
            _logDatabase.OnLogRepeated += OnLogChanged;
            _logDatabase.OnLogRemoved += OnLogRemoved;

            _logHandlersRegistered = true;
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

            _logDatabase.OnLogAdded -= OnLogChanged;
            _logDatabase.OnLogRepeated -= OnLogChanged;
            _logDatabase.OnLogRemoved -= OnLogRemoved;
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
            UserData.AddEntry(new EntryBinding(log));
            _userEditor.SetStatusDirty(this);
        }

        private void OnLogRemoved(LogEntry log)
        {
            var entryBinding = UserData.OriginalGroup.FirstOrDefault(e => e.Entry.Equals(log));
            if (entryBinding != null)
            {
                UserData.RemoveEntry(entryBinding);
                _userEditor.SetStatusDirty(this);
            }
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
