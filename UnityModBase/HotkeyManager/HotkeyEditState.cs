namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 热键编辑器从折叠显示到录制确认的界面状态。
    /// </summary>
    public enum HotkeyEditState
    {
        /// <summary>
        /// 编辑器折叠且未进行编辑。
        /// </summary>
        Idle,

        /// <summary>
        /// 编辑器已展开，但尚未等待输入。
        /// </summary>
        Expanded,

        /// <summary>
        /// 已请求录制，正在等待首次按键。
        /// </summary>
        WaitingPress,

        /// <summary>
        /// 正在收集当前组合键输入。
        /// </summary>
        Recording,

        /// <summary>
        /// 已形成候选组合，等待用户确认或取消。
        /// </summary>
        WaitingConfirm,
    }

    /// <summary>
    /// 保存一次热键输入采样的键盘与手柄候选组合，不负责读取设备或推进编辑状态。
    /// </summary>
    public class HotkeyInputSnapshot
    {
        /// <summary>
        /// 本次采样是否检测到任意按下输入。
        /// </summary>
        public bool HasAnyPressed { get; set; }

        /// <summary>
        /// 本次采样形成的键盘组合；没有键盘输入时可为 <c>null</c>。
        /// </summary>
        public KeyboardChord KeyboardChord { get; set; }

        /// <summary>
        /// 本次采样形成的手柄组合；没有手柄输入时可为 <c>null</c>。
        /// </summary>
        public GamepadChord GamepadChord { get; set; }
    }
}
