using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityModBase.HProvider;

namespace UnityModBase.HLogSpace
{
    /// <summary>
    /// 维护进程内的有界日志集合，为新日志分配序号，并把内容等价的条目合并为重复计数。
    /// 本类只管理内存状态及变更通知；文件持久化由订阅数据库事件的 <see cref="LogWriter"/> 负责。
    /// </summary>
    /// <remarks>
    /// 条目查找与合并、容量淘汰、事件通知和队列清理由同一实例锁串行化；序号则通过原子递增分配。
    /// 事件处理器在持有该锁时同步执行，不应长时间阻塞，也不应等待需要写入同一数据库的其他线程。
    /// <see cref="Dispose"/> 只复位状态而不终止实例；生命周期所有者仍应避免将其与详细参数重载的日志写入并发调用。
    /// </remarks>
    public class LogDatabase : IDisposable
    {
        /// <summary>
        /// 新增非重复日志后保留的条目数上限；重复日志只累加计数，不占用新条目。
        /// </summary>
        public const int MaxLogCount = 500;

        // 保护重复项合并、容量淘汰、事件顺序和释放清理，避免并发追加等价日志时生成多个主条目。
        private readonly object _lock = new object();
        private int _seq = 0;
        private readonly ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();

        /// <summary>
        /// 当前队列结构的只读快照。后续增删不会改变已取得的列表，但其中的 <see cref="LogEntry"/> 仍可能更新重复信息。
        /// </summary>
        public IReadOnlyList<LogEntry> Logs => _logs.ToArray();

        /// <summary>
        /// 详细参数重载最近分配的日志序号；初始值及释放复位值为 <c>0</c>。
        /// 直接传入 <see cref="LogEntry"/> 不会更新该值。
        /// </summary>
        public int Seq => _seq;

        /// <summary>
        /// 用于采集 Unity 帧号和场景的服务；可为 <c>null</c>，此时分别回退为 <c>0</c> 和 <c>?</c>。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 新的非重复日志入队且完成容量淘汰后同步触发。
        /// </summary>
        public event Action<LogEntry> OnLogAdded;

        /// <summary>
        /// 因容量限制或释放而移除日志时同步触发。
        /// </summary>
        public event Action<LogEntry> OnLogRemoved;

        /// <summary>
        /// 内容等价的日志合并到已有条目后同步触发，参数为已更新的首个匹配条目。
        /// </summary>
        public event Action<LogEntry> OnLogRepeated;

        /// <summary>
        /// 创建空的内存日志库。
        /// </summary>
        /// <param name="unityService">Unity 状态提供器；可为 <c>null</c> 以使用帧号和场景回退值。</param>
        public LogDatabase(UnityProvider unityService)
        {
            UnityService = unityService;
        }

        /// <summary>
        /// 添加已有日志条目。等价条目会合并到队列中首个匹配项，并累加重复次数；
        /// 新条目导致超容量时，会先移除最早条目，再通知新增事件。
        /// </summary>
        /// <param name="log">要添加或合并的日志条目，不能为 <c>null</c>。</param>
        /// <remarks>
        /// 查找、合并、入队、容量淘汰和相关事件通知在同一临界区内完成。
        /// 单个事件订阅者抛出的异常会被忽略，后续订阅者仍会执行。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="log"/> 为 <c>null</c>。</exception>
        public void AddLog(LogEntry log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            lock (_lock)
            {
                var repeatLog = _logs.Where(l => l.Equals(log));
                if (repeatLog.Any())
                {
                    var mainLog = repeatLog.First();

                    foreach (var l in repeatLog.Skip(1))
                        mainLog.RepeatCount += l.RepeatCount;

                    mainLog.LastRepeatTime = log.Timestamp;
                    mainLog.RepeatCount += log.RepeatCount;

                    foreach (var handler in OnLogRepeated.GetInvocationListOrEmpty())
                    {
                        try { handler?.Invoke(mainLog); }
                        catch { }
                    }

                    return;
                }

                _logs.Enqueue(log);

                if (_logs.Count > MaxLogCount)
                {
                    _logs.TryDequeue(out var removedLog);
                    foreach (var handler in OnLogRemoved.GetInvocationListOrEmpty())
                    {
                        try { handler?.Invoke(removedLog); }
                        catch { }
                    }
                }

                foreach (var handler in OnLogAdded.GetInvocationListOrEmpty())
                {
                    try { handler?.Invoke(log); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 使用当前时间、托管线程、Unity 帧和场景构造日志后添加，并以原子递增值分配条目序号。
        /// </summary>
        /// <param name="logLevel">日志等级。</param>
        /// <param name="msg">日志文本；<c>null</c> 会由条目规范化为空字符串。</param>
        /// <param name="ex">关联异常，可为 <c>null</c>。</param>
        /// <param name="member">调用成员名；<c>null</c> 会规范化为空字符串。</param>
        /// <param name="file">调用源文件路径；<c>null</c> 会规范化为空字符串。</param>
        /// <param name="line">调用源文件行号；<c>0</c> 表示未提供。</param>
        /// <remarks>
        /// 序号在进入 <see cref="AddLog(LogEntry)"/> 的写入临界区前分配；
        /// 请勿与 <see cref="Dispose"/> 并发调用，否则复位前构造的条目可能在复位后入队，且序号可能被重新使用。
        /// </remarks>
        public void AddLog(LogLevel logLevel, string msg, Exception ex, string member, string file, int line)
        {
            var id = Interlocked.Increment(ref _seq);
            DateTime timestamp = DateTime.Now;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            int frame = UnityService?.FrameCount ?? 0;
            string scene = UnityService?.ActiveScene.name;
            scene = string.IsNullOrEmpty(scene) ? "?" : scene;

            AddLog(new LogEntry(id, timestamp, threadId, frame, scene, logLevel, msg, file, line, member, ex));
        }

        /// <summary>
        /// 添加不带异常的调试日志。
        /// </summary>
        /// <param name="msg">日志文本。</param>
        /// <param name="member">调用成员名。</param>
        /// <param name="file">调用源文件路径。</param>
        /// <param name="line">调用源文件行号。</param>
        public void Debug(string msg, string member, string file, int line) => AddLog(LogLevel.Debug, msg, null, member, file, line);

        /// <summary>
        /// 添加不带异常的常规信息日志。
        /// </summary>
        /// <param name="msg">日志文本。</param>
        /// <param name="member">调用成员名。</param>
        /// <param name="file">调用源文件路径。</param>
        /// <param name="line">调用源文件行号。</param>
        public void Info(string msg, string member, string file, int line) => AddLog(LogLevel.Info, msg, null, member, file, line);

        /// <summary>
        /// 添加不带异常的通知日志。
        /// </summary>
        /// <param name="msg">日志文本。</param>
        /// <param name="member">调用成员名。</param>
        /// <param name="file">调用源文件路径。</param>
        /// <param name="line">调用源文件行号。</param>
        public void Notice(string msg, string member, string file, int line) => AddLog(LogLevel.Notice, msg, null, member, file, line);

        /// <summary>
        /// 添加不带异常的警告日志。
        /// </summary>
        /// <param name="msg">日志文本。</param>
        /// <param name="member">调用成员名。</param>
        /// <param name="file">调用源文件路径。</param>
        /// <param name="line">调用源文件行号。</param>
        public void Warn(string msg, string member, string file, int line) => AddLog(LogLevel.Warning, msg, null, member, file, line);

        /// <summary>
        /// 添加可关联异常的错误日志。
        /// </summary>
        /// <param name="msg">日志文本。</param>
        /// <param name="ex">关联异常，可为 <c>null</c>。</param>
        /// <param name="member">调用成员名。</param>
        /// <param name="file">调用源文件路径。</param>
        /// <param name="line">调用源文件行号。</param>
        public void Error(string msg, Exception ex, string member, string file, int line) => AddLog(LogLevel.Error, msg, ex, member, file, line);

        /// <summary>
        /// 在写入临界区内清空队列、把序号复位为 <c>0</c> 并移除全部事件订阅者。
        /// 每个已存条目均会在出队后通知移除事件，订阅者异常不会中断后续清理。
        /// </summary>
        /// <remarks>
        /// 此方法不会把实例标记为终止状态，释放后仍可重新添加日志。
        /// 队列清理与 <see cref="AddLog(LogEntry)"/> 串行执行，但详细参数重载会在取得该锁前分配序号，因此仍不应与本方法并发调用。
        /// </remarks>
        public void Dispose()
        {
            lock (_lock)
            {
                try
                {
                    _seq = 0;
                    while (_logs.TryDequeue(out var log))
                    {
                        foreach (var handler in OnLogRemoved.GetInvocationListOrEmpty())
                        {
                            try { handler?.Invoke(log); }
                            catch { }
                        }
                    }
                    OnLogAdded = null;
                    OnLogRemoved = null;
                    OnLogRepeated = null;
                }
                catch { }
            }
        }
    }
}
