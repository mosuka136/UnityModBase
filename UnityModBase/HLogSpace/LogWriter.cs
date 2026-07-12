using System;
using System.IO;
using System.Text;
using System.Threading;

namespace UnityModBase.HLogSpace
{
    public class LogWriter : IDisposable
    {
        private static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan LongestDuration = TimeSpan.FromSeconds(5);

        private Timer _timer;
        private LogEntry _lastLog;
        private StreamWriter _writer;
        private readonly object _lock = new object();

        public bool Enable { get; set; } = true;
        public LogLevel Level { get; set; } = LogLevel.Info;

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

        public void Write(LogEntry log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            if (!Enable)
                return;

            try { _writer?.WriteLine(log.ToString()); }
            catch { }
        }

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
        /// 释放日志资源，关闭文件写入器和定时器。
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
