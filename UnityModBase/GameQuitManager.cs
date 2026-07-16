using System;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HProvider;

namespace UnityModBase
{
    /// <summary>
    /// 将 Unity 的进程退出通知转换为框架级一次性退出事件，并在显式卸载时复用同一清理入口。
    /// 该类型不发起游戏退出，也不决定其他服务的释放顺序。
    /// </summary>
    public static class GameQuitManager
    {
        private static bool _initialized = false;

        // 保护 Unity 事件订阅状态以及退出回调的提取和清空；用户回调始终在锁外执行。
        private static readonly object _lock = new object();

        /// <summary>
        /// 游戏退出或框架显式释放时执行的一次性回调。
        /// 释放时会在调用前清空当时的订阅；单个回调异常不会阻止其余回调。
        /// </summary>
        public static event Action OnGameQuit;

        /// <summary>
        /// 在游戏启动阶段订阅 Unity 退出事件。重复调用不会重复订阅。
        /// </summary>
        [InitializeOnGameBoot]
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                UnityProvider.Instance.UnityQuitting += Dispose;
                BLog.Debug("GameQuitManager initialized.");

                _initialized = true;
            }
        }

        /// <summary>
        /// 取消 Unity 退出事件订阅，并提取、清空后逐一执行当前退出回调。
        /// 该方法可由 Unity 退出事件或框架卸载路径调用；重复调用不会重复执行已清空的回调。
        /// </summary>
        public static void Dispose()
        {
            Action[] handlers;

            lock (_lock)
            {
                if (_initialized)
                    UnityProvider.Instance.UnityQuitting -= Dispose;

                handlers = OnGameQuit.GetInvocationListOrEmpty().ToArray();
                OnGameQuit = null;

                _initialized = false;
            }

            // 在锁外调用，允许退出处理器重入 Dispose，也避免用户代码长期占用生命周期锁。
            foreach (var handler in handlers)
            {
                try
                {
                    handler?.Invoke();
                }
                catch (Exception ex)
                {
                    BLog.Error($"Exception in OnGameQuit handler.", ex);
                }
            }
        }
    }
}
