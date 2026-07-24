using System;
using System.IO;
using System.Text;

namespace UnityModBase.HLogSpace
{
    /// <summary>
    /// 表示一次日志事件及其调用位置快照。初始事件信息创建后不可变，重复时间和次数由日志数据库原地更新。
    /// </summary>
    /// <remarks>重复信息是可变状态且没有内部同步；并发更新时调用方需自行协调。</remarks>
    public class LogEntry
    {
        /// <summary>
        /// 数据库分配的进程内序号；直接构造条目时由调用方定义。
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 首次记录该事件的本地时间。
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 首次记录事件时的托管线程标识。
        /// </summary>
        public int ThreadId { get; }

        /// <summary>
        /// 首次记录事件时的 Unity 帧号；没有 Unity 状态提供器时为 <c>0</c>。
        /// </summary>
        public int Frame { get; }

        /// <summary>
        /// 首次记录事件时的场景名；传入 <c>null</c> 时为 <c>?</c>。
        /// </summary>
        public string Scene { get; }

        /// <summary>
        /// 事件严重等级。
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// 日志文本；构造参数为 <c>null</c> 时为空字符串。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 调用源文件路径；未知时为空字符串。
        /// </summary>
        public string File { get; }

        /// <summary>
        /// 调用源文件行号；<c>0</c> 表示未知。
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 调用成员名；未知时为空字符串。
        /// </summary>
        public string Member { get; }

        /// <summary>
        /// 与事件关联的异常，可为 <c>null</c>。
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 最近一次等价事件的时间；新建条目时等于 <see cref="Timestamp"/>。
        /// </summary>
        public DateTime LastRepeatTime { get; set; }

        /// <summary>
        /// 合并后的事件总次数；新建条目时为 <c>1</c>。
        /// </summary>
        public int RepeatCount { get; set; }

        /// <summary>
        /// <see cref="RepeatCount"/> 是否大于 <c>1</c>。
        /// </summary>
        public bool IsRepeated => RepeatCount > 1;

        /// <summary>
        /// 创建日志快照，并把可选字符串中的 <c>null</c> 规范化为稳定的显示值。
        /// </summary>
        /// <param name="id">进程内日志序号。</param>
        /// <param name="timestamp">首次事件时间。</param>
        /// <param name="threadId">托管线程标识。</param>
        /// <param name="frame">Unity 帧号。</param>
        /// <param name="scene">场景名；<c>null</c> 转换为 <c>?</c>。</param>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志文本；<c>null</c> 转换为空字符串。</param>
        /// <param name="file">调用源文件路径；<c>null</c> 转换为空字符串。</param>
        /// <param name="line">调用源文件行号。</param>
        /// <param name="member">调用成员名；<c>null</c> 转换为空字符串。</param>
        /// <param name="exception">关联异常，可为 <c>null</c>。</param>
        public LogEntry(int id, DateTime timestamp, int threadId, int frame, string scene, LogLevel level, string message, string file, int line, string member, Exception exception)
        {
            Id = id;
            Timestamp = timestamp;
            ThreadId = threadId;
            Frame = frame;
            Scene = scene ?? "?";
            Level = level;
            Message = message ?? string.Empty;
            File = file ?? string.Empty;
            Line = line;
            Member = member ?? string.Empty;
            Exception = exception;

            LastRepeatTime = timestamp;
            RepeatCount = 1;
        }

        /// <summary>
        /// 把最近重复时间替换为指定值，并将重复次数增加一次。
        /// </summary>
        /// <param name="newRepeatTime">最近一次等价事件的时间。</param>
        public void UpdateRepeat(DateTime newRepeatTime)
        {
            LastRepeatTime = newRepeatTime;
            RepeatCount++;
        }

        /// <summary>
        /// 判断两条日志是否可合并。比较线程、场景、等级、消息、调用位置及异常完整文本；
        /// 不比较序号、首次时间、帧号和重复状态。
        /// </summary>
        /// <param name="other">候选日志，可为 <c>null</c>。</param>
        /// <returns>两条日志满足合并条件时为 <c>true</c>。</returns>
        public bool Equals(LogEntry other)
        {
            if (other == null)
                return false;

            return ThreadId == other.ThreadId &&
                   Scene == other.Scene &&
                   Level == other.Level &&
                   Message == other.Message &&
                   File == other.File &&
                   Line == other.Line &&
                   Member == other.Member &&
                   Exception?.ToString() == other.Exception?.ToString();
        }

        /// <summary>
        /// 判断对象是否为满足日志合并条件的 <see cref="LogEntry"/>。
        /// </summary>
        /// <param name="obj">候选对象。</param>
        /// <returns>对象是等价日志条目时为 <c>true</c>。</returns>
        public override bool Equals(object obj)
        {
            return obj is LogEntry other && Equals(other);
        }

        /// <summary>
        /// 根据参与日志等价判断的字段生成哈希码。
        /// </summary>
        /// <returns>当前条目的哈希码。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ThreadId.GetHashCode();
                hash = hash * 31 + (Scene?.GetHashCode() ?? 0);
                hash = hash * 31 + Level.GetHashCode();
                hash = hash * 31 + (Message?.GetHashCode() ?? 0);
                hash = hash * 31 + (File?.GetHashCode() ?? 0);
                hash = hash * 31 + Line.GetHashCode();
                hash = hash * 31 + (Member?.GetHashCode() ?? 0);
                hash = hash * 31 + (Exception?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// 格式化为单条日志文本。调用位置仅在成员名、文件名和正行号全部存在时追加；异常另起一行输出。
        /// </summary>
        /// <returns>可直接写入日志文件的文本。</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(256);

            sb.Append('[').Append(Id).Append("] ")
              .Append(Timestamp.ToString("HH:mm:ss.fff"));

            if (IsRepeated)
                sb.Append('-').Append(LastRepeatTime.ToString("HH:mm:ss.fff"));

            sb.Append(" T").Append(ThreadId)
              .Append(" F").Append(Frame)
              .Append(" S=").Append(Scene)
              .Append(" ").Append(Level.ToString());

            if (IsRepeated)
                sb.Append(" x").Append(RepeatCount);

            sb.Append(" | ").Append(Message);

            if (!string.IsNullOrEmpty(Member) && !string.IsNullOrEmpty(Path.GetFileName(File)) && Line > 0)
            {
                sb.Append(" (").Append(Path.GetFileName(File))
                  .Append(':').Append(Line)
                  .Append(" ").Append(Member).Append(')');
            }

            if (Exception != null)
            {
                sb.AppendLine();
                sb.Append(Exception);
            }

            return sb.ToString();
        }
    }
}
