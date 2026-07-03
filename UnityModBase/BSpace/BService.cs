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
        public static string BaseDirectory { get; private set; }

        public static UserContext Context { get; private set; }
        public static UserService Service => Context.Service;
        public static LogDatabase LogDatabase => Service.LogDatabase;
        public static LogWriter LogWriter => Service.LogWriter;
        public static ConfigService Config => Service.Config;

        public static void Initialize(string baseDirectory)
        {
            try
            {
                Context = UserManager.Register(nameof(UnityModBase), nameof(UnityModBase));

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
                BConfigManager.EnableLog.OnValueChanged += (s, e) => LogWriter.Enable = e;
                BConfigManager.LogLevel.OnValueChanged += (s, e) => LogWriter.Level = e;
            }
            catch (Exception ex)
            {
                LogDatabase?.Error($"Failed to initialize {nameof(BService)}", ex, nameof(BService), null, 0);
            }
        }

        public static void Dispose()
        {
            try
            {
                Context?.Service.Dispose();
                Context = null;
            }
            catch (Exception ex)
            {
                LogDatabase?.Error($"Failed to dispose {nameof(BService)}", ex, nameof(BService), null, 0);
            }
        }
    }
}
