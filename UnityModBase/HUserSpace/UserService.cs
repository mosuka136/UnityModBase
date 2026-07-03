using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HUserSpace
{
    public class UserService : IDisposable
    {
        public string UserId { get; }
        public LogDatabase LogDatabase { get; private set; }
        public LogWriter LogWriter { get; private set; }
        public ConfigService Config { get; private set; }
        public Type ConfigManagerType { get; private set; }

        public UserService(string userId)
        {
            UserId = userId;
            LogDatabase = new LogDatabase(UnityProvider.Instance);
        }

        public void RegisterLog(string directory, string fileName, LogLevel level)
        {
            LogWriter = new LogWriter(directory, fileName, level);

            foreach (var log in LogDatabase.Logs)
                LogWriter.Log(log);
            LogDatabase.OnLogAdded += LogWriter.Log;
            LogDatabase.OnLogRepeated += LogWriter.Log;
        }

        public void RegisterConfig<T>(string configFilePath) where T : class
        {
            RegisterConfig(typeof(T), configFilePath);
        }

        public void RegisterConfig(Type configManagerType, string configFilePath)
        {
            ConfigManagerType = configManagerType;
            Config = new ConfigService(configFilePath);
        }

        public void Dispose()
        {
            try
            {
                LogDatabase?.Dispose();
                LogDatabase = null;
                LogWriter?.Flush(true);
                LogWriter?.Dispose();
                LogWriter = null;
                Config = null;
            }
            catch { }
        }
    }
}
