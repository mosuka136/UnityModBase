using System;
using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 运行时配置项的非泛型视图。
    /// UI 层通过该接口读取元数据并写入装箱值，而不需要在绑定阶段知道具体泛型类型。
    /// 除了展示和读写能力，本接口还定义了批量重载使用的事务三阶段协议
    /// （<see cref="PrepareBind"/> / <see cref="ApplyBind"/> / <see cref="PublishBind"/> / <see cref="RollbackBind"/>），
    /// <see cref="ConfigService.Reload"/> 依赖该协议实现可回滚的统一提交；自定义实现必须正确实现这组方法，
    /// 否则加入配置表后会导致重载预检或提交失败。
    /// </summary>
    public interface IConfigEntry : IWritableEntry
    {
        /// <summary>
        /// 当前绑定的文件层模型；重载成功后可能替换为新实例。
        /// </summary>
        ConfigFileEntry Entry { get; }

        /// <summary>
        /// 装箱后的声明默认值；该值用于元数据，不表示读取失败时会自动回退。
        /// </summary>
        object BoxedDefaultValue { get; }

        /// <summary>
        /// 面向非泛型调用方的同步值变化事件。
        /// </summary>
        event EventHandler OnValueChangedBase;

        /// <summary>
        /// 事务协议第一阶段：在不切换当前绑定的前提下验证候选项，并构造可提交、可回滚的计划。
        /// 实现需在此阶段完成全部可能失败的解码与规范化编码，并向候选项补充运行时元数据；
        /// 但不得修改当前活动绑定、运行时值，也不得发布变化事件。
        /// </summary>
        /// <param name="candidate">来自本次新文件模型的候选项；成功提交前不属于活动运行时绑定。</param>
        /// <param name="plan">成功时返回尚未应用的单项提交计划；失败时为 <c>null</c>。</param>
        /// <param name="errorMessage">失败诊断；成功时为空字符串。</param>
        /// <returns>候选项是否已完成预检且可以参加统一提交。</returns>
        bool PrepareBind(ConfigFileEntry candidate, out EntryChangePlan plan, out string errorMessage);

        /// <summary>
        /// 事务协议第二阶段：静默切换内存状态，不得发布变化事件。
        /// 实现需把计划的第一步即标记为已应用，保证后续任一赋值意外失败时仍能被回滚；
        /// 调用方应将计划按顺序入栈，发生异常时按逆序对已应用计划调用 <see cref="RollbackBind"/>。
        /// </summary>
        /// <param name="plan">由 <see cref="PrepareBind"/> 创建的提交计划。</param>
        void ApplyBind(EntryChangePlan plan);

        /// <summary>
        /// 事务协议第三阶段：在全部计划应用成功后发布本项对应的变化事件。
        /// 若应用后该配置项已被事件处理器再次赋值，应跳过本次发布，避免重复或失真通知。
        /// 此阶段抛出的异常只能记录并继续，不能撤销其他计划已对订阅者产生的副作用。
        /// </summary>
        /// <param name="plan">已成功应用的提交计划。</param>
        void PublishBind(EntryChangePlan plan);

        /// <summary>
        /// 事务协议回滚阶段：撤销一次已开始的 <see cref="ApplyBind"/>，未应用的计划必须安全返回。
        /// 调用方只会在事件发布前调用该方法，并按应用顺序的逆序执行。
        /// </summary>
        /// <param name="plan">需要撤销的提交计划。</param>
        void RollbackBind(EntryChangePlan plan);
    }
}
