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
    /// 等价项查找与更新、新条目入队、容量淘汰和队列清理由同一实例锁串行化。
    /// 所有队列变更先取得通知锁，再在队列锁内提交，并在释放队列锁后按提交顺序同步发布事件。
    /// 处理器重入写入时，新的通知会排到当前通知之后，避免不同订阅者观察到相反顺序。
    /// 详细参数重载使用原子递增分配序号，但序号复位不与该递增互斥，因此不应与 <see cref="Dispose"/> 并发调用。
    /// </remarks>
    public class LogDatabase : IDisposable
    {
        /// <summary>
        /// 新增非重复日志后保留的条目数上限；重复日志只累加计数，不占用新条目。
        /// </summary>
        public const int MaxLogCount = 500;

        // 保护等价项查找与更新、新条目入队、容量淘汰及释放清理，使一次队列变更保持原子性。
        // 所有取得本锁的路径都必须先取得 _handlerLock，事件回调不得持有本锁。
        private readonly object _lock = new object();

        // 串行化队列提交、快照订阅和通知分发，确保所有订阅者按同一提交顺序观察数据库变化。
        // Monitor 允许处理器重入；重入路径只追加通知，由最外层分发循环在当前通知结束后处理。
        private readonly object _handlerLock = new object();

        // 以下分发状态只允许在 _handlerLock 下访问。队列保存已经提交、尚未发布的数据库变更。
        private readonly Queue<LogNotification> _pendingNotifications = new Queue<LogNotification>();

        // 防止重入调用启动嵌套分发，否则不同订阅者可能以相反顺序观察外层和重入产生的事件。
        private bool _isDispatchingNotifications = false;

        // Dispose 从处理器内重入时不能立即清空委托，必须等排队的移除通知全部发布后再清理。
        private bool _clearHandlersAfterDispatch = false;

        // 详细参数重载通过 Interlocked 递增；Dispose 直接复位为 0，二者并发时可能产生重复序号。
        private int _seq = 0;

        // 结构变更仍由 _lock 保护；使用并发队列是为了让 Logs 能在不取得队列锁时安全创建快照。
        private readonly ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();

        /// <summary>
        /// 当前队列结构的只读快照。后续增删不会改变已取得的列表，但其中的 <see cref="LogEntry"/> 仍可能更新重复信息。
        /// </summary>
        /// <remarks>
        /// 此属性不会取得数据库变更锁；并发执行“入队后淘汰”时，快照可能短暂包含超出容量上限的条目。
        /// 需要与后续事件无遗漏衔接的一致快照时，应使用 <see cref="SubscribeWithSnapshot"/>。
        /// </remarks>
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
        /// 新的非重复日志入队且完成容量淘汰后，在添加线程上按数据库提交顺序同步触发。
        /// 同一次添加触发容量淘汰时，本事件晚于对应的 <see cref="OnLogRemoved"/>。
        /// </summary>
        public event Action<LogEntry> OnLogAdded;

        /// <summary>
        /// 因容量限制或释放而移除日志时同步触发。
        /// 容量淘汰和释放清理均在队列锁外按数据库提交顺序通知。
        /// </summary>
        public event Action<LogEntry> OnLogRemoved;

        /// <summary>
        /// 内容等价的日志合并到已有条目后，在添加线程上按数据库提交顺序同步触发；参数为已更新的首个匹配条目。
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
        /// 在同一数据库变更边界内取得一致快照、注册后续变化处理器并执行初始化。
        /// </summary>
        /// <param name="initialize">接收当前队列快照的初始化处理器，不能为 <c>null</c>。</param>
        /// <param name="onLogAdded">后续新增日志处理器，可为 <c>null</c>。</param>
        /// <param name="onLogRemoved">后续移除日志处理器，可为 <c>null</c>。</param>
        /// <param name="onLogRepeated">后续重复日志处理器，可为 <c>null</c>。</param>
        /// <remarks>
        /// 后续处理器会先注册，但初始化处理器返回前其他线程不能提交日志变更，因此快照与事件之间没有遗漏或乱序窗口。
        /// 初始化处理器不应重入当前数据库，否则重入事件会在初始化完成前同步到达新处理器。
        /// 若初始化处理器抛出异常，本次新增的处理器会各退订一次，原异常随后重新抛出；初始化已产生的其他副作用不会回滚。
        /// 需要无遗漏初始化的调用方应使用本方法，不要自行组合 <see cref="Logs"/> 快照与事件订阅。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="initialize"/> 为 <c>null</c>。</exception>
        public void SubscribeWithSnapshot(Action<IReadOnlyList<LogEntry>> initialize, Action<LogEntry> onLogAdded, Action<LogEntry> onLogRemoved, Action<LogEntry> onLogRepeated)
        {
            if (initialize == null)
                throw new ArgumentNullException(nameof(initialize));

            lock (_handlerLock)
            {
                LogEntry[] snapshot;
                lock (_lock)
                    snapshot = _logs.ToArray();

                OnLogAdded += onLogAdded;
                OnLogRemoved += onLogRemoved;
                OnLogRepeated += onLogRepeated;

                try
                {
                    initialize(snapshot);
                }
                catch
                {
                    OnLogAdded -= onLogAdded;
                    OnLogRemoved -= onLogRemoved;
                    OnLogRepeated -= onLogRepeated;
                    throw;
                }
            }
        }

        /// <summary>
        /// 从对应事件中各移除一次指定处理器，并与其他线程正在执行的数据库通知串行。
        /// </summary>
        /// <param name="onLogAdded">要从新增事件移除的处理器，可为 <c>null</c>。</param>
        /// <param name="onLogRemoved">要从移除事件移除的处理器，可为 <c>null</c>。</param>
        /// <param name="onLogRepeated">要从重复事件移除的处理器，可为 <c>null</c>。</param>
        /// <remarks>
        /// 从通知线程外调用时，本方法会等待当前回调结束。在处理器内重入调用时，退订对后续通知生效，
        /// 但不会改变当前通知已经捕获的处理器列表。
        /// </remarks>
        public void Unsubscribe(Action<LogEntry> onLogAdded, Action<LogEntry> onLogRemoved, Action<LogEntry> onLogRepeated)
        {
            lock (_handlerLock)
            {
                OnLogAdded -= onLogAdded;
                OnLogRemoved -= onLogRemoved;
                OnLogRepeated -= onLogRepeated;
            }
        }

        /// <summary>
        /// 添加已有日志条目。查找时已存在等价条目则更新首个匹配项的最后重复时间和累计次数；
        /// 否则将条目入队，并在超出容量时移除最早条目。
        /// </summary>
        /// <param name="log">要添加或合并的日志条目，不能为 <c>null</c>。</param>
        /// <remarks>
        /// 查找、合并、入队和容量淘汰在同一个队列临界区内完成；事件在释放队列锁后按提交顺序执行。
        /// 不同写入线程不会并发变更队列或执行订阅者。处理器可以重入数据库；重入通知会延迟到当前通知已发送给全部订阅者后。
        /// 合并时使用 <paramref name="log"/> 的 <see cref="LogEntry.Timestamp"/> 更新最后重复时间，并把其
        /// <see cref="LogEntry.RepeatCount"/> 整体累加到已有条目；不会采用传入条目的 <see cref="LogEntry.LastRepeatTime"/>。
        /// 单个处理器抛出的异常会被忽略，后续处理器仍会执行。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="log"/> 为 <c>null</c>。</exception>
        public void AddLog(LogEntry log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            lock (_handlerLock)
            {
                lock (_lock)
                {
                    var repeatLog = _logs.FirstOrDefault(l => l.Equals(log));
                    if (repeatLog != null)
                    {
                        repeatLog.LastRepeatTime = log.Timestamp;
                        repeatLog.RepeatCount += log.RepeatCount;
                        _pendingNotifications.Enqueue(new LogNotification(LogNotificationType.Repeated, repeatLog));
                    }
                    else
                    {
                        _logs.Enqueue(log);
                        if (_logs.Count > MaxLogCount && _logs.TryDequeue(out var removedLog))
                            _pendingNotifications.Enqueue(new LogNotification(LogNotificationType.Removed, removedLog));
                        _pendingNotifications.Enqueue(new LogNotification(LogNotificationType.Added, log));
                    }
                }

                DispatchPendingNotifications();
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
        /// 序号通过 <see cref="Interlocked.Increment(ref int)"/> 分配，随后才采集运行时信息并调用 <see cref="AddLog(LogEntry)"/>，
        /// 因此整个构造与添加过程不是原子事务。请勿与 <see cref="Dispose"/> 并发调用，否则复位前分配的条目仍可能在复位后入队，
        /// 且与复位后创建的条目使用相同序号。
        /// </remarks>
        public void AddLog(LogLevel logLevel, string msg, Exception ex, string member, string file, int line)
        {
            int id = Interlocked.Increment(ref _seq);
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
        /// 队列清理、移除通知及事件清理与 <see cref="AddLog(LogEntry)"/> 串行执行。从通知线程外调用时，
        /// 本方法返回前会等待其他线程的在途回调结束；从处理器内重入时，事件清理延迟到最外层分发结束。
        /// 序号复位仍不与详细参数重载的原子递增互斥。移除处理器异常会被忽略。
        /// </remarks>
        public void Dispose()
        {
            lock (_handlerLock)
            {
                lock (_lock)
                {
                    _seq = 0;
                    while (_logs.TryDequeue(out var log))
                        _pendingNotifications.Enqueue(new LogNotification(LogNotificationType.Removed, log));
                }

                _clearHandlersAfterDispatch = true;
                DispatchPendingNotifications();
            }
        }

        private void DispatchPendingNotifications()
        {
            // 调用方必须持有 _handlerLock。方法有意不自行加锁，以便重入调用共享同一分发状态和待通知队列。
            // 重入的 AddLog 或 Dispose 只负责排队；最外层循环必须先把当前通知发送给全部订阅者。
            if (_isDispatchingNotifications)
                return;

            _isDispatchingNotifications = true;
            try
            {
                while (_pendingNotifications.Count > 0)
                {
                    var notification = _pendingNotifications.Dequeue();
                    Action<LogEntry> handlers;
                    switch (notification.Type)
                    {
                        case LogNotificationType.Added:
                            handlers = OnLogAdded;
                            break;
                        case LogNotificationType.Removed:
                            handlers = OnLogRemoved;
                            break;
                        default:
                            handlers = OnLogRepeated;
                            break;
                    }

                    // 每次出队时重新读取委托，使处理器内的退订能影响后续排队通知，但不改变当前调用列表。
                    foreach (var handler in handlers.GetInvocationListOrEmpty())
                    {
                        try { handler?.Invoke(notification.Log); }
                        catch { }
                    }
                }
            }
            finally
            {
                _isDispatchingNotifications = false;
                if (_clearHandlersAfterDispatch)
                {
                    OnLogAdded = null;
                    OnLogRemoved = null;
                    OnLogRepeated = null;
                    _clearHandlersAfterDispatch = false;
                }
            }
        }

        private enum LogNotificationType
        {
            Added,
            Removed,
            Repeated,
        }

        private readonly struct LogNotification
        {
            public LogNotificationType Type { get; }
            public LogEntry Log { get; }

            public LogNotification(LogNotificationType type, LogEntry log)
            {
                Type = type;
                Log = log;
            }
        }
    }
}
