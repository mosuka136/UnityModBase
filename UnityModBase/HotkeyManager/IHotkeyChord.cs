namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 键盘或手柄组合键的统一契约，区分“当前保持按下”和“当前帧完成触发”两种查询。
    /// </summary>
    /// <remarks>
    /// 有效性只表示实现满足最低结构要求，不保证其公开集合未被写入 <c>null</c>、错误设备类型或不可序列化状态。
    /// </remarks>
    public interface IHotkeyChord
    {
        /// <summary>
        /// 当前组合是否满足实现定义的最低结构要求。无效的组合在任何时候都不会触发。
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 判断组合要求的全部输入当前是否保持按下。
        /// </summary>
        /// <returns>组合有效且全部输入按下时为 <c>true</c>。</returns>
        bool IsPressed();

        /// <summary>
        /// 判断组合是否在当前帧达到实现定义的触发边沿；由具体组合决定哪个成员必须在本帧刚按下。
        /// </summary>
        /// <returns>组合在当前帧触发时为 <c>true</c>。</returns>
        bool WasPressedThisFrame();

        /// <summary>
        /// 移除组合内的全部输入定义，使组合失效。
        /// </summary>
        void Clear();

        /// <summary>
        /// 返回可供配置文件解析的规范化组合文本。
        /// </summary>
        /// <returns>组合文本；无有效输入时为空字符串。</returns>
        string ToString();

        /// <summary>
        /// 深复制组合结构，输入服务引用按实现约定共享。
        /// </summary>
        /// <returns>独立的组合对象。</returns>
        IHotkeyChord Clone();
    }
}
