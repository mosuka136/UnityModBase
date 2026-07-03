using UnityModBase.BSpace;

namespace UnityModBase
{
    public static class UnityModBase
    {
        public static void Initialize(string baseDirectory)
        {
            BService.Initialize(baseDirectory);
            GameBootRegistery.Initialize();
        }

        public static void Dispose()
        {
            BService.Dispose();
        }
}
}
