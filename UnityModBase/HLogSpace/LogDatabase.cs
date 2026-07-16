using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityModBase.HProvider;

namespace UnityModBase.HLogSpace
{
    /// <summary>
    /// 保存进程内日志快照，为新日志分配序号，并把内容等价的日志合并为重复计数。
    /// 容量超过 <see cref="MaxLogCount"/> 时移除队首条目；文件输出由订阅此数据库的 <see cref="LogWriter"/> 负责。
    /// </summary>
    /// <remarks>
    /// 队列和序号分配支持并发追加，但重复项查找及 <see cref="LogEntry.RepeatCount"/> 更新不是原子事务；
    /// 多线程同时写入内容等价的日志时，调用方不应依赖严格的合并次数或容量瞬时上限。
    /// </remarks>
    public class LogDatabase : IDisposable
    {
        /// <summary>
        /// 正常追加路径保留的目标日志条数上限。
        /// </summary>
        public const int MaxLogCount = 500;

        private int _seq = 0;
        private readonly ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();

        /// <summary>
        /// 当前队列结构的只读快照。后续增删不会改变已取得的列表，但其中的 <see cref="LogEntry"/> 仍可能更新重复信息。
        /// </summary>
        public IReadOnlyList<LogEntry> Logs => _logs.ToArray();

        /// <summary>
        /// 通过详细参数重载创建过的日志序号；直接传入 <see cref="LogEntry"/> 不会更新该值。
        /// </summary>
        public int Seq => _seq;

        /// <summary>
        /// 用于采集 Unity 帧号和场景的服务；可为 <c>null</c>，此时分别回退为 <c>0</c> 和 <c>?</c>。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 新的非重复日志入队后触发。
        /// </summary>
        public event Action<LogEntry> OnLogAdded;

        /// <summary>
        /// 因容量限制或释放而移除日志时触发。
        /// </summary>
        public event Action<LogEntry> OnLogRemoved;

        /// <summary>
        /// 内容等价的日志合并到已有条目后触发，参数为已更新的原条目。
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
        /// 添加已有日志条目。<c>null</c> 按空操作处理；等价条目会合并到队列中首个匹配项，而不触发新增事件。
        /// 单个事件订阅者抛出的异常会被忽略，后续订阅者仍会执行。
        /// </summary>
        /// <param name="log">要添加或合并的日志条目。</param>
        public void AddLog(LogEntry log)
        {
            if (log == null)
                return;

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

        /// <summary>
        /// 使用当前时间、托管线程、Unity 帧和场景构造日志后添加，并以原子递增值分配条目序号。
        /// </summary>
        /// <param name="logLevel">日志等级。</param>
        /// <param name="msg">日志文本；<c>null</c> 会由条目规范化为空字符串。</param>
        /// <param name="ex">关联异常，可为 <c>null</c>。</param>
        /// <param name="member">调用成员名；<c>null</c> 会规范化为空字符串。</param>
        /// <param name="file">调用源文件路径；<c>null</c> 会规范化为空字符串。</param>
        /// <param name="line">调用源文件行号；<c>0</c> 表示未提供。</param>
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
        /// 清空队列、把序号复位为 <c>0</c> 并移除全部事件订阅者。
        /// 此方法不会把实例标记为终止状态，释放后仍可重新添加日志。
        /// </summary>
        /// <remarks>应避免与并发写入交错；复位序号和清空队列不是相对于 <see cref="AddLog(LogLevel, string, Exception, string, string, int)"/> 的原子操作。</remarks>
        public void Dispose()
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
