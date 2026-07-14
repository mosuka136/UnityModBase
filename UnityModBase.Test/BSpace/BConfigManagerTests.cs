using System.Reflection;
using UnityModBase.BSpace;
using UnityModBase.HLogSpace;
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

        private sealed class BConfigManagerStateScope : IDisposable
        {
            private static readonly Type BConfigManagerType = typeof(BConfigManager);
            private static readonly Type BServiceType = typeof(BService);
            private static readonly FieldInfo InitializedField = GetRequiredField(BConfigManagerType, "_initialized");
            private static readonly PropertyInfo ContextProperty = GetRequiredProperty(BServiceType, "Context");

            private readonly bool _originalInitialized;
            private readonly UserContext _originalContext;

            private BConfigManagerStateScope(bool originalInitialized, UserContext originalContext)
            {
                _originalInitialized = originalInitialized;
                _originalContext = originalContext;
            }

            public static BConfigManagerStateScope Create()
            {
                var scope = new BConfigManagerStateScope(
                    (bool)InitializedField.GetValue(null),
                    (UserContext)ContextProperty.GetValue(null));

                InitializedField.SetValue(null, false);
                ContextProperty.SetValue(null, CreateContextWithoutConfigService());

                return scope;
            }

            public void Dispose()
            {
                InitializedField.SetValue(null, _originalInitialized);
                ContextProperty.SetValue(null, _originalContext);
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

            private static UserContext CreateContextWithoutConfigService()
            {
                var service = new UserService("UnityModBase.Test");
                typeof(UserService)
                    .GetProperty(nameof(UserService.LogDatabase), BindingFlags.Instance | BindingFlags.Public)
                    .SetValue(service, new LogDatabase(null));

                return new UserContext("UnityModBase.Test", "UnityModBase.Test")
                {
                    Service = service
                };
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
        }
    }
}
