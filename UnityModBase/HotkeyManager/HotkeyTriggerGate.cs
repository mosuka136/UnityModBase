using System;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 热键触发的使用侧去抖门：同一热键被接受后，短时间内不再接受后续边沿。
    /// 部分游戏的输入配置会让 Input System 把一次物理按下在连续多帧重复报告为“本帧按下”，
    /// 调用方若直接按边沿执行动作，一次按键会被执行两次（例如窗口刚被热键打开就在下一帧被关闭，
    /// 表现为唤出界面闪现即灭）。本门以接受时刻起的短暂静默期吸收此类重复边沿；
    /// 真实快速连按的间隔远大于静默期，不受影响。
    /// </summary>
    /// <remarks>
    /// 时间由调用方注入（通常为 <c>Time.realtimeSinceStartup</c>），便于单元测试；
    /// 同一热键应固定使用同一个门实例，多个热键不应共用。
    /// </remarks>
    public sealed class HotkeyTriggerGate
    {
        /// <summary>同一热键两次触发之间的最小接受间隔，单位为秒。</summary>
        private const float SilenceSeconds = 0.07f;

        private float _lastAcceptedTime = float.NegativeInfinity;

        /// <summary>
        /// 判断热键本帧的按下边沿是否应执行动作。
        /// 热键为 <c>null</c> 或本帧没有边沿时直接返回 <c>false</c>，不调用时钟，也不会消耗静默期。
        /// </summary>
        /// <param name="hotkey">要查询的热键；为 <c>null</c> 时不触发。</param>
        /// <param name="clock">实时时钟读取委托，单位为秒；仅在本帧存在边沿时被调用。</param>
        /// <returns>本帧边沿应当执行动作时为 <c>true</c>。</returns>
        public bool ShouldTrigger(Hotkey hotkey, Func<float> clock)
        {
            if (hotkey?.WasPressedThisFrame() != true)
                return false;

            var now = clock();
            if (now - _lastAcceptedTime < SilenceSeconds)
                return false;

            _lastAcceptedTime = now;
            return true;
        }
    }
}
