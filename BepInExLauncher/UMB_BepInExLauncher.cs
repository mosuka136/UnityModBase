using BepInEx;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace UMB_BepInExLauncher
{
    public static class UMB_BepInExLauncherInfo
    {
        public const string GUID = "com.buele.bepinexlauncher";
        public const string Version = "1.0.0";
    }

    [BepInPlugin(UMB_BepInExLauncherInfo.GUID, nameof(UMB_BepInExLauncher), UMB_BepInExLauncherInfo.Version)]
    public class UMB_BepInExLauncher : BaseUnityPlugin
    {
        public void Awake()
        {
            var harmony = new Harmony(UMB_BepInExLauncherInfo.GUID);
            harmony.PatchAll();

            UnityModBase.UnityModBase.Initialize(Paths.PluginPath);

            Logger.LogInfo($"{nameof(UnityModBase)} has been loaded.");
        }

        public void Start()
        {
        }

        public void Update()
        {
        }

        public void OnDestroy()
        {
            UnityModBase.UnityModBase.Dispose();
        }

        [HarmonyPatch]
        public class HarmonyPatches
        {
            [HarmonyPatch(typeof(SceneManager), "Internal_SceneLoaded")]
            [HarmonyPostfix]
            public static void PostfixSceneLoaded()
            {
                UnityModBase.GameBootRegistery.Boot();
            }
        }
    }
}
