using System;
using System.Collections.Generic;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HUserSpace
{
    /// <summary>
    /// 持有单个用户的内存日志数据库、可选文件写入器和可选配置服务，并协调日志转发、配置模型事件接线与资源释放。
    /// 重复注册日志会释放旧写入器；重复注册配置只替换当前引用，不释放旧服务，且仅重新挂接配置创建前暂存的处理器。
    /// 本类型不负责声明配置表或配置项，也不负责配置界面投影。
    /// </summary>
    /// <remarks>该类型没有内部生命周期锁，注册、事件订阅、日志转发与释放应由调用方串行化。</remarks>
    public class UserService : IDisposable
    {
        // 防止替换写入器或释放时重复退订数据库事件；它不是跨线程同步标记。
        private bool _logWriterSubscribed = false;

        // 暂存配置服务创建前添加的模型变化处理器；每次 RegisterConfig 都会把仍在列表中的处理器挂到新服务。
        // 该列表跨配置替换和 Dispose 保留，用于迁移早期订阅，不代表当前服务上的完整订阅集合。
        private readonly List<Action> _configChangedList = new List<Action>();

        /// <summary>
        /// 观察当前或随后创建的配置服务所发布的模型变化。
        /// </summary>
        /// <remarks>
        /// <see cref="Config"/> 尚未创建时，添加的处理器会暂存并由 <see cref="RegisterConfig(Type, string)"/> 挂接；
        /// 配置服务已存在时，添加的处理器只直接挂到当时的服务，不会加入待迁移列表。
        /// 移除操作会同时尝试从当前配置服务和暂存列表退订，但不会触及已被替换的旧服务。
        /// 配置服务构造期间的首次读取发生在暂存处理器挂接之前，因此不会通过本事件通知这些处理器。
        /// 事件的执行顺序和异常隔离规则由 <see cref="ConfigService.OnConfigChanged"/> 定义。
        /// </remarks>
        public event Action OnConfigChanged
        {
            add
            {
                if (Config != null)
                    Config.OnConfigChanged += value;
                else
                    _configChangedList.Add(value);
            }
            remove
            {
                if (Config != null)
                    Config.OnConfigChanged -= value;
                _configChangedList.Remove(value);
            }
        }

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
        /// 替换当前文件日志写入器，在数据库变更暂停期间回放一致快照并订阅后续新增和重复日志。
        /// 旧写入器会先解除订阅并释放；底层文件创建失败由 <see cref="LogWriter"/> 容错，不向此方法传播。
        /// </summary>
        /// <param name="directory">日志目录，不可为空白。</param>
        /// <param name="fileName">日志文件基础名，不可为空白；写入器会追加小时级时间后缀并使用 <c>.log</c> 扩展名。</param>
        /// <param name="level">最低写入等级，低于该等级的条目只保留在内存数据库中。</param>
        /// <remarks>
        /// 快照与新订阅之间不存在遗漏窗口；但本类型不串行化整个替换过程，调用方仍不应让本方法与释放或另一次注册并发执行。
        /// </remarks>
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
            LogDatabase.SubscribeWithSnapshot(InitializeLogWriter, OnLogAdded, null, OnLogRepeated);

            _logWriterSubscribed = true;
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
        /// 创建配置服务并立即读取指定文件，然后替换当前配置引用。
        /// 创建完成后，把配置服务创建前暂存的模型变化处理器挂到新服务；本方法本身不发布配置注册或变化事件。
        /// </summary>
        /// <param name="configManagerType">声明配置项的管理器类型，不可为 <c>null</c>。</param>
        /// <param name="configFilePath">配置文件路径，不可为 <c>null</c> 或空字符串；仅空白字符串会交由配置服务处理。</param>
        /// <exception cref="ArgumentException"><paramref name="configFilePath"/> 为 <c>null</c> 或空字符串时抛出。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="configManagerType"/> 为 <c>null</c> 时抛出。</exception>
        /// <remarks>
        /// 当前已有配置服务不会在替换前自动释放。直接订阅旧服务或在旧服务存在期间通过 <see cref="OnConfigChanged"/> 添加的处理器
        /// 不会迁移到新服务；只有配置创建前进入暂存列表的处理器会再次挂接。
        /// </remarks>
        public void RegisterConfig(Type configManagerType, string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath))
                throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

            ConfigManagerType = configManagerType ?? throw new ArgumentNullException(nameof(configManagerType));
            Config = new ConfigService(configFilePath);

            foreach (var handler in _configChangedList)
                Config.OnConfigChanged += handler;
        }

        /// <summary>
        /// 解除日志数据库订阅并尽力释放日志数据库、文件写入器和当前配置服务。
        /// 当前配置服务会清空其模型变化订阅；配置服务创建前暂存的处理器列表和 <see cref="ConfigManagerType"/> 保留原值。
        /// 释放异常会被忽略。释放后的实例不应再次注册配置，否则暂存处理器会重新挂接。
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
            }
            catch { }
        }

        private void UnsubscribeLogWriter()
        {
            if (!_logWriterSubscribed || LogDatabase == null)
                return;

            LogDatabase.Unsubscribe(OnLogAdded, null, OnLogRepeated);

            _logWriterSubscribed = false;
        }

        private void InitializeLogWriter(IReadOnlyList<LogEntry> logs)
        {
            // 数据库在整批回放结束前阻止后续提交，确保文件先接收快照，再接收实时新增和重复通知。
            foreach (var log in logs)
                LogWriter.Log(log);
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
