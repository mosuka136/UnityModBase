using System.Reflection;
using Moq;
using UnityModBase.BSpace;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.BSpace
{
    public class BConfigManagerTests
    {
        [Fact]
        public void Initialize_WhenConfigServiceIsMissing_RethrowsAndResetsState()
        {
            using var scope = BConfigManagerStateScope.Create();

            Assert.Throws<NullReferenceException>(() => scope.Initialize("missing.cfg"));

            Assert.False(scope.IsInitialized());
            Assert.Null(scope.GetStaticProperty("Config"));
            Assert.Null(BConfigManager.EnableLog);
            Assert.Null(BConfigManager.LogLevel);
        }

        [Fact]
        public void Initialize_WithConfigService_BindsDefaultsPersistsIsIdempotentAndDisposeResetsState()
        {
            // Arrange
            using var scope = BConfigManagerStateScope.CreateWithConfigService();

            // Act
            scope.Initialize(scope.ConfigFilePath);

            // Assert
            var config = Assert.IsType<ConfigService>(scope.GetStaticProperty(nameof(BConfigManager.Config)));
            var setLanguage = BConfigManager.SetLanguage;
            var configUiHotkey = BConfigManager.ConfigUIHotkey;
            var logUiHotkey = BConfigManager.LogUIHotkey;
            var controlUiHotkey = BConfigManager.ControlUIHotkey;
            var reloadConfigHotkey = BConfigManager.ReloadConfigHotkey;
            var enableLog = BConfigManager.EnableLog;
            var logLevel = BConfigManager.LogLevel;

            Assert.True(scope.IsInitialized());
            Assert.True(config.SaveOnConfigSet);
            Assert.Equal(new[] { "General", "Hotkey", "Log" }, config.Sheet.Keys);
            Assert.Equal(7, config.Sheet.Values.Sum(table => table.Count()));
            Assert.Equal(LanguageType.English, setLanguage.Value);
            Assert.Equal("F1", configUiHotkey.Value.ToString());
            Assert.Equal("F2", logUiHotkey.Value.ToString());
            Assert.Equal("F3", controlUiHotkey.Value.ToString());
            Assert.Equal("Ctrl+R", reloadConfigHotkey.Value.ToString());
            Assert.True(enableLog.Value);
            Assert.Equal(LogLevel.Info, logLevel.Value);
            Assert.Equal(LanguageType.English, Translator.DefaultLanguage);
            Assert.Equal(2, scope.GetFrameUpdateHandlers().Length);
            Assert.Contains(scope.SentinelFrameUpdateHandler, scope.GetFrameUpdateHandlers());

            var persistedContent = File.ReadAllText(scope.ConfigFilePath);
            Assert.Contains("[General]", persistedContent);
            Assert.Contains(nameof(BConfigManager.SetLanguage), persistedContent);
            Assert.Contains(nameof(BConfigManager.ConfigUIHotkey), persistedContent);
            Assert.Contains(nameof(BConfigManager.LogUIHotkey), persistedContent);
            Assert.Contains(nameof(BConfigManager.ControlUIHotkey), persistedContent);
            Assert.Contains(nameof(BConfigManager.ReloadConfigHotkey), persistedContent);
            Assert.Contains(nameof(BConfigManager.EnableLog), persistedContent);
            Assert.Contains(nameof(BConfigManager.LogLevel), persistedContent);

            setLanguage.Value = LanguageType.Chinese;
            Assert.Equal(LanguageType.Chinese, Translator.DefaultLanguage);

            scope.Initialize(scope.AlternateConfigFilePath);

            Assert.Same(config, BConfigManager.Config);
            Assert.Same(setLanguage, BConfigManager.SetLanguage);
            Assert.Same(configUiHotkey, BConfigManager.ConfigUIHotkey);
            Assert.Same(logUiHotkey, BConfigManager.LogUIHotkey);
            Assert.Same(controlUiHotkey, BConfigManager.ControlUIHotkey);
            Assert.Same(reloadConfigHotkey, BConfigManager.ReloadConfigHotkey);
            Assert.Same(enableLog, BConfigManager.EnableLog);
            Assert.Same(logLevel, BConfigManager.LogLevel);
            Assert.False(File.Exists(scope.AlternateConfigFilePath));

            // Act
            BConfigManager.Dispose();
            Translator.DefaultLanguage = LanguageType.None;
            setLanguage.Value = LanguageType.English;

            // Assert
            Assert.False(scope.IsInitialized());
            Assert.Null(scope.GetStaticProperty(nameof(BConfigManager.Config)));
            Assert.Null(BConfigManager.EnableLog);
            Assert.Null(BConfigManager.LogLevel);
            Assert.Null(BConfigManager.ConfigUIHotkey);
            Assert.Null(BConfigManager.LogUIHotkey);
            Assert.Null(BConfigManager.ControlUIHotkey);
            Assert.Null(BConfigManager.ReloadConfigHotkey);
            Assert.Null(BConfigManager.SetLanguage);
            Assert.Equal(LanguageType.None, Translator.DefaultLanguage);
            Assert.Equal(new[] { scope.SentinelFrameUpdateHandler }, scope.GetFrameUpdateHandlers());
        }

        [Fact]
        public void ReloadConfig_WhenReloadSucceeds_LogsSuccessAtInfoLevel()
        {
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);

            scope.ReloadConfig();

            var log = Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{scope.ConfigFilePath}'."));
            Assert.Equal(LogLevel.Info, log.Level);
            Assert.DoesNotContain(scope.LogDatabase.Logs, log =>
                log.Message.StartsWith("Failed to reload config file.", StringComparison.Ordinal));
        }

        [Fact]
        public void ReloadConfig_WhenReloadFails_LogsFailureAtErrorLevel()
        {
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);
            File.WriteAllText(scope.ConfigFilePath, string.Empty);

            scope.ReloadConfig();

            var log = Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Failed to reload config file. Path='{scope.ConfigFilePath}'. See earlier diagnostics for the failing stage."));
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.DoesNotContain(scope.LogDatabase.Logs, log =>
                log.Message.StartsWith("Config file reloaded.", StringComparison.Ordinal));
        }

        [Fact]
        public void ReloadConfig_WithMultipleRegisteredUsers_ReloadsEveryUsersConfig()
        {
            // Arrange：重载热键面向注册表中的全部用户，而不只是框架自身的配置服务。
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);
            var otherEntry = RegisterUserWithReloadableEntry(scope, "OtherUser", out var otherConfigPath);

            // Act
            scope.ReloadConfig();

            // Assert
            Assert.Equal(7, otherEntry.Value);
            var frameworkLog = Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{scope.ConfigFilePath}'."));
            var otherLog = Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{otherConfigPath}'."));
            Assert.Equal(LogLevel.Info, frameworkLog.Level);
            Assert.Equal(LogLevel.Info, otherLog.Level);
        }

        [Fact]
        public void ReloadConfig_WhenOneUserFailsToReload_LogsFailureAndContinuesWithRemainingUsers()
        {
            // Arrange：框架用户注册在先且其配置文件损坏；单个用户失败不得中断其余用户的重载。
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);
            var otherEntry = RegisterUserWithReloadableEntry(scope, "OtherUser", out var otherConfigPath);
            File.WriteAllText(scope.ConfigFilePath, string.Empty);

            // Act
            scope.ReloadConfig();

            // Assert
            Assert.Equal(7, otherEntry.Value);
            var failureLog = Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Failed to reload config file. Path='{scope.ConfigFilePath}'. See earlier diagnostics for the failing stage."));
            Assert.Equal(LogLevel.Error, failureLog.Level);
            Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{otherConfigPath}'."));
        }

        [Fact]
        public void ReloadConfig_WhenRegisteredUserHasNoConfig_SkipsItAndReloadsRemainingUsers()
        {
            // 契约：注册表允许存在尚未登记配置文件的用户（UserService.Config 为 null），
            // 与 GuiHost 为无配置用户挂载空绑定树的处理一致；重载应跳过这类用户并继续处理其余用户。
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);
            scope.RegisterAdditionalUser("NoConfigUser");
            var otherEntry = RegisterUserWithReloadableEntry(scope, "OtherUser", out var otherConfigPath);

            // Act
            var exception = Record.Exception(() => scope.ReloadConfig());

            // Assert
            Assert.Null(exception);
            Assert.Equal(7, otherEntry.Value);
            Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{otherConfigPath}'."));
            Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{scope.ConfigFilePath}'."));
        }

        [Fact]
        public void ReloadConfigOnUserOrder_WhenReloadThrows_LogsUnexpectedErrorAndDoesNotPropagate()
        {
            // Arrange：重载遍历的注册表是实时视图，遍历期间的新用户注册会使枚举失效
            //（框架配置重载完成的 OnConfigChanged 里注册迟到用户），由此抛出的异常
            // 必须由帧回调捕获记录，不得传播给帧更新派发器。
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);
            var config = Assert.IsType<ConfigService>(scope.GetStaticProperty(nameof(BConfigManager.Config)));
            config.OnConfigChanged += RegisterLateUserDuringReload;

            // 用报告“本帧已按下”的替身组合替换重载热键的组合，使帧回调进入重载分支；
            // ToString 须与文件中的规范化文本一致，重载准备阶段的等值比较会按组合文本对比新旧热键。
            var pressedChord = new Mock<IHotkeyChord>(MockBehavior.Strict);
            pressedChord.Setup(chord => chord.WasPressedThisFrame()).Returns(true);
            pressedChord.Setup(chord => chord.ToString()).Returns("Ctrl+R");
            var hotkey = BConfigManager.ReloadConfigHotkey.Value;
            hotkey.Hotkeys.Clear();
            hotkey.Hotkeys.Add(new HotkeyChord(pressedChord.Object, UnityProvider.Instance));
            var frameHandler = scope.GetFrameUpdateHandlers().Single(handler =>
                handler.Method.DeclaringType == typeof(BConfigManager)
                && handler.Method.Name == "ReloadConfigOnUserOrder");

            // Act
            var exception = Record.Exception(() => frameHandler());

            // Assert
            Assert.Null(exception);
            // 中断发生在框架自身重载完成之后：成功日志已保留，异常只阻断其后的用户。
            Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == $"Config file reloaded. Path='{scope.ConfigFilePath}'."));
            var failureLog = Assert.Single(scope.LogDatabase.Logs.Where(log =>
                log.Message == "Unexpected error while processing the config reload hotkey."));
            Assert.Equal(LogLevel.Error, failureLog.Level);
            Assert.IsType<InvalidOperationException>(failureLog.Exception);

            config.OnConfigChanged -= RegisterLateUserDuringReload;
        }

        // 框架配置重载完成时向注册表追加用户，模拟重载遍历期间的并发注册，使注册表枚举失效。
        private static void RegisterLateUserDuringReload()
        {
            UserManager.CreateUser("LateUser", new Translator("迟到用户", "Late User"));
        }

        // 为附加用户登记一个含单个整型项的配置文件，并把磁盘值改写为 7；返回该配置项用于断言重载是否生效。
        private static ConfigEntry<int> RegisterUserWithReloadableEntry(
            BConfigManagerStateScope scope,
            string userId,
            out string configPath)
        {
            configPath = scope.GetAdditionalUserConfigPath(userId);
            var user = scope.RegisterAdditionalUser(userId, configPath);
            var config = user.Service.Config;
            config.CreateTable("OtherTable", new Translator("其他表", "Other Table"));
            var entry = config.Bind(
                "OtherTable",
                "OtherKey",
                0,
                new Translator("其他项", "Other Entry"));
            File.WriteAllText(configPath, "[OtherTable]\nOtherKey = 7\n");
            return entry;
        }

        private sealed class BConfigManagerStateScope : IDisposable
        {
            private static readonly Type BConfigManagerType = typeof(BConfigManager);
            private static readonly Type BServiceType = typeof(BService);
            private static readonly FieldInfo InitializedField = GetRequiredField(BConfigManagerType, "_initialized");
            private static readonly PropertyInfo ContextProperty = GetRequiredProperty(BServiceType, "Context");
            private static readonly FieldInfo FrameUpdateHandlersField = GetRequiredField(typeof(FrameUpdateManager), nameof(FrameUpdateManager.OnFrameUpdate));
            private static readonly FieldInfo DefaultLanguageField = GetRequiredField(typeof(Translator), "_defaultLanguage");
            private static readonly FieldInfo DefaultLanguageHandlersField = GetRequiredField(typeof(Translator), nameof(Translator.OnDefaultLanguageChanged));
            private static readonly MethodInfo ReloadConfigMethod = GetRequiredMethod(BConfigManagerType, "ReloadConfig");
            private static readonly FieldInfo UserContextsField = GetRequiredField(typeof(UserManager), "_userContexts");
            private static readonly PropertyInfo[] StaticProperties =
            {
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.Config)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.EnableLog)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.LogLevel)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.ConfigUIHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.LogUIHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.ControlUIHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.ReloadConfigHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.SetLanguage))
            };

            private readonly bool _originalInitialized;
            private readonly UserContext _originalContext;
            // 隔离开始前 UserManager 注册表的内容快照；Dispose 时对同一个实时字典实例清空后回填，
            // 而非替换字段（内部注册表为 readonly，UserManager 也只暴露实时视图）。
            private readonly Dictionary<string, UserContext> _originalUserContexts;
            private readonly object[] _originalStaticPropertyValues;
            private readonly Action _originalFrameUpdateHandlers;
            private readonly LanguageType _originalDefaultLanguage;
            private readonly EventHandler<LanguageType> _originalDefaultLanguageHandlers;
            private readonly UserContext _testContext;
            private readonly string _tempDirectory;

            private BConfigManagerStateScope(
                bool originalInitialized,
                UserContext originalContext,
                Dictionary<string, UserContext> originalUserContexts,
                object[] originalStaticPropertyValues,
                Action originalFrameUpdateHandlers,
                LanguageType originalDefaultLanguage,
                EventHandler<LanguageType> originalDefaultLanguageHandlers,
                UserContext testContext,
                string tempDirectory)
            {
                _originalInitialized = originalInitialized;
                _originalContext = originalContext;
                _originalUserContexts = originalUserContexts;
                _originalStaticPropertyValues = originalStaticPropertyValues;
                _originalFrameUpdateHandlers = originalFrameUpdateHandlers;
                _originalDefaultLanguage = originalDefaultLanguage;
                _originalDefaultLanguageHandlers = originalDefaultLanguageHandlers;
                _testContext = testContext;
                _tempDirectory = tempDirectory;
            }

            public string ConfigFilePath { get; private set; }
            public string AlternateConfigFilePath => Path.Combine(_tempDirectory, "alternate.cfg");
            public Action SentinelFrameUpdateHandler { get; private set; }
            public LogDatabase LogDatabase => _testContext.Service.LogDatabase;

            public static BConfigManagerStateScope Create()
            {
                return CreateCore(withConfigService: false);
            }

            public static BConfigManagerStateScope CreateWithConfigService()
            {
                return CreateCore(withConfigService: true);
            }

            public void Dispose()
            {
                BConfigManager.Dispose();

                var userContexts = GetUserContexts();
                // 只释放测试期间新增的上下文：快照成员的所有权属于其他测试或宿主，不得代为释放；
                // 测试上下文跳过本循环，由下一行单独释放。
                foreach (var context in userContexts.Values)
                {
                    if (!_originalUserContexts.Values.Contains(context) && !ReferenceEquals(context, _testContext))
                        context.Dispose();
                }
                _testContext?.Dispose();
                // 注册表字典实例不可替换，只能清空后按快照回填，恢复隔离前的注册项。
                userContexts.Clear();
                foreach (var pair in _originalUserContexts)
                    userContexts.Add(pair.Key, pair.Value);

                for (var index = 0; index < StaticProperties.Length; index++)
                    StaticProperties[index].SetValue(null, _originalStaticPropertyValues[index]);

                FrameUpdateHandlersField.SetValue(null, _originalFrameUpdateHandlers);
                DefaultLanguageField.SetValue(null, _originalDefaultLanguage);
                DefaultLanguageHandlersField.SetValue(null, _originalDefaultLanguageHandlers);
                ContextProperty.SetValue(null, _originalContext);
                InitializedField.SetValue(null, _originalInitialized);

                if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
                    Directory.Delete(_tempDirectory, true);
            }

            public object GetStaticProperty(string propertyName)
            {
                return GetRequiredProperty(BConfigManagerType, propertyName).GetValue(null);
            }

            public void Initialize(string configFilePath)
            {
                BConfigManager.Initialize(configFilePath);
            }

            public bool IsInitialized()
            {
                return (bool)InitializedField.GetValue(null);
            }

            public void ReloadConfig()
            {
                ReloadConfigMethod.Invoke(null, null);
            }

            public Action[] GetFrameUpdateHandlers()
            {
                return ((Action)FrameUpdateHandlersField.GetValue(null))?.GetInvocationList().Cast<Action>().ToArray()
                    ?? Array.Empty<Action>();
            }

            public string GetAdditionalUserConfigPath(string userId)
            {
                return Path.Combine(_tempDirectory, $"{userId}.cfg");
            }

            /// <summary>
            /// 在 <see cref="UserManager"/> 注册表中追加一个附加用户；
            /// <paramref name="configFilePath"/> 为 <c>null</c> 时不登记配置文件，模拟仅有用户服务的注册项。
            /// 直接写入内部注册表而不经 <see cref="UserManager.Register"/>，
            /// 以避免触发静态的 <see cref="UserManager.OnUserRegistered"/> 事件和配置变化转发接线，维持测试隔离。
            /// </summary>
            public UserContext RegisterAdditionalUser(string userId, string configFilePath = null)
            {
                var service = CreateServiceWithTestLogDatabase();
                if (configFilePath != null)
                    service.RegisterConfig(typeof(BConfigManagerTests), configFilePath);

                var context = new UserContext(userId, new Translator(userId, userId))
                {
                    Service = service
                };
                GetUserContexts().Add(userId, context);
                return context;
            }

            // 取 UserManager 的内部注册表字典本身（非副本）；对它的增删立即反映到全部实时视图。
            private static Dictionary<string, UserContext> GetUserContexts()
            {
                return (Dictionary<string, UserContext>)UserContextsField.GetValue(null);
            }

            private static BConfigManagerStateScope CreateCore(bool withConfigService)
            {
                var originalStaticPropertyValues = StaticProperties
                    .Select(property => property.GetValue(null))
                    .ToArray();
                var tempDirectory = withConfigService
                    ? Path.Combine(Path.GetTempPath(), "UnityModBase.Test", Guid.NewGuid().ToString("N"))
                    : null;
                if (tempDirectory != null)
                    Directory.CreateDirectory(tempDirectory);

                var service = CreateServiceWithTestLogDatabase();
                var configFilePath = tempDirectory == null ? null : Path.Combine(tempDirectory, "UnityModBase.cfg");
                if (withConfigService)
                    service.RegisterConfig(typeof(BConfigManager), configFilePath);

                var testContext = new UserContext(
                    "UnityModBase.Test",
                    new Translator("UnityModBase.Test", "UnityModBase.Test"))
                {
                    Service = service
                };
                Action sentinelFrameUpdateHandler = () => { };
                // ReloadConfig 遍历 UserManager 注册表；测试把注册表清空后只放入测试上下文，形成隔离视图，
                // 释放夹具时先释放测试期间新增的上下文，再按快照回填同一个字典实例。
                var userContexts = GetUserContexts();
                var originalUserContexts = userContexts.ToDictionary(pair => pair.Key, pair => pair.Value);
                userContexts.Clear();
                userContexts.Add(testContext.UserId, testContext);
                var scope = new BConfigManagerStateScope(
                    (bool)InitializedField.GetValue(null),
                    (UserContext)ContextProperty.GetValue(null),
                    originalUserContexts,
                    originalStaticPropertyValues,
                    (Action)FrameUpdateHandlersField.GetValue(null),
                    (LanguageType)DefaultLanguageField.GetValue(null),
                    (EventHandler<LanguageType>)DefaultLanguageHandlersField.GetValue(null),
                    testContext,
                    tempDirectory)
                {
                    ConfigFilePath = configFilePath,
                    SentinelFrameUpdateHandler = sentinelFrameUpdateHandler
                };

                InitializedField.SetValue(null, false);
                foreach (var property in StaticProperties)
                    property.SetValue(null, null);
                ContextProperty.SetValue(null, testContext);
                FrameUpdateHandlersField.SetValue(null, sentinelFrameUpdateHandler);
                DefaultLanguageField.SetValue(null, LanguageType.Chinese);
                DefaultLanguageHandlersField.SetValue(null, null);

                return scope;
            }

            // UserService 默认以全局 UnityProvider 构造日志数据库（用于日志附帧号和场景名）；
            // setter 非公开，经反射替换为无提供器的纯内存实例，供测试通过 Logs 快照断言日志内容。
            private static UserService CreateServiceWithTestLogDatabase()
            {
                var service = new UserService("UnityModBase.Test");
                typeof(UserService)
                    .GetProperty(nameof(UserService.LogDatabase), BindingFlags.Instance | BindingFlags.Public)
                    .SetValue(service, new LogDatabase(null));
                return service;
            }

            private static FieldInfo GetRequiredField(Type type, string fieldName)
            {
                var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    throw new InvalidOperationException($"Field '{fieldName}' was not found on {type.FullName}.");
                }

                return field;
            }

            private static PropertyInfo GetRequiredProperty(Type type, string propertyName)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property == null)
                {
                    throw new InvalidOperationException($"Property '{propertyName}' was not found on {type.FullName}.");
                }

                return property;
            }

            private static MethodInfo GetRequiredMethod(Type type, string methodName)
            {
                var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                {
                    throw new InvalidOperationException($"Method '{methodName}' was not found on {type.FullName}.");
                }

                return method;
            }
        }
    }
}
