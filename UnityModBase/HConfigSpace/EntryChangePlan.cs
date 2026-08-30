namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 一次配置项重绑定的事务提交计划。
    /// 仅作为数据载体，保存 <see cref="ConfigEntry{T}"/> 在准备阶段捕获的旧、新状态；
    /// 实际的内存提交、回滚和事件发布由 <see cref="IConfigEntry"/> 上的
    /// <see cref="IConfigEntry.ApplyBind"/>、<see cref="IConfigEntry.RollbackBind"/> 和
    /// <see cref="IConfigEntry.PublishBind"/> 操作这些数据完成。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 生命周期遵循三阶段事务模型：先由 <see cref="IConfigEntry.PrepareBind"/> 创建实例，
    /// 此时活动绑定与运行时值均未改变；随后由 <see cref="ConfigService.Reload"/> 依次调用
    /// <see cref="IConfigEntry.ApplyBind"/> 静默切换内存状态，任一项失败时按逆序调用
    /// <see cref="IConfigEntry.RollbackBind"/> 撤销；全部应用成功后再统一调用
    /// <see cref="IConfigEntry.PublishBind"/> 发布变化事件。
    /// </para>
    /// <para>
    /// 实例仅供一次重载流程串行使用，不提供线程安全保护；事件发布后不再允许回滚。
    /// </para>
    /// </remarks>
    public sealed class EntryChangePlan
    {
        /// <summary>
        /// 拥有本计划的配置项。配置服务持有非泛型计划列表，需通过该引用把提交、回滚和发布
        /// 委托回类型正确的 <see cref="ConfigEntry{T}"/> 方法。
        /// </summary>
        public IConfigEntry ConfigEntry { get; }

        /// <summary>
        /// 提交前活动绑定的文件项，回滚时用于恢复 <see cref="ConfigEntry{T}.Entry"/>。
        /// </summary>
        public ConfigFileEntry OldEntry { get; }

        /// <summary>
        /// 提交前的运行时值（装箱形式），回滚时用于恢复配置项内部值。
        /// </summary>
        public object OldValue { get; }

        /// <summary>
        /// 本次重绑定的目标文件项，应用成功后成为新的活动绑定。
        /// </summary>
        public ConfigFileEntry NewEntry { get; }

        /// <summary>
        /// 从 <see cref="NewEntry"/> 解码得到的目标值（装箱形式）。
        /// </summary>
        public object NewValue { get; }

        /// <summary>
        /// <see cref="NewEntry"/> 在准备阶段读取时的原始值文本。
        /// <see cref="IConfigEntry.ApplyBind"/> 会把规范化编码后的文本写回 <see cref="NewEntry"/>，
        /// 回滚时需用本字段恢复候选项原貌，避免失败计划在即将丢弃的新文件模型上残留部分提交痕迹。
        /// </summary>
        public string OriginalCandidateValue { get; }

        /// <summary>
        /// 规范化编码后的目标值文本，由 <see cref="IConfigEntry.ApplyBind"/> 写入 <see cref="NewEntry"/>。
        /// 值未变化时沿用候选项原始文本，与 <see cref="ConfigEntry{T}.Value"/> 的等值短路规则一致。
        /// </summary>
        public string EncodedValue { get; }

        /// <summary>
        /// 目标值是否与提交前值不等价。<c>false</c> 时应用只替换绑定引用，不写值、不发布事件。
        /// </summary>
        public bool Changed { get; }


        /// <summary>
        /// 提交前 <see cref="ConfigEntry{T}"/> 的变化版本号，回滚时用于恢复。
        /// </summary>
        public long OldChangeVersion { get; }

        /// <summary>
        /// 应用阶段记录的新版本号。发布阶段据此判断：若应用后该配置项又被事件处理器赋值，
        /// 版本号将不再相等，此时跳过本计划的事件发布，避免重复或失真通知。
        /// </summary>
        public long AppliedChangeVersion { get; set; }

        /// <summary>
        /// 是否已执行 <see cref="IConfigEntry.ApplyBind"/>。该方法在第一步即置位，
        /// 保证后续任一赋值意外抛出异常时本计划仍能进入回滚路径；回滚完成后复位。
        /// </summary>
        public bool Applied { get; set; } = false;

        /// <summary>
        /// 捕获一次重绑定的旧、新状态，供事务三阶段使用。仅供 <see cref="IConfigEntry.PrepareBind"/> 内部构造。
        /// </summary>
        internal EntryChangePlan(
            IConfigEntry configEntry,
            ConfigFileEntry oldEntry,
            object oldValue,
            ConfigFileEntry newEntry,
            object newValue,
            string encodedValue,
            long oldChangeVersion,
            bool changed)
        {
            ConfigEntry = configEntry;
            OldEntry = oldEntry;
            OldValue = oldValue;
            NewEntry = newEntry;
            NewValue = newValue;
            EncodedValue = encodedValue;
            OriginalCandidateValue = newEntry.Value;
            Changed = changed;
            OldChangeVersion = oldChangeVersion;
        }
    }
}
