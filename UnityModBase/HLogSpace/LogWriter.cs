using System;
using System.IO;
using System.Text;
using System.Threading;

namespace UnityModBase.HLogSpace
{
    /// <summary>
    /// 将日志条目追加到按创建小时命名的 UTF-8 文件，并延迟输出重复日志的汇总行。
    /// 该类型不拥有日志数据库；通常由 <see cref="HUserSpace.UserService"/> 订阅数据库事件后调用。
    /// </summary>
    /// <remarks>
    /// <see cref="Log"/>、<see cref="Flush"/> 和 <see cref="Dispose"/> 共享同一把锁，可与内部定时器串行；
    /// <see cref="Write"/> 本身不加锁，外部直接调用时不得与释放并发。
    /// </remarks>
    public class LogWriter : IDisposable
    {
        private static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan LongestDuration = TimeSpan.FromSeconds(5);

        // 定时器与调用线程都可能触发汇总刷新，这些字段必须在 _lock 下成组读取和修改。
        private Timer _timer;
        private LogEntry _lastLog;
        private StreamWriter _writer;
        private readonly object _lock = new object();

        /// <summary>
        /// 是否实际写入文件，默认启用。禁用不影响 <see cref="Log"/> 对等级和重复状态的跟踪。
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// 最低写入等级，默认 <see cref="LogLevel.Info"/>；低于该等级的条目不会成为重复跟踪基准。
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;

        /// <summary>
        /// 尝试创建日志目录及文件，并启动每 <c>1.5</c> 秒检查一次的重复汇总定时器。
        /// 文件名为“基础名-yyyy-MM-dd-HH.log”，实例存续期间不会跨小时切换文件。
        /// </summary>
        /// <param name="directory">日志目录。</param>
        /// <param name="fileName">文件基础名；目录和扩展名会被忽略。</param>
        /// <param name="level">最低写入等级。</param>
        /// <remarks>文件系统失败会被内部捕获，实例退化为不产生文件的写入器，不向调用方报告初始化结果。</remarks>
        public LogWriter(string directory, string fileName, LogLevel level)
        {
            try
            {
                Level = level;

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var fullPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}-{DateTime.Now:yyyy-MM-dd-HH}.log");
                var fs = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };

                _writer.WriteLine($"{new string('-', 50)}LOG-START-{DateTime.Now}{new string('-', 50)}");

                _timer = new Timer(s => Flush(), null, WriteInterval, WriteInterval);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 按等级接收日志并立即写入首次出现的条目。与当前跟踪条目等价时不会重复写入；
        /// 条目的重复计数由外部更新后，会在静默约 <c>1.5</c> 秒、持续约 <c>5</c> 秒、下一条不同日志到达或强制刷新时输出汇总。
        /// </summary>
        /// <param name="log">要处理的日志条目。</param>
        /// <exception cref="ArgumentNullException"><paramref name="log"/> 为 <c>null</c> 时抛出。</exception>
        public void Log(LogEntry log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            lock (_lock)
            {
                if (log.Level < Level)
                    return;

                if (log.Equals(_lastLog))
                    return;

                if (_lastLog != null && _lastLog.RepeatCount > 1)
                    Write(_lastLog);
                Write(log);

                _lastLog = log;
            }
        }

        /// <summary>
        /// 绕过等级与重复判断，直接把条目格式化后追加到文件；<see cref="Enable"/> 为 <c>false</c> 时不写入。
        /// 文件不可用或写入失败时静默忽略。
        /// </summary>
        /// <param name="log">要写入的日志条目。</param>
        /// <exception cref="ArgumentNullException"><paramref name="log"/> 为 <c>null</c> 时抛出。</exception>
        public void Write(LogEntry log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            if (!Enable)
                return;

            try { _writer?.WriteLine(log.ToString()); }
            catch { }
        }

        /// <summary>
        /// 在重复条目达到静默时间或累计持续时间阈值后写出汇总；强制模式忽略时间阈值。
        /// 没有待汇总的重复条目时为空操作。
        /// </summary>
        /// <param name="forced">是否立即写出当前重复汇总。</param>
        public void Flush(bool forced = false)
        {
            lock (_lock)
            {
                if (_lastLog?.IsRepeated != true)
                    return;

                if (forced ||
                    DateTime.Now - _lastLog.LastRepeatTime >= WriteInterval ||
                    _lastLog.LastRepeatTime - _lastLog.Timestamp >= LongestDuration)
                {
                    Write(_lastLog);
                    _lastLog = null;
                }
            }
        }

        /// <summary>
        /// 停止定时器，强制写出待处理的重复汇总，追加结束标记并关闭文件。
        /// 文件从未成功创建或关闭失败时静默返回。
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;

                if (_writer == null)
                    return;

                try
                {
                    Flush(forced: true);

                    _writer.WriteLine($"{new string('-', 50)}LOG-END-{DateTime.Now}{new string('-', 50)}");
                    _writer.WriteLine();
                    _writer.WriteLine();

                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                }
                catch
                {
                }
            }
        }
    }
}
