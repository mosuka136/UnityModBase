using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;

namespace UnityModBase
{
    public class ServiceRegistry : IDisposable
    {
        private static readonly Dictionary<string, ServiceRegistry> _services = new Dictionary<string, ServiceRegistry>();

        public static IEnumerable<string> ServiceKeys => _services.Keys;
        public static IEnumerable<ServiceRegistry> Services => _services.Values;

        public static event Action<ServiceRegistry> OnServiceRegistered;

        public string Key { get; }
        public string Name { get; }
        public LogDatabase LogDatabase { get; private set; }
        public LogWriter LogWriter { get; private set; }
        public ConfigService Config { get; private set; }
        public Type ConfigManagerType { get; private set; }

        private ServiceRegistry(string key, string name)
        {
            if (string.IsNullOrEmpty(key))
                key = $"Service_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            while (_services.ContainsKey(key))
                key += $"_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            Key = key;
            Name = name ?? string.Empty;

            LogDatabase = new LogDatabase(UnityProvider.Instance);
        }

        public static ServiceRegistry Register(string key, string name)
        {
            var service = new ServiceRegistry(key, name);
            _services.Add(service.Key, service);
            foreach (var handler in (OnServiceRegistered?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<ServiceRegistry>>())
            {
                try { handler.Invoke(service); }
                catch { }
            }
            return service;
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
                _services.Remove(Key);
            }
            catch { }
        }
    }
}
