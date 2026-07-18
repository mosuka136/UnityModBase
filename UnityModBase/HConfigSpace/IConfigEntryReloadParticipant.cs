namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 配置重载协调器与运行时配置项之间的内部事务协议。
    /// 实现负责在准备阶段完成全部可能失败的转换与比较，并生成可提交的重绑定计划；
    /// 允许补充尚未生效的候选文件项元数据，但不得修改活动绑定、运行时值或发布变化事件。
    /// </summary>
    internal interface IConfigEntryReloadParticipant
    {
        /// <summary>
        /// 验证候选文件项，并捕获提交和回滚所需的旧、新状态。
        /// </summary>
        /// <param name="candidate">来自本次新文件模型的候选项；成功提交前不属于活动运行时绑定。</param>
        /// <param name="plan">成功时返回尚未应用的单项重绑定计划；失败时为 <c>null</c>。</param>
        /// <param name="errorMessage">失败诊断；成功时为空字符串。</param>
        /// <returns>候选项是否已完成预检且可以参加统一提交。</returns>
        /// <remarks>
        /// 实现可以修改候选项的非值元数据，但不得修改当前活动文件项或运行时值。
        /// </remarks>
        bool TryPrepareRebind(ConfigFileEntry candidate, out ConfigReloadPlan plan, out string errorMessage);
    }
}
