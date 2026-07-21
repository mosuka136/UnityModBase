using System.Reflection;
using UnityModBase.BSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test
{
    public class UnityModBaseInitializationTests
    {
        [Fact]
        public void Initialize_WhenBaseDirectoryIsNull_RethrowsAndDoesNotMarkInitialized()
        {
            using var scope = UnityModBaseInitializationStateScope.Create();

            Assert.Throws<ArgumentNullException>(() => global::UnityModBase.UnityModBase.Initialize(null));

            Assert.False(scope.IsUnityModBaseInitialized());
            Assert.False(scope.IsBServiceInitialized());
            Assert.False(scope.IsGameBootRegisteryInitialized());
        }

        private sealed class UnityModBaseInitializationStateScope : IDisposable
        {
            private static readonly Type UnityModBaseType = typeof(global::UnityModBase.UnityModBase);
            private static readonly Type BServiceType = typeof(BService);
            private static readonly Type UserManagerType = typeof(UserManager);
            private static readonly FieldInfo UnityModBaseInitializedField = GetRequiredField(UnityModBaseType, "_initialized");
            private static readonly FieldInfo BServiceInitializedField = GetRequiredField(BServiceType, "_initialized");
            private static readonly FieldInfo GameBootRegisteryInitializedField = GetRequiredField(typeof(GameBootRegistry), "_initialized");
            private static readonly FieldInfo UserContextsField = GetRequiredField(UserManagerType, "_userContexts");
            private static readonly FieldInfo OnUserRegisteredField = GetRequiredField(UserManagerType, nameof(UserManager.OnUserRegistered));
            private static readonly PropertyInfo BServiceContextProperty = GetRequiredProperty(BServiceType, "Context");

            private readonly bool _originalUnityModBaseInitialized;
            private readonly bool _originalBServiceInitialized;
            private readonly bool _originalGameBootRegisteryInitialized;
            private readonly UserContext _originalBServiceContext;
            private readonly Dictionary<string, UserContext> _originalUserContexts;
            private readonly Action<UserContext> _originalOnUserRegistered;

            private UnityModBaseInitializationStateScope(
                bool originalUnityModBaseInitialized,
                bool originalBServiceInitialized,
                bool originalGameBootRegisteryInitialized,
                UserContext originalBServiceContext,
                Dictionary<string, UserContext> originalUserContexts,
                Action<UserContext> originalOnUserRegistered)
            {
                _originalUnityModBaseInitialized = originalUnityModBaseInitialized;
                _originalBServiceInitialized = originalBServiceInitialized;
                _originalGameBootRegisteryInitialized = originalGameBootRegisteryInitialized;
                _originalBServiceContext = originalBServiceContext;
                _originalUserContexts = originalUserContexts;
                _originalOnUserRegistered = originalOnUserRegistered;
            }

            public static UnityModBaseInitializationStateScope Create()
            {
                var scope = new UnityModBaseInitializationStateScope(
                    (bool)UnityModBaseInitializedField.GetValue(null),
                    (bool)BServiceInitializedField.GetValue(null),
                    (bool)GameBootRegisteryInitializedField.GetValue(null),
                    (UserContext)BServiceContextProperty.GetValue(null),
                    GetUserContexts().ToDictionary(pair => pair.Key, pair => pair.Value),
                    (Action<UserContext>)OnUserRegisteredField.GetValue(null));

                UnityModBaseInitializedField.SetValue(null, false);
                BServiceInitializedField.SetValue(null, false);
                GameBootRegisteryInitializedField.SetValue(null, false);
                BServiceContextProperty.SetValue(null, null);
                GetUserContexts().Clear();
                OnUserRegisteredField.SetValue(null, new Action<UserContext>(UseTestLogDatabase));

                return scope;
            }

            public void Dispose()
            {
                var contexts = GetUserContexts();
                foreach (var context in contexts.Values)
                {
                    if (!_originalUserContexts.Values.Contains(context))
                        context.Dispose();
                }

                contexts.Clear();
                foreach (var pair in _originalUserContexts)
                    contexts.Add(pair.Key, pair.Value);

                OnUserRegisteredField.SetValue(null, _originalOnUserRegistered);
                BServiceContextProperty.SetValue(null, _originalBServiceContext);
                GameBootRegisteryInitializedField.SetValue(null, _originalGameBootRegisteryInitialized);
                BServiceInitializedField.SetValue(null, _originalBServiceInitialized);
                UnityModBaseInitializedField.SetValue(null, _originalUnityModBaseInitialized);
            }

            public bool IsUnityModBaseInitialized()
            {
                return (bool)UnityModBaseInitializedField.GetValue(null);
            }

            public bool IsBServiceInitialized()
            {
                return (bool)BServiceInitializedField.GetValue(null);
            }

            public bool IsGameBootRegisteryInitialized()
            {
                return (bool)GameBootRegisteryInitializedField.GetValue(null);
            }

            private static Dictionary<string, UserContext> GetUserContexts()
            {
                return (Dictionary<string, UserContext>)UserContextsField.GetValue(null);
            }

            private static void UseTestLogDatabase(UserContext context)
            {
                typeof(UserService)
                    .GetProperty(nameof(UserService.LogDatabase), BindingFlags.Instance | BindingFlags.Public)
                    .SetValue(context.Service, new LogDatabase(null));
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
