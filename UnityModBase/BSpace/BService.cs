using System;
using System.IO;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.BSpace
{
    internal static class BService
    {
        public const string DirectoryName = nameof(UnityModBase);

        private static readonly object _lock = new object();
        private static bool _initialized = false;

        public static string BaseDirectory { get; private set; }
        public static UserContext Context { get; private set; }
        public static UserService Service => Context?.Service;
        public static LogDatabase LogDatabase => Service?.LogDatabase;
        public static LogWriter LogWriter => Service?.LogWriter;
        public static ConfigService Config => Service?.Config;

        public static void Initialize(string baseDirectory)
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

        public static void Dispose()
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
                    UserManager.Dispose();
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
