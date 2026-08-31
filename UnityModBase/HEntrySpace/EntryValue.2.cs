using System;

namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 保存条目的两个独立值，供配置、实时控制和通用 GUI 共享使用。
    /// 实例不可变：元素只在构造时赋值，任何修改都应新建实例整体替换。
    /// <see cref="Value1"/>、<see cref="Value2"/> 属性名是配置编解码的反射契约
    /// （<see cref="HConfigSpace.ConfigFileModel"/> 按名读取元素并以元素构造函数重建实例），不可重命名。
    /// </summary>
    /// <typeparam name="T1">第一个元素的值类型。</typeparam>
    /// <typeparam name="T2">第二个元素的值类型。</typeparam>
    /// <remarks>
    /// 配置文件中编码为无外层定界符的 <c>v1,v2</c> 平铺文本；该格式无法区分内外层逗号，
    /// 因此元素类型不允许再嵌套多元素条目值，绑定阶段由
    /// <see cref="EntryModel.IsEntryMultipleValueType(Type)"/> 与 <see cref="HConfigSpace.ConfigService"/> 拒绝。
    /// 等值判断按元素委托 <see cref="EntryModel.ValueEqual(object, object)"/>，与单值条目遵循同一套规则。
    /// </remarks>
    public sealed class EntryValue<T1, T2> : IEntryMultipleValue, IEquatable<EntryValue<T1, T2>>
    {
        /// <summary>获取第一个元素。</summary>
        public T1 Value1 { get; }

        /// <summary>获取第二个元素。</summary>
        public T2 Value2 { get; }

        /// <summary>创建包含指定两个元素的条目值。</summary>
        public EntryValue(T1 value1, T2 value2)
        {
            Value1 = value1;
            Value2 = value2;
        }

        /// <inheritdoc/>
        public bool Equals(EntryValue<T1, T2> other)
        {
            return other != null &&
                   EntryModel.ValueEqual(Value1, other.Value1) &&
                   EntryModel.ValueEqual(Value2, other.Value2);
        }

        /// <inheritdoc/>
        public bool Equals(IEntryValue other)
        {
            return Equals(other as EntryValue<T1, T2>);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as EntryValue<T1, T2>);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (EntryModel.ValueHashCode(Value1) * 397) ^ EntryModel.ValueHashCode(Value2);
            }
        }
    }
}
