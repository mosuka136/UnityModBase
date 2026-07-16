using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HUserSpace
{
    /// <summary>
    /// 持有单个用户的内存日志数据库、可选文件写入器和可选配置服务，并负责它们之间的事件接线与释放。
    /// 重复注册日志会替换旧写入器；配置注册则直接替换当前引用，调用方应避免在同一实例上并发或重复注册配置。
    /// </summary>
    /// <remarks>该类型没有内部生命周期锁，注册、日志事件转发与释放应由调用方串行化。</remarks>
    public class UserService : IDisposable
    {
        // 防止替换写入器或释放时重复退订数据库事件；它不是跨线程同步标记。
        private bool _logWriterSubscribed = false;

        /// <summary>
        /// 新日志写入器建立且历史日志回放、数据库订阅完成后触发；订阅者异常会记录到当前日志数据库。
        /// </summary>
        public event Action<LogWriter> OnLogWriterRegister;

        /// <summary>
        /// 新配置服务完成构造和初始读取后触发；订阅者异常会记录到当前日志数据库。
        /// </summary>
        public event Action<ConfigService> OnConfigRegister;

        /// <summary>
        /// 此服务所属的非空白用户标识。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 用户的内存日志库；构造时创建，释放后为 <c>null</c>。
        /// </summary>
        public LogDatabase LogDatabase { get; private set; }

        /// <summary>
        /// 当前文件日志写入器；尚未注册、注册替换前或释放后可为 <c>null</c>。
        /// </summary>
        public LogWriter LogWriter { get; private set; }

        /// <summary>
        /// 当前配置服务；尚未注册或释放后为 <c>null</c>。
        /// </summary>
        public ConfigService Config { get; private set; }

        /// <summary>
        /// 声明当前配置项的管理器类型。用于关联配置所有者，释放时不会复位该元数据。
        /// </summary>
        public Type ConfigManagerType { get; private set; }

        /// <summary>
        /// 创建用户服务及其内存日志数据库。
        /// </summary>
        /// <param name="userId">非空白的用户标识。</param>
        /// <exception cref="ArgumentException"><paramref name="userId"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        public UserService(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or whitespace.", nameof(userId));

            UserId = userId;
            LogDatabase = new LogDatabase(UnityProvider.Instance);
        }

        /// <summary>
        /// 替换当前文件日志写入器，先回放数据库快照，再订阅后续新增和重复日志。
        /// 旧写入器会先解除订阅并释放；底层文件创建失败由 <see cref="LogWriter"/> 容错，不向此方法传播。
        /// </summary>
        /// <param name="directory">日志目录，不可为空白。</param>
        /// <param name="fileName">日志文件基础名，不可为空白；写入器会追加小时级时间后缀并使用 <c>.log</c> 扩展名。</param>
        /// <param name="level">最低写入等级，低于该等级的条目只保留在内存数据库中。</param>
        /// <exception cref="ArgumentException"><paramref name="directory"/> 或 <paramref name="fileName"/> 为空白时抛出。</exception>
        public void RegisterLog(string directory, string fileName, LogLevel level)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));

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

        /// <summary>
        /// 以泛型类型作为配置管理器标识，创建并注册配置服务。
        /// </summary>
        /// <typeparam name="T">配置管理器类型，仅作为元数据保存，不会被实例化。</typeparam>
        /// <param name="configFilePath">配置文件路径，不可为 <c>null</c> 或空字符串。</param>
        /// <exception cref="ArgumentException"><paramref name="configFilePath"/> 为 <c>null</c> 或空字符串时抛出。</exception>
        public void RegisterConfig<T>(string configFilePath) where T : class
        {
            RegisterConfig(typeof(T), configFilePath);
        }

        /// <summary>
        /// 创建配置服务并立即读取指定文件，然后替换当前配置引用并派发注册事件。
        /// 当前已有配置服务不会在替换前自动释放。
        /// </summary>
        /// <param name="configManagerType">声明配置项的管理器类型，不可为 <c>null</c>。</param>
        /// <param name="configFilePath">配置文件路径，不可为 <c>null</c> 或空字符串；仅空白字符串会交由配置服务处理。</param>
        /// <exception cref="ArgumentException"><paramref name="configFilePath"/> 为 <c>null</c> 或空字符串时抛出。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="configManagerType"/> 为 <c>null</c> 时抛出。</exception>
        public void RegisterConfig(Type configManagerType, string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath))
                throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

            ConfigManagerType = configManagerType ?? throw new ArgumentNullException(nameof(configManagerType));
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

        /// <summary>
        /// 解除日志数据库订阅并尽力释放日志数据库、文件写入器和当前配置服务，然后清空本实例事件订阅者。
        /// 释放异常会被忽略；<see cref="ConfigManagerType"/> 保留原值。
        /// </summary>
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
