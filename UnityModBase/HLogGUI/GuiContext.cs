using System;
using System.Linq;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    public class GuiContext : IUserContext
    {
        private LogDatabase _logDatabase;
        private UserEditor _userEditor;
        private ToastEditor _toastEditor;
        private bool _logHandlersRegistered = false;
        private bool _toastNotificationsSubscribed = false;

        public GroupBinding UserData { get; set; }

        public bool IsColumnWidthDirty { get; set; } = true;
        public bool HasRepeatedEntry { get; set; } = false;
        public bool HasExceptionEntry { get; set; } = false;
        public float TotalColumnWidth { get; set; } = 0f;

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
