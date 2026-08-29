namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 为共享条目值提供业务等值判断。
    /// </summary>
    /// <remarks>
    /// 实现类型若覆盖 <see cref="object.Equals(object)"/>，还应覆盖 <see cref="object.GetHashCode()"/> 并保持一致。
    /// </remarks>
    public interface IEntryValue
    {
        /// <summary>判断当前值与另一个条目值是否等价。</summary>
        bool Equals(IEntryValue other);
    }
}
