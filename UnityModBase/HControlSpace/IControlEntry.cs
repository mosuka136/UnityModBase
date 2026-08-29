using System;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 实时控制项的非泛型只读契约，供表模型、更新调度器和 GUI 投影统一枚举。
    /// </summary>
    public interface IControlEntry : IEntry
    {
        /// <summary>获取可选 GUI 元数据。</summary>
        IUiMetadata Metadata { get; }

        /// <summary>获取自动更新策略。</summary>
        ControlUpdatePolicy UpdatePolicy { get; }
    }

    /// <summary>
    /// 程序集内部的可写和可调度契约。公共调用方只能通过强类型事件接收界面修改。
    /// </summary>
    internal interface IControlEntryInternal : IControlEntry, IDisposable
    {
        /// <summary>
        /// 写入界面提交的新值。值必须可赋给条目声明类型；等值写入被忽略，
        /// 实际变化会更新界面缓存并同步触发 <c>OnValueChanged</c>。
        /// </summary>
        /// <param name="value">非 null 的装箱新值。</param>
        void SetBoxedValueFromGui(object value);

        /// <summary>
        /// 按条目更新策略推进一次刷新调度，只读取 getter，不触发界面变化事件。
        /// </summary>
        /// <param name="unscaledDeltaTime">非缩放帧间隔（秒），供按秒策略累计。</param>
        /// <param name="isVisible">实时控制界面当前是否可见。</param>
        /// <param name="becameVisible">本帧是否刚由隐藏转为可见；按秒策略借此立即刷新一次。</param>
        void Update(float unscaledDeltaTime, bool isVisible, bool becameVisible);
    }
}
