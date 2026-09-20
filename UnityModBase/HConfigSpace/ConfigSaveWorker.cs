using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 配置文件的共享后台写服务：接收"已在调用线程完成编码的整文件内容"，按目标路径合并排队后由定时器线程串行落盘。
    /// 同一路径连续入队时只保留最新内容（latest-wins），因此快速连续的配置变更只产生一次磁盘写入。
    /// 该类型只负责磁盘 IO；文件模型的编码必须由调用方（通常是 Unity 主线程）完成后以不可变字符串传入。
    /// </summary>
    /// <remarks>
    /// <see cref="Enqueue"/> 只做轻量的原子字典操作，不会因后台正在写盘而阻塞调用线程；
    /// 写盘在 <c>_drainLock</c> 内串行执行，保证同一文件不会被并发写入，也使 <see cref="WriteNow"/>、
    /// <see cref="FlushFile"/> 与 <see cref="FlushAll"/> 能通过版本号握手确认写入完成。
    /// 后台写失败不会直接记录日志（日志数据库会访问 Unity API），而是进入失败队列，
    /// 由主线程通过 <see cref="TryDequeueFailure"/> 逐帧取走后上报。该类型不依赖任何 Unity API。
    /// </remarks>
    internal sealed class ConfigSaveWorker : IDisposable
    {
        private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMilliseconds(300);

        // 入队、后台写盘和等待握手共享的串行点；配置文件很小，锁内写盘的开销可忽略。
        private readonly object _drainLock = new object();

        // 待写内容按路径 latest-wins；PendingWrite 不可变，使内容与版本号作为整体被原子替换。
        private readonly ConcurrentDictionary<string, PendingWrite> _pending =
            new ConcurrentDictionary<string, PendingWrite>();

        // 已写盘的版本号与最近一次成败；仅在持有 _drainLock 时更新，等待者据此确认目标版本完成。
        private readonly ConcurrentDictionary<string, long> _writtenVersions =
            new ConcurrentDictionary<string, long>();

        private readonly ConcurrentDictionary<string, bool> _writeResults =
            new ConcurrentDictionary<string, bool>();

        // 后台写失败报告；由主线程逐帧取走并记录日志。
        private readonly ConcurrentQueue<SaveFailure> _failures = new ConcurrentQueue<SaveFailure>();

        private readonly TimeSpan _flushInterval;
        private Timer _timer;
        private bool _disposed;
        // 跨路径全局递增的写入版本号，分配给每次入队的内容；等待者据同一路径的已写版本号确认目标内容落盘。
        private long _version;

        /// <summary>
        /// 启动后台写服务，按指定周期待写内容落盘；周期内同一路径的多次入队会被合并为一次写入。
        /// </summary>
        /// <param name="flushInterval">
        /// 后台写盘周期，默认 <c>300</c> 毫秒；测试可传入较长周期并改用 <see cref="FlushAll"/> 手动驱动。
        /// </param>
        internal ConfigSaveWorker(TimeSpan? flushInterval = null)
        {
            _flushInterval = flushInterval ?? DefaultFlushInterval;
            _timer = new Timer(DrainTimerCallback, null, _flushInterval, _flushInterval);
        }

        /// <summary>
        /// 把一份已编码的配置内容排入后台写队列；同一路径的先前待写内容会被直接替换。
        /// 该方法不等待写入完成，也不返回写入结果；失败会进入失败队列，由主线程取走后上报。
        /// 服务已释放时退化为调用线程同步写入。
        /// </summary>
        /// <param name="filePath">目标配置文件路径。</param>
        /// <param name="content">完整的文件内容。</param>
        /// <returns>分配给本次内容的递增版本号；自动保存路径不使用该值。</returns>
        internal long Enqueue(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath) || content == null)
                return 0;

            if (_disposed)
            {
                TryWriteFile(filePath, content, out _);
                return 0;
            }

            var version = Interlocked.Increment(ref _version);
            _pending[filePath] = new PendingWrite(content, version);
            return version;
        }

        /// <summary>
        /// 排入内容并立即触发后台写，阻塞等待该版本写入完成后返回成败。
        /// 这是显式保存路径的入口：语义与调用线程同步写盘一致，同时保持与后台队列的严格串行。
        /// </summary>
        /// <param name="filePath">目标配置文件路径。</param>
        /// <param name="content">完整的文件内容。</param>
        /// <param name="timeout">等待写入完成的最长时间；超时按失败返回，但内容仍保留在队列中等待后续写入。</param>
        /// <returns>目标版本已成功落盘时返回 <c>true</c>；超时或写入失败时返回 <c>false</c>。</returns>
        internal bool WriteNow(string filePath, string content, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(filePath) || content == null)
                return false;

            if (_disposed)
                return TryWriteFile(filePath, content, out _);

            var version = Interlocked.Increment(ref _version);
            _pending[filePath] = new PendingWrite(content, version);

            TriggerImmediateFlush();
            return WaitForVersion(filePath, version, timeout);
        }

        /// <summary>
        /// 等待指定路径的全部待写内容落盘；没有待写内容时立即返回。
        /// 用于读取磁盘前（如配置重载）确保最近的排队写入已经生效。
        /// </summary>
        /// <param name="filePath">目标配置文件路径。</param>
        /// <param name="timeout">等待写入完成的最长时间。</param>
        /// <returns>待写内容已全部落盘（或本就没有待写内容）时返回 <c>true</c>；等待超时返回 <c>false</c>。服务已释放时队列恒空，直接返回 <c>true</c>。</returns>
        internal bool FlushFile(string filePath, TimeSpan timeout)
        {
            // 释放流程会在停止定时器后同步写完全部待写内容，因此释放后队列恒为空。
            if (string.IsNullOrWhiteSpace(filePath) || _disposed)
                return true;

            long target;
            // 没有待写内容时无需等待：有写盘记录说明此前内容已落盘，无记录说明该路径从未入队。
            if (_pending.TryGetValue(filePath, out var pending))
                target = pending.Version;
            else if (_writtenVersions.TryGetValue(filePath, out _))
                return true;
            else
                return true;

            TriggerImmediateFlush();
            return WaitForVersion(filePath, target, timeout);
        }

        /// <summary>
        /// 触发一次立即的写盘周期，并阻塞等待全部待写内容落盘。
        /// 用于进程退出前集中刷盘；超时后剩余内容仍保留在队列中。
        /// </summary>
        /// <param name="timeout">等待全部写入完成的最长时间。</param>
        /// <returns>队列已清空时返回 <c>true</c>；等待超时返回 <c>false</c>。服务已释放时剩余内容已在释放时写完，按队列状态返回。</returns>
        internal bool FlushAll(TimeSpan timeout)
        {
            if (_disposed)
                return _pending.IsEmpty;

            TriggerImmediateFlush();

            var stopwatch = Stopwatch.StartNew();
            lock (_drainLock)
            {
                while (_pending.Count > 0)
                {
                    if (_disposed)
                        return _pending.IsEmpty;

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    Monitor.Wait(_drainLock, remaining);
                }

                return true;
            }
        }

        /// <summary>
        /// 取出一份最早的后台写失败报告；应在 Unity 主线程逐帧调用，并把结果记录到常规日志。
        /// </summary>
        /// <param name="filePath">失败的目标文件路径。</param>
        /// <param name="exception">写盘时捕获的异常。</param>
        /// <returns>成功取出一份失败报告时返回 <c>true</c>；队列已空时返回 <c>false</c>。</returns>
        internal bool TryDequeueFailure(out string filePath, out Exception exception)
        {
            if (_failures.TryDequeue(out var failure))
            {
                filePath = failure.FilePath;
                exception = failure.Exception;
                return true;
            }

            filePath = null;
            exception = null;
            return false;
        }

        /// <summary>
        /// 停止后台定时器，把剩余待写内容在当前线程同步写完。可重复调用。
        /// 释放后入队和立即写入都退化为调用线程同步写盘。
        /// </summary>
        public void Dispose()
        {
            lock (_drainLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _timer?.Dispose();
                _timer = null;

                // 定时器已停止，等待者不会再被唤醒；直接写完剩余内容并唤醒它们按超时/完成状态返回。
                DrainPending();
                Monitor.PulseAll(_drainLock);
            }
        }

        /// <summary>
        /// 把目标文件内容写入磁盘：先写同目录唯一临时文件，再替换或移动到最终路径并生成 <c>.bak</c> 备份，
        /// 失败路径尽力清理残留临时文件。未注入后台写服务时，<see cref="ConfigService"/> 的同步直写路径与本服务的后台写盘共用本方法。
        /// </summary>
        /// <param name="filePath">目标配置文件路径。</param>
        /// <param name="content">完整的文件内容。</param>
        /// <param name="exception">写盘失败时捕获到的异常；成功时为 <c>null</c>。</param>
        /// <returns>写入成功时返回 <c>true</c>；目标目录创建、写入或替换失败时返回 <c>false</c>。</returns>
        internal static bool TryWriteFile(string filePath, string content, out Exception exception)
        {
            exception = null;
            var tmpFilePath = string.Empty;

            try
            {
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                tmpFilePath = Path.Combine(directoryPath ?? string.Empty, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
                var backupFilePath = filePath + ".bak";

                File.WriteAllText(tmpFilePath, content);

                if (File.Exists(filePath))
                    File.Replace(tmpFilePath, filePath, backupFilePath, true);
                else
                    File.Move(tmpFilePath, filePath);

                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpFilePath))
                        File.Delete(tmpFilePath);
                }
                catch
                {
                }
            }
        }

        private void DrainTimerCallback(object state)
        {
            lock (_drainLock)
            {
                if (_disposed)
                    return;

                DrainPending();
            }
        }

        /// <summary>
        /// 取出并写完全部待写内容；必须在持有 <c>_drainLock</c> 时调用。
        /// 定时器回调可能重入，由锁串行化后第二轮自然发现队列已空。
        /// </summary>
        private void DrainPending()
        {
            if (_pending.Count == 0)
                return;

            var snapshot = new List<KeyValuePair<string, PendingWrite>>();
            foreach (var key in _pending.Keys)
            {
                if (_pending.TryRemove(key, out var pending))
                    snapshot.Add(new KeyValuePair<string, PendingWrite>(key, pending));
            }

            foreach (var item in snapshot)
            {
                var succeeded = TryWriteFile(item.Key, item.Value.Content, out var exception);
                _writtenVersions[item.Key] = item.Value.Version;
                _writeResults[item.Key] = succeeded;

                if (!succeeded)
                    _failures.Enqueue(new SaveFailure(item.Key, exception));
            }

            Monitor.PulseAll(_drainLock);
        }

        private void TriggerImmediateFlush()
        {
            // 只把到期时间提前到当下，周期保持不变，后续入队仍享有完整的合并窗口。
            _timer?.Change(TimeSpan.Zero, _flushInterval);
        }

        /// <summary>
        /// 等待指定路径的目标版本写盘完成。等待循环在 <c>_drainLock</c> 内进行，写盘方也在该锁内更新版本与成败并唤醒等待者，避免丢失唤醒。
        /// 版本比较使用大于等于：目标内容可能已被同一路径的更新内容覆盖落盘（latest-wins），此时无需也不应再写入旧内容。
        /// </summary>
        private bool WaitForVersion(string filePath, long targetVersion, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            lock (_drainLock)
            {
                while (true)
                {
                    if (_writtenVersions.TryGetValue(filePath, out var version) && version >= targetVersion)
                        return _writeResults.TryGetValue(filePath, out var succeeded) && succeeded;

                    if (_disposed)
                        return false;

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    Monitor.Wait(_drainLock, remaining);
                }
            }
        }

        private sealed class PendingWrite
        {
            public readonly string Content;
            public readonly long Version;

            public PendingWrite(string content, long version)
            {
                Content = content;
                Version = version;
            }
        }

        private sealed class SaveFailure
        {
            public readonly string FilePath;
            public readonly Exception Exception;

            public SaveFailure(string filePath, Exception exception)
            {
                FilePath = filePath;
                Exception = exception;
            }
        }
    }
}
