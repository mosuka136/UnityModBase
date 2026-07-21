using System;
using UnityEngine.InputSystem;
using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 单个普通键盘按键的叶级触发器，通常作为 <see cref="KeyboardChord"/> 的主键。
    /// 修饰键的左右侧语义由 <see cref="KeyboardModifierTrigger"/> 处理，本类不负责组合其他按键。
    /// </summary>
    /// <remarks>
    /// <see cref="Key.None"/> 始终视为未配置。可写属性只接受 <see cref="Key"/> 中已定义的值；
    /// 未定义值会被静默忽略，配置解析器则返回明确的失败结果。
    /// </remarks>
    public class KeyboardTrigger : IHotkeyTrigger
    {
        private Key _key = Key.None;

        /// <inheritdoc />
        public UnityProvider UnityService { get; }

        /// <summary>
        /// 要查询的 Input System 键；默认 <see cref="Key.None"/> 表示未配置。赋入未定义枚举值时保留原值。
        /// </summary>
        public Key Key
        {
            get => _key;
            set
            {
                if (_key != value && Enum.IsDefined(typeof(Key), value))
                    _key = value;
            }
        }

        /// <summary>
        /// 创建未指定键的触发器。
        /// </summary>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardTrigger(UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
        }

        /// <summary>
        /// 创建指定键的触发器；<see cref="Key.None"/> 会保留未配置状态，未定义枚举值会被忽略并回退到该状态。
        /// </summary>
        /// <param name="key">要查询的键。</param>
        /// <param name="unityService">输入状态提供器，不可为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unityService"/> 为 <c>null</c> 时抛出。</exception>
        public KeyboardTrigger(Key key, UnityProvider unityService)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
            Key = key;
        }

        /// <inheritdoc />
        public bool IsPressed()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;
            return Key != Key.None && kb[Key].isPressed;
        }

        /// <inheritdoc />
        public bool WasPressedThisFrame()
        {
            var kb = UnityService?.KeyboardCurrent;
            if (kb == null)
                return false;
            return Key != Key.None && kb[Key].wasPressedThisFrame;
        }

        /// <summary>
        /// 不区分大小写解析已定义的 <see cref="Key"/> 名称；<see cref="Key.None"/> 和未定义数值均视为失败。
        /// </summary>
        /// <param name="token">单个键名，前后空白会被移除。</param>
        /// <param name="unityService">输入状态提供器；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>成功时包含键盘触发器，否则包含解析错误。</returns>
        public static HotkeyResult<KeyboardTrigger> TryParse(string token, UnityProvider unityService)
        {
            if (unityService == null)
                return HotkeyResult<KeyboardTrigger>.Fail("UnityService is null.");

            if (string.IsNullOrWhiteSpace(token))
                return HotkeyResult<KeyboardTrigger>.Fail("Token is null or whitespace.");

            token = token.Trim();

            if (Enum.TryParse<Key>(token, true, out var key))
            {
                if (!Enum.IsDefined(typeof(Key), key))
                    return HotkeyResult<KeyboardTrigger>.Fail($"Parsed key '{key}' is not defined in Key enum.");

                if (key == Key.None)
                    return HotkeyResult<KeyboardTrigger>.Fail("Parsed key is None.");

                return new KeyboardTrigger(key, unityService);
            }

            return HotkeyResult<KeyboardTrigger>.Fail($"Failed to parse token '{token}' as a Key.");
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Key == Key.None)
                return string.Empty;
            return Key.ToString();
        }

        /// <inheritdoc />
        public IHotkeyTrigger Clone()
        {
            return new KeyboardTrigger(Key, UnityService);
        }
    }
}
