using System;
using System.IO;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.BSpace
{
    /// <summary>
    /// 组装 UnityModBase 自身的用户上下文、配置服务和文件日志，并维护框架专属的引用与事件联动。
    /// 具体配置项由 <see cref="BConfigManager"/> 声明，通用服务资源由 <see cref="UserService"/> 持有；
    /// 用户注册表及其中资源的最终释放由进程级生命周期通过 <see cref="UserManager.Dispose"/> 统一完成。
    /// </summary>
    internal static class BService
    {
        private const string DirectoryName = nameof(UnityModBase);

        // 只串行化基础服务的初始化与释放；子服务的日常调用仍遵循各自的线程安全约束。
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        internal static string BaseDirectory { get; set; }
        internal static UserContext Context { get; set; }
        internal static UserService Service => Context?.Service;
        internal static LogDatabase LogDatabase => Service?.LogDatabase;
        internal static LogWriter LogWriter => Service?.LogWriter;
        internal static ConfigService Config => Service?.Config;

        /// <summary>
        /// 创建框架用户上下文，并在规范化后的基础目录中注册配置文件和按小时命名的日志文件。
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
                    Context = UserManager.Register(nameof(UnityModBase), nameof(UnityModBase));

                    shouldDispose = true;

                    BaseDirectory = baseDirectory;
                    if (!string.Equals(new DirectoryInfo(BaseDirectory).Name, DirectoryName, StringComparison.OrdinalIgnoreCase))
                        BaseDirectory = Path.Combine(BaseDirectory, DirectoryName);

                    if (!Directory.Exists(BaseDirectory))
                        Directory.CreateDirectory(BaseDirectory);

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
