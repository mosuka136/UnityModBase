using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;

namespace UnityModBase
{
    /// <summary>
    /// 提供一个全局帧更新分发点。
    /// 该管理器在游戏启动后创建隐藏的 Unity 组件，把 <c>MonoBehaviour.Update</c> 转换为普通事件，
    /// 供无需自行创建组件的模块订阅；它不负责订阅者的更新频率控制或业务状态管理。
    /// </summary>
    public static class FrameUpdateManager
    {
        /// <summary>
        /// 由 Unity 主线程每帧同步触发一次。
        /// </summary>
        /// <remarks>
        /// 管理器会隔离单个订阅者异常：<see cref="MissingMethodException"/> 会使对应订阅失效并被移除，
        /// 其他异常只记录日志且保留订阅。订阅者仍应避免耗时操作，以免拉长整帧更新时间。
        /// </remarks>
        public static event Action OnFrameUpdate;

        /// <summary>
        /// 由 <see cref="GameBootRegistery"/> 创建并跨场景保留的帧更新桥接组件。
        /// 该组件只负责事件分发，其对象生命周期由启动注册器管理。
        /// </summary>
        [RegisterOnGameBoot]
        public class Updater : MonoBehaviour
        {
            /// <summary>
            /// 记录桥接组件已由启动注册器成功创建，不建立额外状态或订阅。
            /// </summary>
            private void Awake()
            {
                BLog.Debug("Frame update dispatcher created.");
            }

            /// <summary>
            /// 对当前订阅列表的快照逐一派发帧更新，使派发期间的增删订阅不影响本轮遍历。
            /// </summary>
            private void Update()
            {
                foreach (var handler in OnFrameUpdate.GetInvocationListOrEmpty())
                {
                    try
                    {
                        handler?.Invoke();
                    }
                    catch (MissingMethodException)
                    {
                        // 该订阅已无法调用，立即移除可避免后续每帧重复失败和刷日志。
                        OnFrameUpdate -= handler;
                        BLog.Warn($"Removed invalid OnFrameUpdate handler: {handler.Method.DeclaringType?.FullName}.{handler.Method.Name}");
                    }
                    catch (Exception ex)
                    {
                        BLog.Error("An error occurred while invoking OnFrameUpdate event.", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 清空所有帧更新订阅。
        /// 桥接组件及其 GameObject 由 <see cref="GameBootRegistery.Dispose"/> 负责销毁。
        /// </summary>
        public static void Dispose()
        {
            OnFrameUpdate = null;
        }
    }
}
