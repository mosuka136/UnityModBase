using System;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 定义运行时值与 GUI 编辑状态之间的适配契约。
    /// 实际值和暂存编辑值分离：<see cref="Value"/> 表示已提交值，<see cref="EditBuffer"/> 保存尚未提交的输入。
    /// </summary>
    public interface IEntryBinding : INodeBinding
    {
        /// <summary>
        /// 获取条目声明的运行时值类型。
        /// </summary>
        Type ValueType { get; }

        /// <summary>
        /// 获取或设置已提交的装箱值。
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// 获取影响编辑控件选择或展示方式的可选元数据。
        /// </summary>
        IUiMetadata Metadata { get; }

        /// <summary>
        /// 获取该条目独占的编辑缓冲区。
        /// </summary>
        EntryEditBuffer EditBuffer { get; }
    }
}
