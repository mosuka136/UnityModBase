using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Resource
{
    /// <summary>
    /// 配置 GUI 的固定文案资源。
    /// 文案以 <see cref="Translator"/> 保存，实际显示语言由运行时全局语言设置决定。
    /// </summary>
    public static class TranslatorResource
    {
        /// <summary>配置窗口标题。</summary>
        public static readonly Translator Title = new Translator("控制面板", "Control Panel");
        /// <summary>布尔配置开启状态文本。</summary>
        public static readonly Translator On = new Translator("开启", "On");
        /// <summary>布尔配置关闭状态文本。</summary>
        public static readonly Translator Off = new Translator("关闭", "Off");
        /// <summary>恢复配置默认值的按钮文本。</summary>
        public static readonly Translator Reset = new Translator("重置", "Reset");
        /// <summary>配置值修改成功提示前缀。</summary>
        public static readonly Translator Changed = new Translator("已修改：", "Changed: ");
        /// <summary>配置值重置成功提示前缀。</summary>
        public static readonly Translator ResetDone = new Translator("已重置：", "Reset: ");
        /// <summary>热键录制模态窗口标题。</summary>
        public static readonly Translator RecordHotkeyPopupTitle = new Translator("录制热键中", "Recording Hotkey");
        /// <summary>开始录制组合键的按钮文本。</summary>
        public static readonly Translator Record = new Translator("录制", "Record");
        /// <summary>确认录制结果的按钮文本。</summary>
        public static readonly Translator Apply = new Translator("应用", "Apply");
        /// <summary>取消操作的按钮文本。</summary>
        public static readonly Translator Cancel = new Translator("取消", "Cancel");
        /// <summary>新增热键组合的按钮文本。</summary>
        public static readonly Translator Add = new Translator("增加", "Add");
        /// <summary>移除热键组合的按钮文本。</summary>
        public static readonly Translator Remove = new Translator("移除", "Remove");
        /// <summary>关闭模态窗口的按钮文本。</summary>
        public static readonly Translator Close = new Translator("关闭", "Close");
        /// <summary>滑条配置缺少有效元数据时的错误文本。</summary>
        public static readonly Translator InvalidSliderMetadata = new Translator("无效的滑动条元数据", "Invalid slider metadata");
    }
}
