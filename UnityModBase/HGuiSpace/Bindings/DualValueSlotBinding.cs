using System;
using System.Reflection;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 把 <see cref="EntryValue{T1,T2}"/> 双元素条目中的单个元素投影为独立条目绑定，
    /// 供 <see cref="Editor.ValueEditorRegistry"/> 按元素类型选择子编辑器。
    /// 元素写入会与另一元素的当前已提交值合并为新的整体值写回父条目；暂存输入保存在自身独立的编辑缓冲区中，
    /// 与另一元素的编辑互不影响。本类不负责延迟提交或变更通知，这些职责仍由 GUI 上下文的变更提交器承担。
    /// </summary>
    /// <remarks>
    /// 槽位键形如 <c>'\0'Dual_{父键}_{下标}</c>：合法配置键仅允许字母、数字和下划线，而 <c>'\0'</c> 在复合值拆分器中
    /// 保留为“无外层定界符”哨兵，不会出现在任何真实配置键中；因此槽位键不会与真实条目键冲突，两槽位之间也互不相同。
    /// 名称与说明透传父条目，使变更提示仍显示真实条目；槽位子元数据从父条目的
    /// <see cref="UiCompositeMetadata"/> 按下标解析，父元数据缺失或槽位未声明时为 null，子编辑器走通用分支。
    /// </remarks>
    internal sealed class DualValueSlotBinding : IEntryBinding
    {
        /// <summary>
        /// 获取被投影的双元素父条目绑定。
        /// </summary>
        public IEntryBinding Parent { get; }

        /// <summary>
        /// 获取本绑定对应的元素下标；0 表示第一个元素，1 表示第二个元素。
        /// </summary>
        public int SlotIndex { get; }

        /// <inheritdoc/>
        public string Key => $"\0Dual_{Parent.Key}_{SlotIndex}";

        /// <inheritdoc/>
        public Translator Name => Parent.Name;

        /// <inheritdoc/>
        public Translator Description => Parent.Description;

        /// <inheritdoc/>
        public Type ValueType { get; }

        /// <inheritdoc/>
        public IUiMetadata Metadata { get; }

        /// <inheritdoc/>
        public EntryEditBuffer EditBuffer { get; } = new EntryEditBuffer();

        private readonly PropertyInfo _ownValueProperty;
        private readonly PropertyInfo _otherValueProperty;
        private readonly Action _onParentValueWritten;

        /// <summary>
        /// 获取或设置父条目双元素值中本槽位对应的元素。
        /// 父条目整体值缺失属于异常状态，此时读取降级返回 null 而不抛出。
        /// 赋值必须为槽位声明类型可赋值的非 null 实例；写入时与另一元素的当前已提交值合并为新的整体值，
        /// 经父条目整体替换，复用其编码、文件同步和变更事件流程，等值写入由父条目忽略。
        /// </summary>
        /// <exception cref="ArgumentException">值为 null 或其运行时类型不能赋给 <see cref="ValueType"/>。</exception>
        public object Value
        {
            get
            {
                var tuple = Parent.Value;
                return tuple == null ? null : _ownValueProperty.GetValue(tuple, null);
            }
            set
            {
                if (!ValueType.IsAssignableFrom(value?.GetType()))
                    throw new ArgumentException($"Invalid value type. Expected {ValueType}, got {value?.GetType()}.");

                WriteMergedValue(value);
            }
        }

        /// <summary>
        /// 创建 <see cref="EntryValue{T1,T2}"/> 双元素条目的槽位绑定。
        /// </summary>
        /// <param name="parent">值类型为封闭 <see cref="EntryValue{T1,T2}"/> 泛型的父条目绑定。</param>
        /// <param name="slotIndex">元素下标，必须为 0 或 1。</param>
        /// <param name="onParentValueWritten">本槽位向父条目写入整体值后的可选通知回调。</param>
        /// <exception cref="ArgumentNullException"><paramref name="parent"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotIndex"/> 不为 0 或 1。</exception>
        /// <exception cref="ArgumentException"><paramref name="parent"/> 的值类型不是封闭 <see cref="EntryValue{T1,T2}"/>。</exception>
        public DualValueSlotBinding(IEntryBinding parent, int slotIndex, Action onParentValueWritten = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (slotIndex != 0 && slotIndex != 1)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Slot index must be 0 or 1.");

            var parentValueType = parent.ValueType;
            if (!IsSupportedValueType(parentValueType))
                throw new ArgumentException($"Parent entry value type is not a closed {typeof(EntryValue<,>).FullName}: {parentValueType?.FullName}.", nameof(parent));

            Parent = parent;
            SlotIndex = slotIndex;
            ValueType = parentValueType.GetGenericArguments()[slotIndex];
            _ownValueProperty = GetSlotProperty(parentValueType, slotIndex);
            _otherValueProperty = GetSlotProperty(parentValueType, slotIndex == 0 ? 1 : 0);
            Metadata = ResolveSlotMetadata(parent.Metadata, slotIndex);
            _onParentValueWritten = onParentValueWritten;
        }

        /// <summary>判断指定类型是否为可投影的封闭双元素条目值类型。</summary>
        internal static bool IsSupportedValueType(Type valueType)
        {
            return valueType != null &&
                   valueType.IsGenericType &&
                   !valueType.ContainsGenericParameters &&
                   valueType.GetGenericTypeDefinition() == typeof(EntryValue<,>);
        }

        private static PropertyInfo GetSlotProperty(Type valueType, int slotIndex)
        {
            var propertyName = slotIndex == 0
                ? nameof(EntryValue<int, int>.Value1)
                : nameof(EntryValue<int, int>.Value2);
            return valueType.GetProperty(propertyName);
        }

        // 与父条目另一元素的当前已提交值合并为新的整体值写入。
        private void WriteMergedValue(object value)
        {
            var tuple = Parent.Value;
            var otherValue = tuple == null ? null : _otherValueProperty.GetValue(tuple, null);
            var newTuple = SlotIndex == 0
                ? Activator.CreateInstance(Parent.ValueType, value, otherValue)
                : Activator.CreateInstance(Parent.ValueType, otherValue, value);
            Parent.Value = newTuple;
            _onParentValueWritten?.Invoke();
        }

        /// <summary>
        /// 解析父条目组合元数据中本槽位的子元数据；父元数据缺失、不是组合元数据、数组过短或槽位未声明时返回 null。
        /// </summary>
        private static IUiMetadata ResolveSlotMetadata(IUiMetadata parentMetadata, int slotIndex)
        {
            var metadatas = (parentMetadata as UiCompositeMetadata)?.Metadatas;
            if (metadatas == null || (uint)slotIndex >= (uint)metadatas.Length)
                return null;
            return metadatas[slotIndex];
        }
    }
}
