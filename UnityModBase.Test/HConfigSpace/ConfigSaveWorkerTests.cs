using UnityModBase.HConfigSpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigSaveWorkerTests : IDisposable
    {
        // 长到测试期间不会自行到期的写盘周期，用例一律通过 WriteNow/FlushAll/Dispose 显式驱动写盘。
        private static readonly TimeSpan InactiveInterval = TimeSpan.FromSeconds(100);
        // 各用例等待写盘完成的统一上限。
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);

        private readonly List<string> _tempFiles = new List<string>();

        private string CreateTempConfigPath()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cfg");
            _tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var path in _tempFiles)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    foreach (var suffix in new[] { ".tmp", ".bak" })
                    {
                        if (File.Exists(path + suffix))
                            File.Delete(path + suffix);
                    }
                }
                catch { }
            }
        }

        [Fact]
        public void WriteNow_WhenFileWritable_WritesContentAndReturnsTrue()
        {
            var path = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            var result = worker.WriteNow(path, "[Table]\nKey = 1\n", ShortTimeout);

            Assert.True(result);
            Assert.Equal("[Table]\nKey = 1\n", File.ReadAllText(path));
            Assert.False(worker.TryDequeueFailure(out _, out _));
        }

        [Fact]
        public void WriteNow_WhenTargetExists_CreatesBackupAndLeavesNoTemporaryFile()
        {
            var path = CreateTempConfigPath();
            File.WriteAllText(path, "old");
            using var worker = new ConfigSaveWorker(InactiveInterval);

            var result = worker.WriteNow(path, "new", ShortTimeout);

            Assert.True(result);
            Assert.Equal("new", File.ReadAllText(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Equal("old", File.ReadAllText(path + ".bak"));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".*.tmp"));
        }

        [Fact]
        public void Enqueue_MultipleTimesForSamePath_FlushAllWritesOnlyLatestContent()
        {
            var path = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            worker.Enqueue(path, "content-1");
            worker.Enqueue(path, "content-2");
            worker.Enqueue(path, "content-3");

            Assert.True(worker.FlushAll(ShortTimeout));
            Assert.Equal("content-3", File.ReadAllText(path));
        }

        [Fact]
        public void WriteNow_WhenEarlierContentIsPendingForSamePath_WritesOnlyLatestContent()
        {
            var path = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            worker.Enqueue(path, "auto-saved-earlier");

            Assert.True(worker.WriteNow(path, "explicit-save-later", ShortTimeout));
            Assert.Equal("explicit-save-later", File.ReadAllText(path));

            // 显式写入与自动保存共用同一 latest-wins 槽位，显式版本落盘后不应再补写被覆盖的旧内容。
            Assert.True(worker.FlushAll(ShortTimeout));
            Assert.Equal("explicit-save-later", File.ReadAllText(path));
        }

        [Fact]
        public void Enqueue_ForDifferentPaths_FlushAllWritesEveryFile()
        {
            var path1 = CreateTempConfigPath();
            var path2 = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            worker.Enqueue(path1, "one");
            worker.Enqueue(path2, "two");

            Assert.True(worker.FlushAll(ShortTimeout));
            Assert.Equal("one", File.ReadAllText(path1));
            Assert.Equal("two", File.ReadAllText(path2));
        }

        [Fact]
        public void FlushFile_WithPendingContent_WaitsForWriteToComplete()
        {
            var path = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            worker.Enqueue(path, "pending");

            Assert.True(worker.FlushFile(path, ShortTimeout));
            Assert.Equal("pending", File.ReadAllText(path));
        }

        [Fact]
        public void FlushFile_WithoutPendingContent_ReturnsImmediately()
        {
            var path = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            Assert.True(worker.FlushFile(path, ShortTimeout));
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void WriteNow_WhenFileIsLocked_ReturnsFalseAndReportsFailure()
        {
            var path = CreateTempConfigPath();
            File.WriteAllText(path, "existing");
            using var worker = new ConfigSaveWorker(InactiveInterval);

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = worker.WriteNow(path, "locked-out", ShortTimeout);
                Assert.False(result);
            }

            Assert.True(worker.TryDequeueFailure(out var failurePath, out var failureException));
            Assert.Equal(path, failurePath);
            Assert.NotNull(failureException);
            Assert.False(worker.TryDequeueFailure(out _, out _));
        }

        [Fact]
        public void Enqueue_WhenWriteFails_ReportsFailureOnce()
        {
            var path = CreateTempConfigPath();
            File.WriteAllText(path, "existing");
            using var worker = new ConfigSaveWorker(InactiveInterval);

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                worker.Enqueue(path, "locked-out");
                // FlushAll 只承诺队列清空；被锁文件的写入失败仍会进入失败队列。
                Assert.True(worker.FlushAll(ShortTimeout));
            }

            Assert.True(worker.TryDequeueFailure(out var failurePath, out _));
            Assert.Equal(path, failurePath);
        }

        [Fact]
        public void Dispose_WritesRemainingPendingContent_AndIsIdempotent()
        {
            var path = CreateTempConfigPath();
            var worker = new ConfigSaveWorker(InactiveInterval);

            worker.Enqueue(path, "last-write");

            worker.Dispose();
            worker.Dispose();

            Assert.Equal("last-write", File.ReadAllText(path));
        }

        [Fact]
        public void WriteNow_AfterDispose_WritesSynchronously()
        {
            var path = CreateTempConfigPath();
            var worker = new ConfigSaveWorker(InactiveInterval);
            worker.Dispose();

            var result = worker.WriteNow(path, "degraded", ShortTimeout);

            Assert.True(result);
            Assert.Equal("degraded", File.ReadAllText(path));
        }

        [Fact]
        public void Enqueue_AfterDispose_WritesSynchronously()
        {
            var path = CreateTempConfigPath();
            var worker = new ConfigSaveWorker(InactiveInterval);
            worker.Dispose();

            worker.Enqueue(path, "degraded-auto-save");

            Assert.Equal("degraded-auto-save", File.ReadAllText(path));
        }

        [Fact]
        public void TryDequeueFailure_WhenNoFailureOccurred_ReturnsFalse()
        {
            var path = CreateTempConfigPath();
            using var worker = new ConfigSaveWorker(InactiveInterval);

            worker.Enqueue(path, "fine");
            Assert.True(worker.FlushAll(ShortTimeout));

            Assert.False(worker.TryDequeueFailure(out _, out _));
        }
    }
}
