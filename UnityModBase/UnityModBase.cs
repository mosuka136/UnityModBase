using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase
{
    public static class UnityModBase
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        public static void Initialize(string baseDirectory)
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                try
                {
                    BService.Initialize(baseDirectory);
                    GameBootRegistery.Initialize();

                    _initialized = true;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                GameQuitManager.Dispose();
                GameBootRegistery.Dispose();
                BService.Dispose();
                FrameUpdateManager.Dispose();
                Translator.Dispose();

                _initialized = false;
            }
        }
    }
}
