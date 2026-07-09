using System.Reflection;
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

            var exception = Assert.Throws<TargetInvocationException>(() => scope.Initialize("missing.cfg"));

            Assert.IsType<NullReferenceException>(exception.InnerException);
            Assert.False(scope.IsInitialized());
            Assert.Null(scope.GetStaticProperty("Config"));
            Assert.Null(scope.GetStaticProperty("EnableLog"));
            Assert.Null(scope.GetStaticProperty("LogLevel"));
        }

        private sealed class BConfigManagerStateScope : IDisposable
        {
            private static readonly Assembly UnityModBaseAssembly = typeof(global::UnityModBase.UnityModBase).Assembly;
            private static readonly Type BConfigManagerType = UnityModBaseAssembly.GetType("UnityModBase.BSpace.BConfigManager", true);
            private static readonly Type BServiceType = UnityModBaseAssembly.GetType("UnityModBase.BSpace.BService", true);
            private static readonly FieldInfo InitializedField = GetRequiredField(BConfigManagerType, "_initialized");
            private static readonly MethodInfo InitializeMethod = GetRequiredMethod(BConfigManagerType, "Initialize");
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
                InitializeMethod.Invoke(null, new object[] { configFilePath });
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

            private static MethodInfo GetRequiredMethod(Type type, string methodName)
            {
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    throw new InvalidOperationException($"Method '{methodName}' was not found on {type.FullName}.");
                }

                return method;
            }

            private static PropertyInfo GetRequiredProperty(Type type, string propertyName)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
                if (property == null)
                {
                    throw new InvalidOperationException($"Property '{propertyName}' was not found on {type.FullName}.");
                }

                return property;
            }
        }
    }
}
