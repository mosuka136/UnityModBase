namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 标识日志表格的字段列，同时作为 <see cref="GroupBinding.SortOrder"/> 的排序键。
    /// <see cref="None"/> 表示保留日志条目的原始插入顺序。
    /// </summary>
    public enum EntryContentType
    {
        /// <summary>不按字段排序，使用原始插入顺序。</summary>
        None,
        /// <summary>日志 ID。</summary>
        Id,
        /// <summary>首次记录时间。</summary>
        Timestamp,
        /// <summary>托管线程 ID。</summary>
        ThreadId,
        /// <summary>Unity 帧编号。</summary>
        Frame,
        /// <summary>场景名称。</summary>
        Scene,
        /// <summary>日志等级。</summary>
        Level,
        /// <summary>日志消息正文。</summary>
        Message,
        /// <summary>调用方源文件。</summary>
        File,
        /// <summary>调用方源文件行号。</summary>
        Line,
        /// <summary>调用方成员名称。</summary>
        Member,
        /// <summary>异常文本。</summary>
        Exception,
        /// <summary>最后重复时间。</summary>
        LastRepeatTime,
        /// <summary>累计出现次数。</summary>
        RepeatCount
    }
}
