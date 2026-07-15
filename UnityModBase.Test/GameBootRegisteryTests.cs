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

            GameBootRegistery.OnGameBoot += handler;
            GameBootRegistery.RegisterComponentOnGameBoot(typeof(string));

            // Act
            GameBootRegistery.Boot();

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

            GameBootRegistery.OnGameBoot += handler;
            GameBootRegistery.RegisterComponentOnGameBoot(typeof(AbstractTestComponent));

            // Act
            GameBootRegistery.Boot();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void RegisterComponentOnGameBoot_NullType_ReturnsFalseWithoutThrowing()
        {
            using var scope = GameBootRegisteryStateScope.Create();
            bool? wasRegistered = null;

            var exception = Record.Exception(() =>
                wasRegistered = GameBootRegistery.RegisterComponentOnGameBoot(null));

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

            GameBootRegistery.OnGameBoot += handler;

            // Act
            GameBootRegistery.Boot();
            GameBootRegistery.Boot();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void Boot_WhenHandlerReentersBoot_StillInvokesOneShotHandlerOnlyOnce()
        {
            var invocationCount = 0;
            using var scope = GameBootRegisteryStateScope.Create();
            GameBootRegistery.OnGameBoot += () =>
            {
                invocationCount++;
                if (invocationCount == 1)
                    GameBootRegistery.Boot();
            };

            GameBootRegistery.Boot();

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
            var wasRegistered = GameBootRegistery.RegisterMethodOnGameBoot(method);
            GameBootRegistery.Boot();

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
            var wasRegistered = GameBootRegistery.RegisterMethodOnGameBoot(null);
            GameBootRegistery.RegisterMethodOnGameBoot(validMethod);
            GameBootRegistery.Boot();

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

            GameBootRegistery.RegisterMethodOnGameBoot(invalidMethod);
            GameBootRegistery.RegisterMethodOnGameBoot(validMethod);

            // Act
            GameBootRegistery.Boot();

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

            GameBootRegistery.RegisterMethodOnGameBoot(throwingMethod);
            GameBootRegistery.RegisterMethodOnGameBoot(validMethod);

            // Act
            GameBootRegistery.Boot();

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
            GameBootRegistery.OnGameBoot += () => invocationCount++;

            // Act
            GameBootRegistery.Dispose();
            GameBootRegistery.Boot();

            // Assert
            Assert.Equal(0, invocationCount);
            Assert.False(scope.IsInitialized());
            Assert.Null(scope.GetOnGameBoot());
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

            private readonly bool _originalInitialized;
            private readonly bool _originalGameBootInvoked;
            private readonly Action _originalOnGameBoot;
            private readonly GameObject[] _originalCreatedGameBootObjects;

            private GameBootRegisteryStateScope(
                bool originalInitialized,
                bool originalGameBootInvoked,
                Action originalOnGameBoot,
                GameObject[] originalCreatedGameBootObjects)
            {
                _originalInitialized = originalInitialized;
                _originalGameBootInvoked = originalGameBootInvoked;
                _originalOnGameBoot = originalOnGameBoot;
                _originalCreatedGameBootObjects = originalCreatedGameBootObjects;
            }

            public static GameBootRegisteryStateScope Create()
            {
                var createdGameBootObjects = GetCreatedGameBootObjectList();
                var scope = new GameBootRegisteryStateScope(
                    (bool)InitializedField.GetValue(null),
                    (bool)GameBootInvokedField.GetValue(null),
                    (Action)OnGameBootField.GetValue(null),
                    createdGameBootObjects.ToArray());

                InitializedField.SetValue(null, false);
                GameBootInvokedField.SetValue(null, false);
                OnGameBootField.SetValue(null, null);
                createdGameBootObjects.Clear();

                return scope;
            }

            public void Dispose()
            {
                var createdGameBootObjects = GetCreatedGameBootObjectList();
                createdGameBootObjects.Clear();
                createdGameBootObjects.AddRange(_originalCreatedGameBootObjects);
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

            private static FieldInfo GetRequiredField(string fieldName)
            {
                var field = typeof(GameBootRegistery).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    throw new InvalidOperationException($"Field '{fieldName}' was not found on {typeof(GameBootRegistery).FullName}.");
                }

                return field;
            }

            private static List<GameObject> GetCreatedGameBootObjectList()
            {
                return (List<GameObject>)CreatedGameBootObjectsField.GetValue(null);
            }
        }
    }
}
