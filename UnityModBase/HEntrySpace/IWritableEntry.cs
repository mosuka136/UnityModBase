namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 提供装箱值写入能力的条目契约。
    /// 写入值必须与 <see cref="IEntry.ValueType"/> 兼容；实现应复用与强类型赋值相同的校验、编码和事件流程，
    /// 而不是绕过它们直接改写内部状态。
    /// </summary>
    public interface IWritableEntry : IEntry
    {
        /// <summary>
        /// 获取或设置当前装箱值。设置等价值通常会被实现忽略；写入不兼容类型应抛出异常而非静默转换。
        /// </summary>
        new object BoxedValue { get; set; }
    }
}
