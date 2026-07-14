using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityModBase.HProvider;

namespace UnityModBase.HLogSpace
{
    public class LogDatabase : IDisposable
    {
        public const int MaxLogCount = 500;

        private int _seq = 0;
        private readonly ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();

        /// <summary>
        /// 当前日志的只读快照。
        /// </summary>
        public IReadOnlyList<LogEntry> Logs => _logs.ToArray();
        public int Seq => _seq;
        public UnityProvider UnityService { get; }

        public event Action<LogEntry> OnLogAdded;
        public event Action<LogEntry> OnLogRemoved;
        public event Action<LogEntry> OnLogRepeated;

        public LogDatabase(UnityProvider unityService)
        {
            UnityService = unityService;
        }

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

        public void Debug(string msg, string member, string file, int line) => AddLog(LogLevel.Debug, msg, null, member, file, line);

        public void Info(string msg, string member, string file, int line) => AddLog(LogLevel.Info, msg, null, member, file, line);

        public void Notice(string msg, string member, string file, int line) => AddLog(LogLevel.Notice, msg, null, member, file, line);

        public void Warn(string msg, string member, string file, int line) => AddLog(LogLevel.Warning, msg, null, member, file, line);

        public void Error(string msg, Exception ex, string member, string file, int line) => AddLog(LogLevel.Error, msg, ex, member, file, line);

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
