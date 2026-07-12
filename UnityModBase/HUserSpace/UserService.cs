using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HUserSpace
{
    public class UserService : IDisposable
    {
        private bool _logWriterSubscribed = false;

        public event Action<LogWriter> OnLogWriterRegister;
        public event Action<ConfigService> OnConfigRegister;

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
            UnsubscribeLogWriter();
            LogWriter?.Dispose();

            LogWriter = new LogWriter(directory, fileName, level);

            foreach (var log in LogDatabase.Logs)
                LogWriter.Log(log);
            LogDatabase.OnLogAdded += OnLogAdded;
            LogDatabase.OnLogRepeated += OnLogRepeated;

            _logWriterSubscribed = true;

            foreach(var handler in OnLogWriterRegister.GetInvocationListOrEmpty())
            {
                try
                {
                    handler.Invoke(LogWriter);
                }
                catch (Exception ex)
                {
                    LogDatabase.Error($"Error invoking {nameof(OnLogWriterRegister)} handler.", ex, string.Empty, string.Empty, 0);
                }
            }
        }

        public void RegisterConfig<T>(string configFilePath) where T : class
        {
            RegisterConfig(typeof(T), configFilePath);
        }

        public void RegisterConfig(Type configManagerType, string configFilePath)
        {
            ConfigManagerType = configManagerType;
            Config = new ConfigService(configFilePath);

            foreach(var handler in OnConfigRegister.GetInvocationListOrEmpty())
            {
                try
                {
                    handler.Invoke(Config);
                }
                catch (Exception ex)
                {
                    LogDatabase.Error($"Error invoking {nameof(OnConfigRegister)} handler.", ex, string.Empty, string.Empty, 0);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeLogWriter();

                LogDatabase?.Dispose();
                LogDatabase = null;
                LogWriter?.Flush(true);
                LogWriter?.Dispose();
                LogWriter = null;
                Config?.Dispose();
                Config = null;

                OnLogWriterRegister = null;
                OnConfigRegister = null;
            }
            catch { }
        }

        private void UnsubscribeLogWriter()
        {
            if (!_logWriterSubscribed || LogDatabase == null)
                return;

            LogDatabase.OnLogAdded -= OnLogAdded;
            LogDatabase.OnLogRepeated -= OnLogRepeated;

            _logWriterSubscribed = false;
        }

        private void OnLogAdded(LogEntry log)
        {
            LogWriter?.Log(log);
        }

        private void OnLogRepeated(LogEntry log)
        {
            LogWriter?.Log(log);
        }
    }
}
