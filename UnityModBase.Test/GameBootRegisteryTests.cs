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

        private sealed class GameBootRegisteryStateScope : IDisposable
        {
            private static readonly FieldInfo InitializedField = GetRequiredField("_initialized");
            private static readonly FieldInfo OnGameBootField = GetRequiredField("OnGameBoot");
            private static readonly FieldInfo CreatedGameBootObjectsField = GetRequiredField("_createdGameBootObjects");

            private readonly bool _originalInitialized;
            private readonly Action _originalOnGameBoot;
            private readonly GameObject[] _originalCreatedGameBootObjects;

            private GameBootRegisteryStateScope(
                bool originalInitialized,
                Action originalOnGameBoot,
                GameObject[] originalCreatedGameBootObjects)
            {
                _originalInitialized = originalInitialized;
                _originalOnGameBoot = originalOnGameBoot;
                _originalCreatedGameBootObjects = originalCreatedGameBootObjects;
            }

            public static GameBootRegisteryStateScope Create()
            {
                var createdGameBootObjects = GetCreatedGameBootObjectList();
                var scope = new GameBootRegisteryStateScope(
                    (bool)InitializedField.GetValue(null),
                    (Action)OnGameBootField.GetValue(null),
                    createdGameBootObjects.ToArray());

                InitializedField.SetValue(null, false);
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
