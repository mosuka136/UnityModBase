using System.Reflection;
using UnityModBase.BSpace;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
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
            var reloadConfigHotkey = BConfigManager.ReloadConfigHotkey;
            var enableLog = BConfigManager.EnableLog;
            var logLevel = BConfigManager.LogLevel;

            Assert.True(scope.IsInitialized());
            Assert.True(config.SaveOnConfigSet);
            Assert.Equal(new[] { "General", "Hotkey", "Log" }, config.Sheet.Keys);
            Assert.Equal(6, config.Sheet.Values.Sum(table => table.Count()));
            Assert.Equal(LanguageType.English, setLanguage.Value);
            Assert.Equal("F1", configUiHotkey.Value.ToString());
            Assert.Equal("F2", logUiHotkey.Value.ToString());
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

            var log = Assert.Single(scope.LogDatabase.Logs.Where(log => log.Message == "Config file reloaded."));
            Assert.Equal(LogLevel.Info, log.Level);
            Assert.DoesNotContain(scope.LogDatabase.Logs, log => log.Message == "Failed to reload config file.");
        }

        [Fact]
        public void ReloadConfig_WhenReloadFails_LogsFailureAtErrorLevel()
        {
            using var scope = BConfigManagerStateScope.CreateWithConfigService();
            scope.Initialize(scope.ConfigFilePath);
            File.WriteAllText(scope.ConfigFilePath, string.Empty);

            scope.ReloadConfig();

            var log = Assert.Single(scope.LogDatabase.Logs.Where(log => log.Message == "Failed to reload config file."));
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.DoesNotContain(scope.LogDatabase.Logs, log => log.Message == "Config file reloaded.");
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
            private static readonly PropertyInfo[] StaticProperties =
            {
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.Config)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.EnableLog)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.LogLevel)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.ConfigUIHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.LogUIHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.ReloadConfigHotkey)),
                GetRequiredProperty(BConfigManagerType, nameof(BConfigManager.SetLanguage))
            };

            private readonly bool _originalInitialized;
            private readonly UserContext _originalContext;
            private readonly object[] _originalStaticPropertyValues;
            private readonly Action _originalFrameUpdateHandlers;
            private readonly LanguageType _originalDefaultLanguage;
            private readonly EventHandler<LanguageType> _originalDefaultLanguageHandlers;
            private readonly UserContext _testContext;
            private readonly string _tempDirectory;

            private BConfigManagerStateScope(
                bool originalInitialized,
                UserContext originalContext,
                object[] originalStaticPropertyValues,
                Action originalFrameUpdateHandlers,
                LanguageType originalDefaultLanguage,
                EventHandler<LanguageType> originalDefaultLanguageHandlers,
                UserContext testContext,
                string tempDirectory)
            {
                _originalInitialized = originalInitialized;
                _originalContext = originalContext;
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
                _testContext?.Dispose();

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

                var testContext = new UserContext("UnityModBase.Test", "UnityModBase.Test")
                {
                    Service = service
                };
                Action sentinelFrameUpdateHandler = () => { };
                var scope = new BConfigManagerStateScope(
                    (bool)InitializedField.GetValue(null),
                    (UserContext)ContextProperty.GetValue(null),
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
