using System;
using System.IO;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.BSpace
{
    /// <summary>
    /// 使用框架内置的可翻译显示名组装 UnityModBase 自身的用户上下文、配置服务和文件日志，
    /// 并维护框架专属的引用与事件联动。
    /// 具体配置项由 <see cref="BConfigManager"/> 声明，通用服务资源由 <see cref="UserService"/> 持有；
    /// 用户注册表及其中资源的最终释放由进程级生命周期通过 <see cref="UserManager.Dispose"/> 统一完成。
    /// </summary>
    internal static class BService
    {
        private const string DirectoryName = nameof(UnityModBase);

        // 只串行化基础服务的初始化与释放；子服务的日常调用仍遵循各自的线程安全约束。
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        // 由本服务创建并注入 ConfigService 的共享后台写服务；退出和释放路径据此做最终刷盘。
        private static ConfigSaveWorker _saveWorker;

        internal static string BaseDirectory { get; set; }
        internal static UserContext Context { get; set; }
        internal static UserService Service => Context?.Service;
        internal static LogDatabase LogDatabase => Service?.LogDatabase;
        internal static LogWriter LogWriter => Service?.LogWriter;
        internal static ConfigService Config => Service?.Config;

        /// <summary>
        /// 创建具有可翻译显示名的框架用户上下文，并在规范化后的基础目录中注册配置文件和按小时命名的日志文件。
        /// 重复调用不会重建资源；初始化失败时会清理本类建立的事件联动和静态引用，并重新抛出原始异常。
        /// </summary>
        /// <param name="baseDirectory">
        /// UnityModBase 数据目录或其父目录；末级目录名不是 <c>UnityModBase</c> 时会自动追加该子目录。
        /// </param>
        /// <remarks>
        /// 已登记到 <see cref="UserManager"/> 的上下文不由本类直接释放。顶层初始化失败回滚会通过
        /// <see cref="UnityModBase.Dispose"/> 统一释放用户注册表及其配置和日志资源。
        /// </remarks>
        internal static void Initialize(string baseDirectory)
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                var shouldDispose = false;
                try
                {
                    Context = UserManager.Register(nameof(UnityModBase), BTranslatorResource.UserName);

                    shouldDispose = true;

                    BaseDirectory = baseDirectory;
                    if (!string.Equals(new DirectoryInfo(BaseDirectory).Name, DirectoryName, StringComparison.OrdinalIgnoreCase))
                        BaseDirectory = Path.Combine(BaseDirectory, DirectoryName);

                    if (!Directory.Exists(BaseDirectory))
                        Directory.CreateDirectory(BaseDirectory);

                    // 后台写服务必须先于首个配置文件创建注入，使框架配置的首次保存也进入后台队列。
                    _saveWorker = new ConfigSaveWorker();
                    ConfigService.SaveWriter = _saveWorker;
                    GameQuitManager.OnGameQuit += FlushSaveWorkerOnGameQuit;

                    var configPath = Path.Combine(BaseDirectory, $"{nameof(UnityModBase)}.cfg");
                    Service.RegisterConfig(typeof(BConfigManager), configPath);
                    BConfigManager.Initialize(configPath);

                    Service.RegisterLog(Path.Combine(BaseDirectory, "logs"), nameof(UnityModBase), BConfigManager.LogLevel.Value);
                    LogWriter.Enable = BConfigManager.EnableLog.Value;

                    BConfigManager.EnableLog.OnValueChanged += OnEnableLogChanged;
                    BConfigManager.LogLevel.OnValueChanged += OnLogLevelChanged;

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    LogDatabase?.Error($"Failed to initialize {nameof(BService)}", ex, nameof(BService), null, 0);
                    if (shouldDispose)
                    {
                        _initialized = true;
                        Dispose();
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// 解除框架配置的事件联动，并清除本类保存的基础目录和用户上下文引用。
        /// 此方法不从用户注册表移除或释放上下文；进程级释放流程会随后调用 <see cref="UserManager.Dispose"/>。
        /// </summary>
        internal static void Dispose()
        {
            lock (_lock)
            {
                if (!_initialized)
                    return;

                var logDatabase = LogDatabase;

                try
                {
                    if (BConfigManager.EnableLog != null)
                        BConfigManager.EnableLog.OnValueChanged -= OnEnableLogChanged;

                    if (BConfigManager.LogLevel != null)
                        BConfigManager.LogLevel.OnValueChanged -= OnLogLevelChanged;

                    BConfigManager.Dispose();

                    // 先解除退出订阅再释放写服务：Dispose 会在停止定时器后同步写完剩余内容。
                    GameQuitManager.OnGameQuit -= FlushSaveWorkerOnGameQuit;
                    ConfigService.SaveWriter = null;
                    _saveWorker?.Dispose();
                    _saveWorker = null;
                }
                catch (Exception ex)
                {
                    logDatabase?.Error($"Failed to dispose {nameof(BService)}", ex, nameof(BService), null, 0);
                }
                finally
                {
                    Context = null;
                    BaseDirectory = null;
                    _initialized = false;
                }
            }
        }

        /// <summary>
        /// 游戏退出或框架释放时，把后台写队列中的全部待写配置内容刷到磁盘。
        /// 该回调在进程级释放流程中先于用户配置服务拆除执行；超时后剩余内容仍由写服务的释放兜底完成。
        /// </summary>
        private static void FlushSaveWorkerOnGameQuit()
        {
            var worker = _saveWorker;
            if (worker != null && !worker.FlushAll(TimeSpan.FromSeconds(3)))
                LogDatabase?.Warn("Timed out flushing pending config file writes on game quit; the disposer will finish the remainder.", nameof(BService), null, 0);
        }

        private static void OnEnableLogChanged(object sender, bool enable)
        {
            if (LogWriter != null)
                LogWriter.Enable = enable;
        }

        private static void OnLogLevelChanged(object sender, LogLevel level)
        {
            if (LogWriter != null)
                LogWriter.Level = level;
        }
    }
}
