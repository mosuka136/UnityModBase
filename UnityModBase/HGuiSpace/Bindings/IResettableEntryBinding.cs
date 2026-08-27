namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 表示可以由界面恢复到业务层默认值的可编辑条目。
    /// </summary>
    public interface IResettableEntryBinding : IEntryBinding
    {
        /// <summary>
        /// 丢弃暂存输入并恢复业务层声明的默认值。
        /// </summary>
        void ResetValue();
    }
}
