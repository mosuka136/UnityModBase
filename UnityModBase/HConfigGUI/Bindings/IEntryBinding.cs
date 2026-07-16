using System;

namespace UnityModBase.HConfigGUI.Bindings
{
    /// <summary>
    /// 定义配置项与 GUI 编辑状态之间的适配契约。
    /// 实际配置值和暂存编辑值分离：<see cref="Value"/> 表示已提交值，<see cref="EditBuffer"/> 保存尚未提交的输入。
    /// </summary>
    public interface IEntryBinding : INodeBinding
    {
        /// <summary>
        /// 获取配置项声明的运行时值类型。
        /// </summary>
        Type ValueType { get; }
        /// <summary>
        /// 获取或设置已提交的装箱配置值。
        /// </summary>
        object Value { get; set; }
        /// <summary>
        /// 获取影响编辑控件选择或展示方式的可选元数据。
        /// </summary>
        IUiMetadata Metadata { get; }
        /// <summary>
        /// 获取该配置项独占的编辑缓冲区。
        /// </summary>
        EntryEditBuffer EditBuffer { get; }

        /// <summary>
        /// 丢弃暂存输入并恢复配置声明的默认值。
        /// </summary>
        void ResetValue();
    }
}
