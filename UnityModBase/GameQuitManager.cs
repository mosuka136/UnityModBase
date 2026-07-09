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
        private static readonly object _lock = new object();

        public static event Action OnGameQuit;

        [InitializeOnGameBoot]
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                UnityProvider.Instance.UnityQuitting += Dispose;
                BLog.Info("GameQuitManager initialized.");

                _initialized = true;
            }
        }

        public static void Dispose()
        {
            Action[] handlers;

            lock (_lock)
            {
                if (_initialized)
                    UnityProvider.Instance.UnityQuitting -= Dispose;

                handlers = (OnGameQuit?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action>().ToArray();
                OnGameQuit = null;

                _initialized = false;
            }

            foreach (var handler in handlers)
            {
                try { handler?.Invoke(); }
                catch { }
            }
        }
    }
}
