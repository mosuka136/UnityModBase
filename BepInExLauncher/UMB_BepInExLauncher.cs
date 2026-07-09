using BepInEx;
using System;
using UnityEngine.SceneManagement;

namespace UnityModBase.BepInExLauncher
{
    public static class UMB_BepInExLauncherInfo
    {
        public const string GUID = "com.buele.bepinexlauncher";
        public const string Version = "1.0.0";
    }

    [BepInPlugin(UMB_BepInExLauncherInfo.GUID, nameof(UMB_BepInExLauncher), UMB_BepInExLauncherInfo.Version)]
    public class UMB_BepInExLauncher : BaseUnityPlugin
    {
        private bool _sceneLoadedHandlerRegistered = false;
        private bool _gameBootInvoked = false;

        public void Awake()
        {
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _sceneLoadedHandlerRegistered = true;

                UnityModBase.Initialize(Paths.PluginPath);

                Logger.LogInfo($"{nameof(UnityModBase)} has been loaded.");
            }
            catch (Exception ex)
            {
                UnregisterSceneLoadedHandler();
                Logger.LogError($"Failed to load {nameof(UnityModBase)}: {ex}");
                Destroy(this);
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
            UnityModBase.Dispose();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_gameBootInvoked)
                return;

            try
            {
                GameBootRegistery.Boot();
                _gameBootInvoked = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to boot game mods after scene load: {ex}");
            }
        }

        private void UnregisterSceneLoadedHandler()
        {
            if (_sceneLoadedHandlerRegistered)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _sceneLoadedHandlerRegistered = false;
            }
        }
    }
}
