using System;
using System.Linq;
using System.Reflection;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 把 <see cref="ValueTuple"/> 条目中的单个元素投影为独立条目绑定，供 <see cref="Editor.ValueEditorRegistry"/> 按元素类型选择子编辑器。
    /// 元素写入会读取父元组的全部元素并替换本下标元素，构造新的元组实例整体写回父条目；暂存输入保存在自身独立的编辑缓冲区中，
    /// 与其他元素的编辑互不影响。本类不负责延迟提交或变更通知，这些职责仍由 GUI 上下文的变更提交器承担。
    /// </summary>
    /// <remarks>
    /// 元素键形如 <c>'\0'Tuple_{父键}_{下标}</c>：合法配置键仅允许字母、数字和下划线，而 <c>'\0'</c> 不会出现在任何真实配置键中，
    /// 因此元素键不会与真实条目键冲突，元素之间也互不相同。名称透传父条目，使变更提示仍显示真实条目。
    /// 支持的元组元素数与 <see cref="EntryModel.IsTupleType"/> 识别的范围一致（1 至 7 个直接元素的封闭泛型），
    /// 8 元及以上带尾元素的元组由集合编辑器的元素类型约束拒绝，不会到达本类。
    /// </remarks>
    internal sealed class TupleElementBinding : IEntryBinding
    {
        /// <summary>
        /// 获取被投影的元组父条目绑定。
        /// </summary>
        public IEntryBinding Parent { get; }

        /// <summary>
        /// 获取本绑定对应的元素下标。
        /// </summary>
        public int ElementIndex { get; }

        /// <inheritdoc/>
        public string Key => $"\0Tuple_{Parent.Key}_{ElementIndex}";

        /// <inheritdoc/>
        public Translator Name => Parent.Name;

        /// <inheritdoc/>
        public Translator Description { get; } = new Translator();

        /// <inheritdoc/>
        public Type ValueType { get; }

        /// <inheritdoc/>
        public IUiMetadata Metadata => null;

        /// <inheritdoc/>
        public EntryEditBuffer EditBuffer { get; } = new EntryEditBuffer();

        /// <summary>
        /// 获取或设置元组编辑器为本元素建议的固定控件宽度，单位为像素。
        /// 大于 0 时子编辑器应以此固定宽度代替弹性宽度绘制，使同一列表内与本元素同列的各行元素控件等宽对齐；
        /// 0 表示无约束，子编辑器按自身默认布局绘制。测量和赋值由元组编辑器在每帧绘制前完成，
        /// 不参与提交或等值判断。
        /// </summary>
        internal float SuggestedWidth { get; set; }

        private readonly FieldInfo[] _itemFields;
        private readonly Action _onParentValueWritten;

        /// <summary>
        /// 获取或设置父元组中本绑定对应下标的元素。
        /// 父条目元组值缺失属于异常状态，此时读取降级返回 null 而不抛出。
        /// 赋值必须为元素声明类型可赋值的非 null 实例；写入时读取父元组全部元素并替换本下标元素，
        /// 构造新的元组实例经父条目整体替换，复用其编码、文件同步和变更事件流程。
        /// </summary>
        /// <exception cref="ArgumentException">值为 null 或其运行时类型不能赋给 <see cref="ValueType"/>。</exception>
        public object Value
        {
            get
            {
                var tuple = Parent.Value;
                return tuple == null ? null : _itemFields[ElementIndex].GetValue(tuple);
            }
            set
            {
                if (!ValueType.IsAssignableFrom(value?.GetType()))
                    throw new ArgumentException($"Invalid value type. Expected {ValueType}, got {value?.GetType()}.");

                WriteMergedValue(value);
            }
        }

        /// <summary>
        /// 创建封闭 <see cref="ValueTuple"/> 元组条目的元素投影绑定。
        /// </summary>
        /// <param name="parent">值类型为封闭元组泛型的父条目绑定。</param>
        /// <param name="elementIndex">元素下标，从 0 起且必须小于父元组的元素数。</param>
        /// <param name="onParentValueWritten">本元素向父条目写入整体值后的可选通知回调。</param>
        /// <exception cref="ArgumentNullException"><paramref name="parent"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="elementIndex"/> 小于 0 或超出父元组元素数。</exception>
        /// <exception cref="ArgumentException"><paramref name="parent"/> 的值类型不是封闭的受支持元组泛型。</exception>
        public TupleElementBinding(IEntryBinding parent, int elementIndex, Action onParentValueWritten = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var itemFields = GetItemFields(parent.ValueType);
            if (itemFields == null)
                throw new ArgumentException($"Parent entry value type is not a closed supported tuple: {parent.ValueType?.FullName}.", nameof(parent));
            if (elementIndex < 0 || elementIndex >= itemFields.Length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {itemFields.Length - 1}.");

            Parent = parent;
            ElementIndex = elementIndex;
            ValueType = parent.ValueType.GetGenericArguments()[elementIndex];
            _itemFields = itemFields;
            _onParentValueWritten = onParentValueWritten;
        }

        /// <summary>
        /// 判断类型是否为受元素投影支持的元组：封闭的 1 至 7 元 <see cref="ValueTuple"/> 泛型。
        /// </summary>
        /// <param name="valueType">待判断的声明类型。</param>
        /// <param name="elementTypes">成功时输出全部元素类型；失败时为 null。</param>
        /// <returns>可投影为逐元素绑定时返回 true。</returns>
        internal static bool IsSupportedTupleType(Type valueType, out Type[] elementTypes)
        {
            elementTypes = null;
            if (!EntryModel.IsTupleType(valueType) || valueType.ContainsGenericParameters)
                return false;

            var genericArguments = valueType.GetGenericArguments();
            if (genericArguments.Length < 1 || genericArguments.Length > 7)
                return false;

            elementTypes = genericArguments;
            return true;
        }

        /// <summary>
        /// 判断类型是否为可整体编辑的元组：在 <see cref="IsSupportedTupleType"/> 之上要求全部元素为
        /// 基础类型或枚举——这些类型有现成子编辑器。元组编辑器和集合编辑器的元素约束共用本判定，
        /// 保证嵌套组合（如集合的元组元素）在两层给出一致的可编辑结论。
        /// </summary>
        /// <param name="valueType">待判断的声明类型。</param>
        /// <returns>可由元组编辑器逐元素编辑时返回 true。</returns>
        internal static bool IsEditableTupleType(Type valueType)
        {
            if (!IsSupportedTupleType(valueType, out var elementTypes))
                return false;

            foreach (var elementType in elementTypes)
            {
                if (!EntryModel.IsPrimitiveType(elementType) && !EntryModel.IsEnumType(elementType))
                    return false;
            }

            return true;
        }

        // 读取元组的 Item1..ItemN 公共字段；类型不受支持时返回 null。
        private static FieldInfo[] GetItemFields(Type valueType)
        {
            if (!IsSupportedTupleType(valueType, out var elementTypes))
                return null;

            return elementTypes
                .Select((_, index) => valueType.GetField($"Item{index + 1}"))
                .ToArray();
        }

        // 读取父元组全部元素并替换本下标元素，构造新元组实例整体写回父条目。
        private void WriteMergedValue(object value)
        {
            var tuple = Parent.Value;
            var items = new object[_itemFields.Length];
            for (int i = 0; i < _itemFields.Length; i++)
                items[i] = i == ElementIndex ? value : _itemFields[i].GetValue(tuple);

            Parent.Value = Activator.CreateInstance(Parent.ValueType, items);
            _onParentValueWritten?.Invoke();
        }

        /// <summary>
        /// 尝试获取元组编辑器建议的固定控件宽度；无约束时返回 false，子编辑器应按自身默认布局绘制。
        /// </summary>
        /// <param name="width">输出的建议宽度，单位为像素；无约束时为 0。</param>
        /// <returns>存在宽度约束时返回 true。</returns>
        internal bool TryGetSuggestedWidth(out float width)
        {
            width = SuggestedWidth;
            return width > 0f;
        }
    }
}
