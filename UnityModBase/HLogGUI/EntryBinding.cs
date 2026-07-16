using UnityModBase.HLogSpace;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 将 <see cref="LogEntry"/> 的各字段格式化为日志表格可直接显示和排序的文本。
    /// 绑定保留底层日志引用，因此重复次数等可变字段会随日志数据库更新；相等性也沿用底层日志条目的定义。
    /// </summary>
    public class EntryBinding
    {
        /// <summary>
        /// 获取被适配的底层日志条目。
        /// </summary>
        public LogEntry Entry { get; }
        /// <summary>获取十进制日志 ID 文本。</summary>
        public string Id => Entry.Id.ToString();
        /// <summary>获取格式为 <c>HH:mm:ss.fff</c> 的首次记录时间。</summary>
        public string Timestamp => Entry.Timestamp.ToString("HH:mm:ss.fff");
        /// <summary>获取产生日志的托管线程 ID 文本。</summary>
        public string ThreadId => Entry.ThreadId.ToString();
        /// <summary>获取产生日志时的 Unity 帧编号文本。</summary>
        public string Frame => Entry.Frame.ToString();
        /// <summary>获取产生日志时的场景名称。</summary>
        public string Scene => Entry.Scene;
        /// <summary>获取日志等级名称。</summary>
        public string Level => Entry.Level.ToString();
        /// <summary>获取日志消息正文。</summary>
        public string Message => Entry.Message;
        /// <summary>获取调用方源文件路径。</summary>
        public string File => Entry.File;
        /// <summary>获取调用方源文件行号文本。</summary>
        public string Line => Entry.Line.ToString();
        /// <summary>获取调用方成员名称。</summary>
        public string Member => Entry.Member;
        /// <summary>获取完整异常文本；没有异常时返回空字符串。</summary>
        public string Exception => Entry.Exception?.ToString() ?? string.Empty;
        /// <summary>获取格式为 <c>HH:mm:ss.fff</c> 的最后重复时间。</summary>
        public string LastRepeatTime => Entry.LastRepeatTime.ToString("HH:mm:ss.fff");
        /// <summary>获取日志累计出现次数文本。</summary>
        public string RepeatCount => Entry.RepeatCount.ToString();
        /// <summary>获取日志是否已被数据库合并为重复条目。</summary>
        public bool IsRepeated => Entry.IsRepeated;

        /// <summary>
        /// 创建日志条目绑定。调用方应提供有效日志条目；本构造函数不执行 null 校验。
        /// </summary>
        /// <param name="entry">要适配的底层日志条目。</param>
        public EntryBinding(LogEntry entry)
        {
            Entry = entry;
        }

        /// <summary>
        /// 按底层日志条目的相等性规则比较两个绑定。
        /// </summary>
        /// <param name="other">另一个日志绑定。</param>
        /// <returns>底层日志条目相等时返回 true。</returns>
        public bool Equals(EntryBinding other)
        {
            if (other == null)
                return false;
            return Entry.Equals(other.Entry);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is EntryBinding other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Entry.GetHashCode();
        }

        /// <summary>
        /// 返回底层日志条目的完整格式化文本。
        /// </summary>
        /// <returns>可复制的完整日志文本。</returns>
        public override string ToString()
        {
            return Entry.ToString();
        }
    }
}
