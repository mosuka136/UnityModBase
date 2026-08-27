namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 指定实时控制项从监听对象读取最新值的调度方式。
    /// </summary>
    public enum ControlUpdateKind
    {
        /// <summary>每个 Unity 更新帧读取一次。</summary>
        EveryFrame,
        /// <summary>按非缩放时间每秒读取一次。</summary>
        EverySecond,
        /// <summary>实时控制界面可见时每帧读取一次。</summary>
        WhenVisibleEveryFrame,
        /// <summary>实时控制界面可见时每秒读取一次。</summary>
        WhenVisibleEverySecond,
        /// <summary>条件委托返回 true 的每一帧读取一次。</summary>
        When,
        /// <summary>实时控制界面可见且条件委托返回 true 的每一帧读取一次。</summary>
        WhenVisible,
        /// <summary>除绑定时的初始读取外不自动更新。</summary>
        Never
    }
}
