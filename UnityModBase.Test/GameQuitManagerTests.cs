using System.Reflection;

namespace UnityModBase.Test
{
    public class GameQuitManagerTests
    {
        [Fact]
        public void Dispose_HandlerSubscribed_InvokesHandler()
        {
            // Arrange
            var invocationCount = 0;
            using var scope = GameQuitManagerStateScope.Create();
            GameQuitManager.OnGameQuit += () => invocationCount++;

            // Act
            GameQuitManager.Dispose();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void Dispose_HandlerThrows_InvokesRemainingHandlersAndSwallowsException()
        {
            // Arrange
            var invocationCount = 0;
            using var scope = GameQuitManagerStateScope.Create();
            GameQuitManager.OnGameQuit += () => throw new InvalidOperationException("handler failure");
            GameQuitManager.OnGameQuit += () => invocationCount++;

            // Act
            GameQuitManager.Dispose();

            // Assert
            Assert.Equal(1, invocationCount);
        }

        private sealed class GameQuitManagerStateScope : IDisposable
        {
            private static readonly FieldInfo InitializedField = GetRequiredField("_initialized");
            private static readonly FieldInfo OnGameQuitField = GetRequiredField("OnGameQuit");

            private readonly bool _originalInitialized;
            private readonly Action _originalOnGameQuit;

            private GameQuitManagerStateScope(
                bool originalInitialized,
                Action originalOnGameQuit)
            {
                _originalInitialized = originalInitialized;
                _originalOnGameQuit = originalOnGameQuit;
            }

            public static GameQuitManagerStateScope Create()
            {
                var scope = new GameQuitManagerStateScope(
                    (bool)InitializedField.GetValue(null),
                    (Action)OnGameQuitField.GetValue(null));

                InitializedField.SetValue(null, false);
                OnGameQuitField.SetValue(null, null);

                return scope;
            }

            public void Dispose()
            {
                OnGameQuitField.SetValue(null, _originalOnGameQuit);
                InitializedField.SetValue(null, _originalInitialized);
            }

            private static FieldInfo GetRequiredField(string fieldName)
            {
                var field = typeof(GameQuitManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    throw new InvalidOperationException($"Field '{fieldName}' was not found on {typeof(GameQuitManager).FullName}.");
                }

                return field;
            }
        }
    }
}
