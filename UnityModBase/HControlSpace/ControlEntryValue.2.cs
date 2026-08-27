using System;
using System.Collections.Generic;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 保存实时控制条目的两个独立值，不提供配置编码、文件持久化或默认值语义。
    /// </summary>
    /// <typeparam name="T1">第一个元素的值类型。</typeparam>
    /// <typeparam name="T2">第二个元素的值类型。</typeparam>
    public sealed class ControlEntryValue<T1, T2> : IEquatable<ControlEntryValue<T1, T2>>
    {
        /// <summary>获取第一个元素。</summary>
        public T1 Value1 { get; }

        /// <summary>获取第二个元素。</summary>
        public T2 Value2 { get; }

        /// <summary>创建包含指定两个元素的实时控制值。</summary>
        public ControlEntryValue(T1 value1, T2 value2)
        {
            Value1 = value1;
            Value2 = value2;
        }

        /// <inheritdoc/>
        public bool Equals(ControlEntryValue<T1, T2> other)
        {
            return other != null &&
                   EqualityComparer<T1>.Default.Equals(Value1, other.Value1) &&
                   EqualityComparer<T2>.Default.Equals(Value2, other.Value2);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as ControlEntryValue<T1, T2>);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var value1Hash = ReferenceEquals(Value1, null) ? 0 : EqualityComparer<T1>.Default.GetHashCode(Value1);
                var value2Hash = ReferenceEquals(Value2, null) ? 0 : EqualityComparer<T2>.Default.GetHashCode(Value2);
                return (value1Hash * 397) ^ value2Hash;
            }
        }
    }
}
