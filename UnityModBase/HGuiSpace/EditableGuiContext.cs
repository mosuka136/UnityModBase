using System;
using System.Collections.Generic;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 保存单个用户的通用可编辑绑定树、提交器和布局缓存。
    /// </summary>
    public class EditableGuiContext : IUserContext
    {
        private readonly Dictionary<string, float> _entryLabelWidth = new Dictionary<string, float>();
        private ToastEditor _toastEditor;
        private bool _toastNotificationsSubscribed;

        /// <summary>获取上下文是否可以参与绘制和交互。</summary>
        public bool IsValid { get; }

        /// <summary>获取条目值变更提交器。</summary>
        public EntryChangeSink ChangeSink { get; }

        /// <summary>获取或设置当前用户的绑定根节点。</summary>
        public GroupBinding UserData { get; set; }

        /// <summary>获取或设置当前展开的枚举条目键。</summary>
        public string ExpandedEnumKey { get; set; } = string.Empty;

        /// <summary>获取或设置当前展开的集合条目键。</summary>
        public string ExpandedCollectionKey { get; set; } = string.Empty;

        /// <summary>获取或设置当前选中的分组键。</summary>
        public string SelectedGroupKey { get; set; } = string.Empty;

        /// <summary>获取或设置侧栏分组按钮宽度缓存。</summary>
        public float GroupButtonWidth { get; set; } = -1f;

        /// <summary>获取或设置条目行尾部操作宽度缓存。</summary>
        public float TrailingActionWidth { get; set; } = -1f;

        /// <summary>获取或设置条目标签宽度是否需要重新测量。</summary>
        public bool IsEntryLabelWidthDirty { get; set; } = true;

        /// <summary>获取或设置分组按钮宽度是否需要重新测量。</summary>
        public bool IsGroupButtonWidthDirty { get; set; } = true;

        /// <summary>获取或设置尾部操作宽度是否需要重新测量。</summary>
        public bool IsTrailingActionWidthDirty { get; set; } = true;

        /// <summary>创建有效的可编辑 GUI 上下文。</summary>
        public EditableGuiContext() : this(true)
        {
        }

        /// <summary>创建具有指定有效状态的可编辑 GUI 上下文。</summary>
        /// <param name="isValid">上下文是否有效。</param>
        protected EditableGuiContext(bool isValid)
        {
            IsValid = isValid;
            ChangeSink = new EntryChangeSink();
        }

        /// <summary>获取指定分组的条目标签宽度缓存。</summary>
        /// <param name="key">分组键。</param>
        /// <param name="defaultValue">缓存不存在时写入并返回的初始值。</param>
        /// <returns>条目标签宽度。</returns>
        public float GetEntryLabelWidth(string key, float defaultValue = 0f)
        {
            if (_entryLabelWidth.TryGetValue(key, out var value))
                return value;
            _entryLabelWidth[key] = defaultValue;
            return defaultValue;
        }

        /// <summary>设置指定分组的条目标签宽度缓存。</summary>
        /// <param name="key">分组键。</param>
        /// <param name="value">条目标签宽度。</param>
        public void SetEntryLabelWidth(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            _entryLabelWidth[key] = value;
        }

        /// <summary>设置通用布局测量的失效状态。</summary>
        public void SetLayoutDirtyFlags(
            bool entryLabelWidthDirty = true,
            bool groupButtonWidthDirty = true,
            bool trailingActionWidthDirty = true)
        {
            IsEntryLabelWidthDirty = entryLabelWidthDirty;
            IsGroupButtonWidthDirty = groupButtonWidthDirty;
            IsTrailingActionWidthDirty = trailingActionWidthDirty;
        }

        /// <summary>订阅条目提交和重置通知，并将其显示为 Toast。</summary>
        /// <param name="toastEditor">Toast 编辑器。</param>
        public void SubscribeToastNotifications(ToastEditor toastEditor)
        {
            if (_toastNotificationsSubscribed)
                UnsubscribeToastNotifications();

            _toastEditor = toastEditor ?? throw new ArgumentNullException(nameof(toastEditor), "ToastEditor cannot be null.");
            ChangeSink.OnEntryValueChanged += OnEntryValueChanged;
            ChangeSink.OnEntryValueReset += OnEntryValueReset;
            _toastNotificationsSubscribed = true;
        }

        /// <summary>提交有效的待处理输入并解除通知订阅。</summary>
        public virtual void Dispose()
        {
            try
            {
                ChangeSink.CommitPending();
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to commit pending GUI edits while disposing context. SelectedGroup='{SelectedGroupKey}', RootKey='{UserData?.Key ?? "<none>"}'.", ex);
            }
            finally
            {
                UnsubscribeToastNotifications();
            }
        }

        private void UnsubscribeToastNotifications()
        {
            if (!_toastNotificationsSubscribed)
                return;

            ChangeSink.OnEntryValueChanged -= OnEntryValueChanged;
            ChangeSink.OnEntryValueReset -= OnEntryValueReset;
            _toastEditor = null;
            _toastNotificationsSubscribed = false;
        }

        private void OnEntryValueChanged(IEntryBinding entry)
        {
            _toastEditor?.SetToast(TranslatorResource.Changed + entry.Name);
        }

        private void OnEntryValueReset(IEntryBinding entry)
        {
            _toastEditor?.SetToast(TranslatorResource.ResetDone + entry.Name);
        }
    }
}
