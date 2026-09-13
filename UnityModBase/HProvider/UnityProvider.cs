using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnityModBase.HProvider
{
    /// <summary>
    /// Unity API 访问适配器。
    /// 该类型集中封装时间、输入、场景和少量数学函数，便于非 MonoBehaviour 代码访问 Unity 状态，也让 GUI/热键逻辑更容易隔离。
    /// </summary>
    public sealed class UnityProvider : IUnityProvider
    {
        /// <inheritdoc />
        public float DeltaTime => Time.deltaTime;

        /// <inheritdoc />
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        /// <inheritdoc />
        public float RealtimeSinceStartup => Time.realtimeSinceStartup;

        /// <inheritdoc />
        public int FrameCount => Time.frameCount;

        /// <inheritdoc />
        public Event EventCurrent => Event.current;

        /// <inheritdoc />
        public Keyboard KeyboardCurrent => Keyboard.current;

        /// <inheritdoc />
        public Gamepad GamepadCurrent => Gamepad.current;

        /// <summary>
        /// 当前 IMGUI 事件中的鼠标位置。必须在有效的 <c>OnGUI</c> 调用链内读取；没有当前事件时访问会失败。
        /// </summary>
        public Vector2 CurrentMousePosition => Event.current.mousePosition;

        /// <inheritdoc />
        public event Action UnityQuitting
        {
            add => Application.quitting += value;
            remove => Application.quitting -= value;
        }

        /// <inheritdoc />
        public Scene ActiveScene => SceneManager.GetActiveScene();

        /// <summary>
        /// 共享的无状态 Unity API 适配器实例。
        /// </summary>
        public static UnityProvider Instance { get; } = new UnityProvider();
        private UnityProvider() { }

        /// <inheritdoc />
        public void DebugLog(object message)
        {
            Debug.Log(message);
        }

        /// <inheritdoc />
        public float Clamp(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <inheritdoc />
        public bool Approximately(float a, float b)
        {
            return Mathf.Approximately(a, b);
        }

        /// <inheritdoc />
        public float Round(float value)
        {
            return Mathf.Round(value);
        }

        /// <inheritdoc />
        public float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        /// <inheritdoc />
        public float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        /// <inheritdoc />
        public void ClipboardCopy(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }

        /// <inheritdoc />
        public string ClipboardPaste()
        {
            return GUIUtility.systemCopyBuffer;
        }
    }
}
