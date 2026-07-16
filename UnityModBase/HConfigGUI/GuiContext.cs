using System;
using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 保存单个用户在配置界面中的绑定树、编辑提交器、弹窗状态和布局缓存。
    /// 上下文挂载到 <see cref="UserContext"/>，不拥有底层配置生命周期；释放时仅解除提示通知订阅。
    /// </summary>
    public class GuiContext : IUserContext
    {
        // 键为配置分组键，值为该组所有配置项名称的最大显示宽度；语言或配置结构变化后必须标脏重算。
        private readonly Dictionary<string, float> _entryLabelWidth = new Dictionary<string, float>();
        private ToastEditor _toastEditor;
        private bool _toastNotificationsSubscribed = false;

        /// <summary>
        /// 表示无法使用的配置 GUI 上下文。该共享实例只用于状态判断，不应承载用户数据。
        /// </summary>
        public readonly static GuiContext InvalidGuiContext = new GuiContext();
        /// <summary>
        /// 指示当前实例是否不是共享的无效上下文哨兵。
        /// </summary>
        public bool IsValid => !ReferenceEquals(this, InvalidGuiContext);

        /// <summary>
        /// 获取当前用户独立的变更提交器。
        /// </summary>
        public EntryChangeSink ChangeSink { get; private set; }
        /// <summary>
        /// 获取或设置由用户配置表投影得到的根绑定。
        /// </summary>
        public GroupBinding UserData { get; set; }

        /// <summary>
        /// 获取当前上下文独立的模态弹窗状态。
        /// </summary>
        public PopupState Popup { get; private set; }
        /// <summary>
        /// 获取或设置当前展开的枚举配置键；空字符串表示没有展开项。
        /// </summary>
        public string ExpandedEnumKey { get; set; } = string.Empty;
        /// <summary>
        /// 获取或设置当前侧栏选中的配置分组键；无匹配项时绘制器回退到首组。
        /// </summary>
        public string SelectedGroupKey { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置侧栏分组按钮区域宽度；-1 表示尚未测量。
        /// </summary>
        public float GroupButtonWidth { get; set; } = -1f;
        /// <summary>
        /// 获取或设置重置按钮宽度；-1 表示尚未测量。
        /// </summary>
        public float ResetButtonWidth { get; set; } = -1f;

        /// <summary>
        /// 获取或设置是否需要重算各分组的配置项标签宽度。
        /// </summary>
        public bool IsEntryLabelWidthDirty { get; set; } = true;
        /// <summary>
        /// 获取或设置是否需要重算侧栏分组按钮宽度。
        /// </summary>
        public bool IsGroupButtonWidthDirty { get; set; } = true;
        /// <summary>
        /// 获取或设置是否需要重算重置按钮宽度。
        /// </summary>
        public bool IsResetButtonWidthDirty { get; set; } = true;

        /// <summary>
        /// 创建具有独立变更提交器、弹窗状态和布局缓存的上下文。
        /// </summary>
        public GuiContext()
        {
            ChangeSink = new EntryChangeSink();
            Popup = new PopupState();
        }

        /// <summary>
        /// 订阅当前上下文的配置变更事件，并将结果转发为短时提示。
        /// 重复调用会先解除旧编辑器订阅，确保通知只发送到最新实例。
        /// </summary>
        /// <param name="toastEditor">接收变更和重置提示的编辑器。</param>
        /// <exception cref="ArgumentNullException"><paramref name="toastEditor"/> 为 null。</exception>
        public void SubscribeToastNotifications(ToastEditor toastEditor)
        {
            if (_toastNotificationsSubscribed)
                UnsubscribeToastNotifications();

            _toastEditor = toastEditor ?? throw new ArgumentNullException(nameof(toastEditor), "ToastEditor cannot be null.");
            ChangeSink.OnEntryValueChanged += OnEntryValueChanged;
            ChangeSink.OnEntryValueReset += OnEntryValueReset;

            _toastNotificationsSubscribed = true;
        }

        /// <summary>
        /// 描述由具体值编辑器提供内容的模态弹窗。
        /// 回调在 IMGUI 绘制期间执行，不负责跨线程调度。
        /// </summary>
        public class PopupState
        {
            /// <summary>
            /// 指示弹窗是否接管配置窗口绘制。
            /// </summary>
            public bool IsOpen { get; set; } = false;
            /// <summary>
            /// 获取或设置弹窗标题的本地化文本。
            /// </summary>
            public Translator Title { get; set; }
            /// <summary>
            /// 获取或设置弹窗主体绘制回调。
            /// </summary>
            public Action DrawAction { get; set; }
            /// <summary>
            /// 获取或设置用户点击关闭按钮时执行的清理回调。
            /// </summary>
            public Action CloseAction { get; set; }
        }

        /// <summary>
        /// 获取指定分组缓存的配置项标签宽度；首次读取不存在的键时会写入并返回默认值。
        /// </summary>
        /// <param name="key">配置分组键。</param>
        /// <param name="defaultValue">尚无缓存时写入并返回的宽度。</param>
        /// <returns>缓存宽度或首次写入的默认宽度。</returns>
        public float GetEntryLabelWidth(string key, float defaultValue = 0f)
        {
            if (_entryLabelWidth.TryGetValue(key, out var value))
                return value;
            _entryLabelWidth[key] = defaultValue;
            return defaultValue;
        }

        /// <summary>
        /// 更新指定分组的配置项标签宽度缓存。
        /// </summary>
        /// <param name="key">非空白配置分组键。</param>
        /// <param name="value">要缓存的宽度。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 null、空字符串或仅包含空白。</exception>
        public void SetEntryLabelWidth(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            _entryLabelWidth[key] = value;
        }

        /// <summary>
        /// 分别标记三类派生布局尺寸是否需要在后续绘制前重新计算。
        /// </summary>
        /// <param name="entryLabelWidthDirty">是否重算配置项标签宽度。</param>
        /// <param name="groupButtonWidthDirty">是否重算侧栏分组按钮宽度。</param>
        /// <param name="resetButtonWidthDirty">是否重算重置按钮宽度。</param>
        public void SetLayoutDirtyFlags(bool entryLabelWidthDirty = true, bool groupButtonWidthDirty = true, bool resetButtonWidthDirty = true)
        {
            IsEntryLabelWidthDirty = entryLabelWidthDirty;
            IsGroupButtonWidthDirty = groupButtonWidthDirty;
            IsResetButtonWidthDirty = resetButtonWidthDirty;
        }

        /// <summary>
        /// 解除配置变更到提示消息的事件订阅；可重复调用。
        /// </summary>
        public void Dispose()
        {
            UnsubscribeToastNotifications();
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
