using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnityModBase.HProvider
{
    /// <summary>
    /// 抽象业务与 GUI 代码所需的 Unity 时间、输入、场景、数学和系统剪贴板 API，便于替换或模拟 Unity 静态状态。
    /// 实现不负责驱动 Unity 生命周期，调用方仍需遵守主线程及具体 API 的上下文约束。
    /// </summary>
    public interface IUnityProvider
    {
        /// <summary>
        /// 上一帧到当前帧的受时间缩放影响时长，单位为秒。
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// 上一帧到当前帧的不受时间缩放影响时长，单位为秒。
        /// </summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// 应用启动后的不受时间缩放影响时长，单位为秒。
        /// </summary>
        float RealtimeSinceStartup { get; }

        /// <summary>
        /// Unity 当前帧序号。
        /// </summary>
        int FrameCount { get; }

        /// <summary>
        /// 当前正在处理的 IMGUI 事件；不在 <c>OnGUI</c> 调用链内时为 <c>null</c>。
        /// </summary>
        /// <remarks>
        /// 依赖事件的窗口逻辑应经本属性读取而不是直接访问 <see cref="Event.current"/>，
        /// 以便在无 Unity 运行时的单元测试中注入替身事件。
        /// </remarks>
        Event EventCurrent { get; }

        /// <summary>
        /// 当前键盘设备；没有可用键盘时为 <c>null</c>。
        /// </summary>
        Keyboard KeyboardCurrent { get; }

        /// <summary>
        /// 当前手柄设备；没有可用手柄时为 <c>null</c>。
        /// </summary>
        Gamepad GamepadCurrent { get; }

        /// <summary>
        /// 当前 IMGUI 事件中的鼠标位置，以 GUI 坐标表示；只能在有效 IMGUI 事件上下文中读取。
        /// </summary>
        Vector2 CurrentMousePosition { get; }

        /// <summary>
        /// Unity 应用开始退出时触发。
        /// </summary>
        event Action UnityQuitting;

        /// <summary>
        /// 当前活动场景的值快照。
        /// </summary>
        Scene ActiveScene { get; }

        /// <summary>
        /// 把对象写入 Unity 调试日志。
        /// </summary>
        /// <param name="message">要记录的对象。</param>
        void DebugLog(object message);

        /// <summary>
        /// 将浮点值限制在指定闭区间内。
        /// </summary>
        /// <param name="value">待限制值。</param>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        /// <returns>限制后的值。</returns>
        float Clamp(float value, float min, float max);

        /// <summary>
        /// 使用 Unity 的浮点容差规则判断两值是否近似相等。
        /// </summary>
        /// <param name="a">第一个值。</param>
        /// <param name="b">第二个值。</param>
        /// <returns>两值在 Unity 容差内相等时为 <c>true</c>。</returns>
        bool Approximately(float a, float b);

        /// <summary>
        /// 使用 Unity 舍入规则返回最接近的整数值，结果仍以浮点数表示。
        /// </summary>
        /// <param name="value">待舍入值。</param>
        /// <returns>舍入后的浮点值。</returns>
        float Round(float value);

        /// <summary>
        /// 返回两个浮点值中的较小值。
        /// </summary>
        /// <param name="a">第一个值。</param>
        /// <param name="b">第二个值。</param>
        /// <returns>较小值。</returns>
        float Min(float a, float b);

        /// <summary>
        /// 返回两个浮点值中的较大值。
        /// </summary>
        /// <param name="a">第一个值。</param>
        /// <param name="b">第二个值。</param>
        /// <returns>较大值。</returns>
        float Max(float a, float b);

        /// <summary>
        /// 用指定文本覆盖系统剪贴板内容。
        /// </summary>
        /// <param name="text">要复制的文本。</param>
        void ClipboardCopy(string text);

        /// <summary>
        /// 读取当前系统剪贴板文本。
        /// </summary>
        /// <returns>剪贴板文本。</returns>
        string ClipboardPaste();
    }
}
