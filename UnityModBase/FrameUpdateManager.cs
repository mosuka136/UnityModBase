using System;
using System.Linq;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;

namespace UnityModBase
{
    /// <summary>
    /// 提供一个全局帧更新分发点。
    /// 该管理器在游戏启动后创建隐藏的 Unity 组件，把 MonoBehaviour.Update 转换为普通事件，供不适合作为组件的补丁模块订阅。
    /// </summary>
    public static class FrameUpdateManager
    {
        /// <summary>
        /// 每帧触发一次。订阅者应自行处理异常和开关判断，避免阻塞其他订阅者。
        /// </summary>
        public static event Action OnFrameUpdate;

        /// <summary>
        /// 由 <see cref="GameBootRegistery"/> 创建的实际 Unity 更新组件。
        /// </summary>
        [RegisterOnGameBoot]
        public class Updater : MonoBehaviour
        {
            public void Awake()
            {
                BLog.Info("Frame update dispatcher created.");
            }

            public void Update()
            {
                foreach (var handler in (OnFrameUpdate?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action>())
                {
                    try
                    {
                        handler?.Invoke();
                    }
                    catch (MissingMethodException)
                    {
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
    }
}
