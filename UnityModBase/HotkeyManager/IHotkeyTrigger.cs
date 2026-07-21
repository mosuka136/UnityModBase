using UnityModBase.HProvider;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 单个输入条件的热键触发契约，使组合键可以统一查询键盘键、键盘修饰条件或手柄按钮。
    /// 实现只负责读取一个输入条件和生成配置文本，不负责组合判定、输入去抖、配置持久化或设备生命周期。
    /// </summary>
    /// <remarks>
    /// 一个条件可以映射到单个物理按键，也可以像“不区分左右的 Ctrl”一样映射到多个候选按键。
    /// 实现按需读取 <see cref="UnityService"/> 暴露的当前设备，不缓存跨帧输入状态，也不拥有该服务。
    /// </remarks>
    public interface IHotkeyTrigger
    {
        /// <summary>
        /// 读取输入设备状态时使用的共享 Unity 服务；触发器不会释放该引用。
        /// </summary>
        UnityProvider UnityService { get; }

        /// <summary>
        /// 判断输入当前是否保持按下。
        /// </summary>
        /// <returns>输入处于按下状态时为 <c>true</c>；对应设备不存在时为 <c>false</c>。</returns>
        bool IsPressed();

        /// <summary>
        /// 判断输入是否在当前帧刚按下；边沿语义由 Unity Input System 提供，触发器不自行比较前后帧状态。
        /// </summary>
        /// <returns>输入在当前帧发生按下转换时为 <c>true</c>；对应设备不存在时为 <c>false</c>。</returns>
        bool WasPressedThisFrame();

        /// <summary>
        /// 返回对应解析器所使用的规范化输入名称；不可序列化状态的具体表示由实现定义。
        /// </summary>
        /// <returns>输入名称；未配置或状态不完整时通常为空字符串。</returns>
        string ToString();

        /// <summary>
        /// 复制触发器的实例状态，并共享当前 Unity 服务引用。
        /// </summary>
        /// <returns>新的触发器实例。</returns>
        IHotkeyTrigger Clone();
    }
}
