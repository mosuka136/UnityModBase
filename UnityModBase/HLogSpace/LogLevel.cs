namespace UnityModBase.HLogSpace
{
    /// <summary>
    /// 日志等级。数值越大表示越严重。
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 面向诊断的详细调试信息。
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 常规运行信息。
        /// </summary>
        Info,

        /// <summary>
        /// 值得关注但不表示故障的运行通知。
        /// </summary>
        Notice,

        /// <summary>
        /// 可能影响行为但仍可继续运行的警告。
        /// </summary>
        Warning,

        /// <summary>
        /// 已发生失败或异常的错误信息。
        /// </summary>
        Error
    }
}
