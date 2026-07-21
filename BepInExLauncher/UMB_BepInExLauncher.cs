using BepInEx;
using System;
using UnityEngine.SceneManagement;

namespace UnityModBase.BepInExLauncher
{
    /// <summary>
    /// BepInEx 识别启动插件所需的稳定元数据。
    /// </summary>
    public static class UMB_BepInExLauncherInfo
    {
        /// <summary>
        /// BepInEx 插件唯一标识；修改后会被宿主视为另一个插件。
        /// </summary>
        public const string GUID = "com.buele.bepinexlauncher";

        /// <summary>
        /// 启动插件版本，由 <see cref="BepInPlugin"/> 元数据使用。
        /// </summary>
        public const string Version = "1.0.0";
    }

    /// <summary>
    /// UnityModBase 的 BepInEx 宿主适配层。
    /// 该插件在加载时初始化基础服务，在首次场景加载后触发游戏启动扩展点，并在销毁时统一释放框架；
    /// 具体配置、日志和扩展点实现仍由 UnityModBase 程序集负责。
    /// </summary>
    [BepInPlugin(UMB_BepInExLauncherInfo.GUID, nameof(UMB_BepInExLauncher), UMB_BepInExLauncherInfo.Version)]
    public class UMB_BepInExLauncher : BaseUnityPlugin
    {
        // Awake 可能在初始化中途失败，单独记录订阅状态以便失败路径和 OnDestroy 安全取消。
        private bool _sceneLoadedHandlerRegistered = false;

        // 启动扩展点只应在首个场景加载后触发，后续场景切换不再进入注册器。
        private bool _gameBootInvoked = false;

        /// <summary>
        /// 建立场景监听并初始化 UnityModBase；失败时撤销监听、记录错误并销毁当前插件组件。
        /// </summary>
        private void Awake()
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

        private void Start()
        {
        }

        private void Update()
        {
        }

        /// <summary>
        /// 在插件卸载或初始化失败后的组件销毁阶段撤销宿主事件，并释放所有框架级状态。
        /// </summary>
        private void OnDestroy()
        {
            UnregisterSceneLoadedHandler();
            UnityModBase.Dispose();
        }

        /// <summary>
        /// 将首次场景加载作为游戏启动边界；场景本身及加载模式不参与扩展点筛选。
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_gameBootInvoked)
                return;

            try
            {
                GameBootRegistry.Boot();
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
