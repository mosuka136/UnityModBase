using System.Reflection;
using UnityEngine;

namespace UnityModBase.Test
{
    public class GameBootRegisteryTests
    {
        [Fact]
        public void RegisterComponentOnGameBoot_NonComponentType_PostfixInvokesOtherHandlersNormally()
        {
            // Arrange
            var invocationCount = 0;
            using var scope = GameBootRegisteryStateScope.Create();
            Action handler = () => invocationCount++;

            GameBootRegistry.OnGameBoot += handler;
            GameBootRegistry.RegisterComponentOnGameBoot(typeof(string));

            // Act
            GameBootRegistry.Boot();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void RegisterComponentOnGameBoot_AbstractComponentType_PostfixSwallowsComponentCreationFailure()
        {
            // Arrange
            var invocationCount = 0;
            using var scope = GameBootRegisteryStateScope.Create();
            Action handler = () => invocationCount++;

            GameBootRegistry.OnGameBoot += handler;
            GameBootRegistry.RegisterComponentOnGameBoot(typeof(AbstractTestComponent));

            // Act
            GameBootRegistry.Boot();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void RegisterComponentOnGameBoot_NullType_ReturnsFalseWithoutThrowing()
        {
            using var scope = GameBootRegisteryStateScope.Create();
            bool? wasRegistered = null;

            var exception = Record.Exception(() =>
                wasRegistered = GameBootRegistry.RegisterComponentOnGameBoot(null));

            Assert.Null(exception);
            Assert.Equal(false, wasRegistered);
            Assert.Null(scope.GetOnGameBoot());
        }

        [Fact]
        public void LoadScenePatch_Postfix_CalledTwiceInvokesCustomHandlerOnlyOnce()
        {
            // Arrange
            var invocationCount = 0;
            using var scope = GameBootRegisteryStateScope.Create();
            Action handler = () => invocationCount++;

            GameBootRegistry.OnGameBoot += handler;

            // Act
            GameBootRegistry.Boot();
            GameBootRegistry.Boot();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void Boot_WhenHandlerReentersBoot_StillInvokesOneShotHandlerOnlyOnce()
        {
            var invocationCount = 0;
            using var scope = GameBootRegisteryStateScope.Create();
            GameBootRegistry.OnGameBoot += () =>
            {
                invocationCount++;
                if (invocationCount == 1)
                    GameBootRegistry.Boot();
            };

            GameBootRegistry.Boot();

            Assert.Equal(1, invocationCount);
            Assert.Null(scope.GetOnGameBoot());
        }

        [Fact]
        public void RegisterMethodOnGameBoot_StaticParameterlessVoidMethod_InvokesMethod()
        {
            // Arrange
            var invocationCount = 0;
            using var registryScope = GameBootRegisteryStateScope.Create();
            using var targetScope = GameBootMethodTargetScope.Create();
            _validGameBootMethodInvoked = () => invocationCount++;
            var method = GetRequiredTestMethod(nameof(ValidGameBootMethod));

            // Act
            var wasRegistered = GameBootRegistry.RegisterMethodOnGameBoot(method);
            GameBootRegistry.Boot();

            // Assert
            Assert.True(wasRegistered);
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void RegisterMethodOnGameBoot_NullMethod_DoesNotPreventFollowingValidMethod()
        {
            // Arrange
            var validInvocationCount = 0;
            using var registryScope = GameBootRegisteryStateScope.Create();
            using var targetScope = GameBootMethodTargetScope.Create();
            _validGameBootMethodInvoked = () => validInvocationCount++;
            var validMethod = GetRequiredTestMethod(nameof(ValidGameBootMethod));

            // Act
            var wasRegistered = GameBootRegistry.RegisterMethodOnGameBoot(null);
            GameBootRegistry.RegisterMethodOnGameBoot(validMethod);
            GameBootRegistry.Boot();

            // Assert
            Assert.False(wasRegistered);
            Assert.Equal(1, validInvocationCount);
        }

        [Theory]
        [InlineData(nameof(InstanceGameBootMethod))]
        [InlineData(nameof(ParameterizedGameBootMethod))]
        [InlineData(nameof(NonVoidGameBootMethod))]
        public void RegisterMethodOnGameBoot_InvalidSignature_DoesNotInvokeMethodOrPreventFollowingValidMethod(
            string invalidMethodName)
        {
            // Arrange
            var invalidInvocationCount = 0;
            var validInvocationCount = 0;
            using var registryScope = GameBootRegisteryStateScope.Create();
            using var targetScope = GameBootMethodTargetScope.Create();
            _invalidGameBootMethodInvoked = () => invalidInvocationCount++;
            _validGameBootMethodInvoked = () => validInvocationCount++;
            var invalidMethod = GetRequiredTestMethod(invalidMethodName);
            var validMethod = GetRequiredTestMethod(nameof(ValidGameBootMethod));

            GameBootRegistry.RegisterMethodOnGameBoot(invalidMethod);
            GameBootRegistry.RegisterMethodOnGameBoot(validMethod);

            // Act
            GameBootRegistry.Boot();

            // Assert
            Assert.Equal(0, invalidInvocationCount);
            Assert.Equal(1, validInvocationCount);
        }

        [Fact]
        public void RegisterMethodOnGameBoot_MethodThrows_DoesNotPreventFollowingValidMethod()
        {
            // Arrange
            var throwingInvocationCount = 0;
            var validInvocationCount = 0;
            using var registryScope = GameBootRegisteryStateScope.Create();
            using var targetScope = GameBootMethodTargetScope.Create();
            _throwingGameBootMethodInvoked = () => throwingInvocationCount++;
            _validGameBootMethodInvoked = () => validInvocationCount++;
            var throwingMethod = GetRequiredTestMethod(nameof(ThrowingGameBootMethod));
            var validMethod = GetRequiredTestMethod(nameof(ValidGameBootMethod));

            GameBootRegistry.RegisterMethodOnGameBoot(throwingMethod);
            GameBootRegistry.RegisterMethodOnGameBoot(validMethod);

            // Act
            GameBootRegistry.Boot();

            // Assert
            Assert.Equal(1, throwingInvocationCount);
            Assert.Equal(1, validInvocationCount);
        }

        [Fact]
        public void Dispose_ClearsHandlersAndResetsInitialized()
        {
            // Arrange
            var invocationCount = 0;
            using var scope = GameBootRegisteryStateScope.Create();
            scope.SetInitialized(true);
            GameBootRegistry.OnGameBoot += () => invocationCount++;

            // Act
            GameBootRegistry.Dispose();
            GameBootRegistry.Boot();

            // Assert
            Assert.Equal(0, invocationCount);
            Assert.False(scope.IsInitialized());
            Assert.Null(scope.GetOnGameBoot());
        }

        [Fact]
        public void StateScope_NestedScope_ClearsAndRestoresScannedEntries()
        {
            using var outerScope = GameBootRegisteryStateScope.Create();
            var originalType = typeof(AbstractTestComponent);
            var temporaryType = typeof(string);
            var originalMethod = GetRequiredTestMethod(nameof(ValidGameBootMethod));
            var temporaryMethod = GetRequiredTestMethod(nameof(ThrowingGameBootMethod));

            Assert.True(outerScope.AddScannedType(originalType));
            Assert.True(outerScope.AddScannedMethod(originalMethod));

            using (GameBootRegisteryStateScope.Create())
            {
                Assert.False(outerScope.ContainsScannedType(originalType));
                Assert.False(outerScope.ContainsScannedMethod(originalMethod));
                Assert.True(outerScope.AddScannedType(temporaryType));
                Assert.True(outerScope.AddScannedMethod(temporaryMethod));
            }

            Assert.True(outerScope.ContainsScannedType(originalType));
            Assert.True(outerScope.ContainsScannedMethod(originalMethod));
            Assert.False(outerScope.ContainsScannedType(temporaryType));
            Assert.False(outerScope.ContainsScannedMethod(temporaryMethod));
        }

        private abstract class AbstractTestComponent : MonoBehaviour
        {
        }

        private static Action _validGameBootMethodInvoked;
        private static Action _invalidGameBootMethodInvoked;
        private static Action _throwingGameBootMethodInvoked;

        private static void ValidGameBootMethod()
        {
            _validGameBootMethodInvoked?.Invoke();
        }

        private void InstanceGameBootMethod()
        {
            _invalidGameBootMethodInvoked?.Invoke();
        }

        private static void ParameterizedGameBootMethod(int value)
        {
            _invalidGameBootMethodInvoked?.Invoke();
        }

        private static int NonVoidGameBootMethod()
        {
            _invalidGameBootMethodInvoked?.Invoke();
            return 1;
        }

        private static void ThrowingGameBootMethod()
        {
            _throwingGameBootMethodInvoked?.Invoke();
            throw new InvalidOperationException("game boot method failure");
        }

        private static MethodInfo GetRequiredTestMethod(string methodName)
        {
            var method = typeof(GameBootRegisteryTests).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' was not found on {typeof(GameBootRegisteryTests).FullName}.");
            }

            return method;
        }

        private sealed class GameBootMethodTargetScope : IDisposable
        {
            private readonly Action _originalValidGameBootMethodInvoked;
            private readonly Action _originalInvalidGameBootMethodInvoked;
            private readonly Action _originalThrowingGameBootMethodInvoked;

            private GameBootMethodTargetScope(
                Action originalValidGameBootMethodInvoked,
                Action originalInvalidGameBootMethodInvoked,
                Action originalThrowingGameBootMethodInvoked)
            {
                _originalValidGameBootMethodInvoked = originalValidGameBootMethodInvoked;
                _originalInvalidGameBootMethodInvoked = originalInvalidGameBootMethodInvoked;
                _originalThrowingGameBootMethodInvoked = originalThrowingGameBootMethodInvoked;
            }

            public static GameBootMethodTargetScope Create()
            {
                var scope = new GameBootMethodTargetScope(
                    _validGameBootMethodInvoked,
                    _invalidGameBootMethodInvoked,
                    _throwingGameBootMethodInvoked);

                _validGameBootMethodInvoked = null;
                _invalidGameBootMethodInvoked = null;
                _throwingGameBootMethodInvoked = null;

                return scope;
            }

            public void Dispose()
            {
                _throwingGameBootMethodInvoked = _originalThrowingGameBootMethodInvoked;
                _invalidGameBootMethodInvoked = _originalInvalidGameBootMethodInvoked;
                _validGameBootMethodInvoked = _originalValidGameBootMethodInvoked;
            }
        }

        private sealed class GameBootRegisteryStateScope : IDisposable
        {
            private static readonly FieldInfo InitializedField = GetRequiredField("_initialized");
            private static readonly FieldInfo GameBootInvokedField = GetRequiredField("_gameBootInvoked");
            private static readonly FieldInfo OnGameBootField = GetRequiredField("OnGameBoot");
            private static readonly FieldInfo CreatedGameBootObjectsField = GetRequiredField("_createdGameBootObjects");
            private static readonly FieldInfo ScannedTypeField = GetRequiredField("_scannedType");
            private static readonly FieldInfo ScannedMethodField = GetRequiredField("_scannedMethod");

            private readonly bool _originalInitialized;
            private readonly bool _originalGameBootInvoked;
            private readonly Action _originalOnGameBoot;
            private readonly GameObject[] _originalCreatedGameBootObjects;
            private readonly Type[] _originalScannedTypes;
            private readonly MethodInfo[] _originalScannedMethods;

            private GameBootRegisteryStateScope(
                bool originalInitialized,
                bool originalGameBootInvoked,
                Action originalOnGameBoot,
                GameObject[] originalCreatedGameBootObjects,
                Type[] originalScannedTypes,
                MethodInfo[] originalScannedMethods)
            {
                _originalInitialized = originalInitialized;
                _originalGameBootInvoked = originalGameBootInvoked;
                _originalOnGameBoot = originalOnGameBoot;
                _originalCreatedGameBootObjects = originalCreatedGameBootObjects;
                _originalScannedTypes = originalScannedTypes;
                _originalScannedMethods = originalScannedMethods;
            }

            public static GameBootRegisteryStateScope Create()
            {
                var createdGameBootObjects = GetCreatedGameBootObjectList();
                var scannedTypes = GetScannedTypeSet();
                var scannedMethods = GetScannedMethodSet();
                var scope = new GameBootRegisteryStateScope(
                    (bool)InitializedField.GetValue(null),
                    (bool)GameBootInvokedField.GetValue(null),
                    (Action)OnGameBootField.GetValue(null),
                    createdGameBootObjects.ToArray(),
                    scannedTypes.ToArray(),
                    scannedMethods.ToArray());

                InitializedField.SetValue(null, false);
                GameBootInvokedField.SetValue(null, false);
                OnGameBootField.SetValue(null, null);
                createdGameBootObjects.Clear();
                scannedTypes.Clear();
                scannedMethods.Clear();

                return scope;
            }

            public void Dispose()
            {
                var createdGameBootObjects = GetCreatedGameBootObjectList();
                var scannedTypes = GetScannedTypeSet();
                var scannedMethods = GetScannedMethodSet();
                createdGameBootObjects.Clear();
                createdGameBootObjects.AddRange(_originalCreatedGameBootObjects);
                scannedTypes.Clear();
                scannedTypes.UnionWith(_originalScannedTypes);
                scannedMethods.Clear();
                scannedMethods.UnionWith(_originalScannedMethods);
                OnGameBootField.SetValue(null, _originalOnGameBoot);
                GameBootInvokedField.SetValue(null, _originalGameBootInvoked);
                InitializedField.SetValue(null, _originalInitialized);
            }

            public Action GetOnGameBoot()
            {
                return (Action)OnGameBootField.GetValue(null);
            }

            public bool IsInitialized()
            {
                return (bool)InitializedField.GetValue(null);
            }

            public void SetInitialized(bool initialized)
            {
                InitializedField.SetValue(null, initialized);
            }

            public bool AddScannedType(Type type)
            {
                return GetScannedTypeSet().Add(type);
            }

            public bool AddScannedMethod(MethodInfo method)
            {
                return GetScannedMethodSet().Add(method);
            }

            public bool ContainsScannedType(Type type)
            {
                return GetScannedTypeSet().Contains(type);
            }

            public bool ContainsScannedMethod(MethodInfo method)
            {
                return GetScannedMethodSet().Contains(method);
            }

            private static FieldInfo GetRequiredField(string fieldName)
            {
                var field = typeof(GameBootRegistry).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    throw new InvalidOperationException($"Field '{fieldName}' was not found on {typeof(GameBootRegistry).FullName}.");
                }

                return field;
            }

            private static List<GameObject> GetCreatedGameBootObjectList()
            {
                return (List<GameObject>)CreatedGameBootObjectsField.GetValue(null);
            }

            private static HashSet<Type> GetScannedTypeSet()
            {
                return (HashSet<Type>)ScannedTypeField.GetValue(null);
            }

            private static HashSet<MethodInfo> GetScannedMethodSet()
            {
                return (HashSet<MethodInfo>)ScannedMethodField.GetValue(null);
            }
        }
    }
}
