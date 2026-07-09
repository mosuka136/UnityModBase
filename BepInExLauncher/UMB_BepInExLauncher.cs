using BepInEx;
using System;
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
        private bool sceneLoadedHandlerRegistered = false;
        private bool gameBootInvoked = false;

        public void Awake()
        {
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                sceneLoadedHandlerRegistered = true;

                UnityModBase.UnityModBase.Initialize(Paths.PluginPath);

                Logger.LogInfo($"{nameof(UnityModBase)} has been loaded.");
            }
            catch (Exception exception)
            {
                UnregisterSceneLoadedHandler();
                Logger.LogError($"Failed to load {nameof(UnityModBase)}: {exception}");
                throw;
            }
        }

        public void Start()
        {
        }

        public void Update()
        {
        }

        public void OnDestroy()
        {
            UnregisterSceneLoadedHandler();
            UnityModBase.UnityModBase.Dispose();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (gameBootInvoked)
                return;

            try
            {
                UnityModBase.GameBootRegistery.Boot();
                gameBootInvoked = true;
            }
            catch (Exception exception)
            {
                Logger.LogError($"Failed to boot game mods after scene load: {exception}");
            }
        }

        private void UnregisterSceneLoadedHandler()
        {
            if (sceneLoadedHandlerRegistered)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                sceneLoadedHandlerRegistered = false;
            }
        }
    }
}
