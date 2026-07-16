using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HLogGUI.Resource
{
    /// <summary>
    /// 日志 GUI 的固定文案资源。
    /// 文案以 <see cref="Translator"/> 保存，实际显示语言由运行时全局语言设置决定。
    /// </summary>
    public static class TranslatorResource
    {
        /// <summary>日志窗口标题。</summary>
        public static readonly Translator Title = new Translator("日志", "Log");
        /// <summary>日志 ID 列标题。</summary>
        public static readonly Translator IdTopBar = new Translator("ID", "ID");
        /// <summary>首次记录时间列标题。</summary>
        public static readonly Translator TimestampTopBar = new Translator("时间戳", "Timestamp");
        /// <summary>线程 ID 列标题。</summary>
        public static readonly Translator ThreadIdTopBar = new Translator("线程ID", "Thread ID");
        /// <summary>Unity 帧编号列标题。</summary>
        public static readonly Translator FrameTopBar = new Translator("帧数", "Frame");
        /// <summary>场景名称列标题。</summary>
        public static readonly Translator SceneTopBar = new Translator("场景", "Scene");
        /// <summary>日志等级列标题。</summary>
        public static readonly Translator LevelTopBar = new Translator("等级", "Level");
        /// <summary>日志消息列标题。</summary>
        public static readonly Translator MessageTopBar = new Translator("消息", "Message");
        /// <summary>源文件列标题。</summary>
        public static readonly Translator FileTopBar = new Translator("文件", "File");
        /// <summary>源文件行号列标题。</summary>
        public static readonly Translator LineTopBar = new Translator("行号", "Line");
        /// <summary>调用方成员列标题。</summary>
        public static readonly Translator MemberTopBar = new Translator("成员", "Member");
        /// <summary>异常列标题。</summary>
        public static readonly Translator ExceptionTopBar = new Translator("异常", "Exception");
        /// <summary>最后重复时间列标题。</summary>
        public static readonly Translator LastRepeatTimeTopBar = new Translator("最后重复时间", "Last Repeat Time");
        /// <summary>累计出现次数列标题。</summary>
        public static readonly Translator RepeatCountTopBar = new Translator("重复次数", "Repeat Count");
        /// <summary>逐行操作列标题。</summary>
        public static readonly Translator MiscMenu = new Translator("杂项", "Misc");
        /// <summary>复制整条日志的按钮文本。</summary>
        public static readonly Translator CopyLog = new Translator("复制日志", "Copy Log");
        /// <summary>复制成功提示前缀。</summary>
        public static readonly Translator Copied = new Translator("已复制：", "Copied:");
    }
}
