using System;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HProvider;

namespace UnityModBase
{
    public static class GameQuitManager
    {
        private static bool _initialized = false;

        public static event Action OnGameQuit;

        [InitializeOnGameBoot]
        public static void Initialize()
        {
            if (_initialized)
                return;

            var unityProvider = UnityProvider.Instance;
            unityProvider.UnityQuitting += Dispose;
            BLog.Info("GameQuitManager initialized.");

            _initialized = true;
        }

        public static void Dispose()
        {
            foreach (var handler in (OnGameQuit?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action>())
            {
                try
                {
                    handler?.Invoke();
                }
                catch
                {
                }
            }
        }
    }
}
