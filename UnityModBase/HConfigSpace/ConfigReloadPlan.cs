namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 表示一个已经完成验证、尚未提交的配置重载计划。
    /// 协调器依次应用全部计划，失败时按相反顺序回滚；只有全部应用成功后才能发布事件。
    /// 计划仅供一次重载流程串行使用，不负责并发保护，也不允许在事件发布后回滚外部副作用。
    /// </summary>
    internal abstract class ConfigReloadPlan
    {
        /// <summary>
        /// 静默切换活动模型，不得发布变化事件，也不应再执行编解码等可提前验证的操作。
        /// 实现即使在中途抛出异常，也必须允许协调器调用 <see cref="Rollback"/> 撤销已经发生的赋值。
        /// </summary>
        internal abstract void Apply();

        /// <summary>
        /// 撤销一次已经开始的 <see cref="Apply"/>；未应用时必须安全返回。
        /// 协调器只会在事件发布前调用该方法，并按应用顺序的逆序执行。
        /// </summary>
        internal abstract void Rollback();

        /// <summary>
        /// 在所有计划均应用成功后发布本计划对应的事件；没有事件的计划应安全返回。
        /// 此阶段的异常只能记录并继续，不能撤销其他计划已经对订阅者产生的副作用。
        /// </summary>
        internal abstract void Publish();
    }

    /// <summary>
    /// 保存运行时表重绑定前后的文件表引用，使表级元数据和配置项绑定参加同一批提交。
    /// </summary>
    internal sealed class TableReloadPlan : ConfigReloadPlan
    {
        private readonly ConfigTable _owner;
        private readonly ConfigFileTable _oldTable;
        private readonly ConfigFileTable _newTable;
        private bool _applied;

        internal TableReloadPlan(ConfigTable owner, ConfigFileTable newTable)
        {
            _owner = owner;
            _oldTable = owner.FileTable;
            _newTable = newTable;
        }

        internal override void Apply()
        {
            _applied = true;
            _owner.RebindFileTable(_newTable);
        }

        internal override void Rollback()
        {
            if (!_applied)
                return;

            _owner.RebindFileTable(_oldTable);
            _applied = false;
        }

        internal override void Publish()
        {
            // 表引用没有独立变化事件；表计划只用于让引用切换与配置项提交共享同一回滚边界。
        }
    }
}
