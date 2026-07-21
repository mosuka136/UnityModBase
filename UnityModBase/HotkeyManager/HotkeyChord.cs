using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{

    /// <summary>
    /// 键盘组合或手柄组合的统一包装器，供 <see cref="Hotkey"/> 和配置解析流程以同一类型保存不同设备的组合。
    /// 输入查询、有效性和清理由 <see cref="Chord"/> 完成，本类不校验具体组合类型或重新绑定其输入服务。
    /// </summary>
    public class HotkeyChord : IHotkeyChord
    {
        /// <summary>
        /// 被包装的具体组合；为 <c>null</c> 时包装器无效且所有输入查询返回 <c>false</c>。
        /// </summary>
        public IHotkeyChord Chord { get; set; }

        /// <summary>
        /// 创建包装器时关联的 Unity 服务。包装器本身不读取该服务，也不强制它与 <see cref="Chord"/> 内触发器的服务一致。
        /// </summary>
        public UnityProvider UnityService { get; }

        /// <inheritdoc />
        public bool IsValid => Chord?.IsValid ?? false;

        /// <summary>
        /// 创建尚未包装具体组合的实例。
        /// </summary>
        /// <param name="unityService">由上层保留的输入服务关联；当前空包装器不会直接读取该服务。</param>
        public HotkeyChord(UnityProvider unityService)
        {
            UnityService = unityService;
        }

        /// <summary>
        /// 包装已有组合，不复制组合对象。
        /// </summary>
        /// <param name="chord">具体组合，可为 <c>null</c>。</param>
        /// <param name="unityService">包装器关联的 Unity 服务。</param>
        public HotkeyChord(IHotkeyChord chord, UnityProvider unityService)
        {
            Chord = chord;
            UnityService = unityService;
        }

        /// <summary>
        /// 分别尝试按键盘组合和手柄组合解析文本；即使键盘解析成功，也会执行手柄解析。
        /// 两者都成功时优先采用键盘语义，因此手柄按钮配置应使用 <c>Gamepad</c> 前缀消除单键名称歧义。
        /// </summary>
        /// <param name="chordStr">单个组合文本。</param>
        /// <param name="unityService">触发器使用的 Unity 服务。</param>
        /// <returns>任一解析器成功时返回包装组合；均失败时合并两侧错误。</returns>
        /// <exception cref="System.NullReferenceException"><paramref name="chordStr"/> 为 <c>null</c> 时由键盘解析器抛出。</exception>
        public static HotkeyResult<HotkeyChord> TryParse(string chordStr, UnityProvider unityService)
        {
            var keyboardResult = KeyboardChord.TryParse(chordStr, unityService);
            var gamepadResult = GamepadChord.TryParse(chordStr, unityService);

            if (keyboardResult.Success)
                return new HotkeyChord(keyboardResult.Value, unityService);

            if (gamepadResult.Success)
                return new HotkeyChord(gamepadResult.Value, unityService);

            return HotkeyResult<HotkeyChord>.Fail("Failed to parse hotkey chord as either keyboard or gamepad chord.", keyboardResult.Errors, gamepadResult.Errors);
        }

        /// <inheritdoc />
        public void Clear()
        {
            Chord?.Clear();
        }

        /// <inheritdoc />
        public IHotkeyChord Clone()
        {
            return new HotkeyChord(Chord?.Clone(), UnityService);
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            return Chord?.IsPressed() ?? false;
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            return Chord?.WasPressedThisFrame() ?? false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Chord?.ToString() ?? string.Empty;
        }
    }
}
