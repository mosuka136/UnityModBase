using System;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 实时控制项的非泛型只读契约，供表模型、更新调度器和 GUI 投影统一枚举。
    /// </summary>
    public interface IControlEntry
    {
        /// <summary>获取所属表键。</summary>
        string TableKey { get; }

        /// <summary>获取条目键。</summary>
        string Key { get; }

        /// <summary>获取显示名称。</summary>
        Translator Name { get; }

        /// <summary>获取显示说明。</summary>
        Translator Description { get; }

        /// <summary>获取声明值类型。</summary>
        Type ValueType { get; }

        /// <summary>获取当前缓存值。</summary>
        object BoxedValue { get; }

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
        void SetBoxedValueFromGui(object value);

        void Update(float unscaledDeltaTime, bool isVisible, bool becameVisible);
    }
}
