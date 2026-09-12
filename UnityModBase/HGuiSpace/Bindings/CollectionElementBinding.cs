using System;
using System.Collections;
using System.Collections.Generic;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 把有序集合条目中的单个元素投影为独立条目绑定，供 <see cref="Editor.ValueEditorRegistry"/> 按元素类型选择子编辑器。
    /// 元素写入会复制父集合并替换对应下标的元素，构造新的集合实例整体写回父条目；暂存输入保存在自身独立的编辑缓冲区中，
    /// 与其他元素的编辑互不影响。本类不负责延迟提交或变更通知，这些职责仍由 GUI 上下文的变更提交器承担。
    /// </summary>
    /// <remarks>
    /// 元素键形如 <c>'\0'Coll_{父键}_{下标}</c>：合法配置键仅允许字母、数字和下划线，而 <c>'\0'</c> 不会出现在任何真实配置键中，
    /// 因此元素键不会与真实条目键冲突，元素之间也互不相同。名称透传父条目，使变更提示仍显示真实条目。
    /// 父集合的增删会使元素下标整体错位，元素投影由集合编辑器在下标布局失效时整体重建，本类不做下标迁移。
    /// 值类型元素经非泛型枚举读取会重新装箱，读取结果按父集合实例缓存，保证父集合不变时引用稳定，
    /// 供上层组合编辑器（如元组编辑器）以引用比较识别外部写入。
    /// </remarks>
    internal sealed class CollectionElementBinding : IEntryBinding
    {
        /// <summary>
        /// 获取被投影的有序集合父条目绑定。
        /// </summary>
        public IEntryBinding Parent { get; }

        /// <summary>
        /// 获取本绑定对应的元素下标。
        /// </summary>
        public int ElementIndex { get; }

        /// <inheritdoc/>
        public string Key => $"\0Coll_{Parent.Key}_{ElementIndex}";

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

        private readonly Action _onParentValueWritten;

        // 元素读取缓存：记录缓存来源的父集合实例和对应的元素装箱结果，见 Value 属性说明。
        private object _cachedParentCollection;
        private object _cachedElement;

        /// <summary>
        /// 获取或设置父集合中本绑定对应下标的元素。
        /// 父条目集合值缺失或短于记录下标属于异常状态，此时读取降级返回 null 而不抛出。
        /// 读取按父集合实例缓存结果：值类型元素经非泛型枚举每次都会重新装箱，
        /// 缓存使父集合实例不变时重复读取返回同一引用；父集合被整体替换（元素写入、增删或外部变更）后缓存失效并重新枚举。
        /// 赋值必须为元素声明类型可赋值的非 null 实例；写入时复制父集合并替换本下标元素，
        /// 构造新的集合实例经父条目整体替换，复用其编码、文件同步和变更事件流程，等值写入由父条目忽略。
        /// </summary>
        /// <exception cref="ArgumentException">值为 null 或其运行时类型不能赋给 <see cref="ValueType"/>。</exception>
        /// <exception cref="InvalidOperationException">父集合短于记录下标，元素投影已过期。</exception>
        public object Value
        {
            get
            {
                var collection = Parent.Value as IEnumerable;
                if (!ReferenceEquals(collection, _cachedParentCollection))
                {
                    _cachedParentCollection = collection;
                    _cachedElement = collection == null ? null : GetElementAt(collection, ElementIndex);
                }

                return _cachedElement;
            }
            set
            {
                if (!ValueType.IsAssignableFrom(value?.GetType()))
                    throw new ArgumentException($"Invalid value type. Expected {ValueType}, got {value?.GetType()}.");

                WriteMergedValue(value);
            }
        }

        /// <summary>
        /// 创建有序集合条目的元素投影绑定。
        /// </summary>
        /// <param name="parent">值类型为一维数组、实现 <see cref="IList{T}"/> 的类型或相应接口声明的父条目绑定。</param>
        /// <param name="elementIndex">元素下标，必须从 0 起且当前无上限校验；合法范围由集合编辑器按下标重建保证。</param>
        /// <param name="onParentValueWritten">本元素向父条目写入整体值后的可选通知回调。</param>
        /// <exception cref="ArgumentNullException"><paramref name="parent"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="elementIndex"/> 小于 0。</exception>
        /// <exception cref="ArgumentException"><paramref name="parent"/> 的值类型不是可按下标访问的有序集合。</exception>
        public CollectionElementBinding(IEntryBinding parent, int elementIndex, Action onParentValueWritten = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (elementIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), "Element index must be non-negative.");

            if (!IsOrderedCollectionType(parent.ValueType, out var elementType))
                throw new ArgumentException($"Parent entry value type is not an ordered collection: {parent.ValueType?.FullName}.", nameof(parent));

            Parent = parent;
            ElementIndex = elementIndex;
            ValueType = elementType;
            _onParentValueWritten = onParentValueWritten;
        }

        /// <summary>
        /// 判断类型是否为可按下标访问的有序集合：一维数组、<see cref="IList{T}"/> 实现或相应接口声明。
        /// 多维数组和无序集合（如 <see cref="HashSet{T}"/>）不满足按下标读写语义，返回 false。
        /// </summary>
        /// <param name="valueType">待判断的集合声明类型。</param>
        /// <param name="elementType">成功时输出推导出的元素类型。</param>
        /// <returns>可按下标投影元素时返回 true。</returns>
        internal static bool IsOrderedCollectionType(Type valueType, out Type elementType)
        {
            elementType = null;
            if (!EntryModel.IsCollectionType(valueType))
                return false;

            elementType = EntryModel.GetCollectionElementType(valueType);
            if (elementType == null)
                return false;

            if (valueType.IsArray)
                return valueType.GetArrayRank() == 1;

            var listInterface = typeof(IList<>).MakeGenericType(elementType);
            return listInterface.IsAssignableFrom(valueType);
        }

        /// <summary>
        /// 复制集合的全部元素到新的对象数组；集合为 null 时返回空数组。
        /// </summary>
        /// <param name="collection">要复制的集合。</param>
        /// <returns>元素快照数组。</returns>
        internal static object[] CopyElements(IEnumerable collection)
        {
            if (collection == null)
                return Array.Empty<object>();

            var elements = new List<object>();
            foreach (var item in collection)
                elements.Add(item);
            return elements.ToArray();
        }

        /// <summary>
        /// 按目标集合声明类型构造包含指定元素的新集合实例。
        /// 数组按元素类型重建；接口声明统一用 <see cref="List{T}"/> 具体实现（与配置解码层为接口条目生成的实例类型一致）；
        /// 其余具体类型要求公开无参构造函数，逐元素经 <see cref="IList.Add(object)"/> 填充。
        /// </summary>
        /// <param name="targetType">目标集合声明类型。</param>
        /// <param name="elementType">集合元素类型。</param>
        /// <param name="elements">要放入新集合的元素。</param>
        /// <returns>新构造的集合实例。</returns>
        /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
        /// <exception cref="NotSupportedException">目标类型没有可用的创建路径。</exception>
        internal static object CreateCollection(Type targetType, Type elementType, object[] elements)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (elementType == null)
                throw new ArgumentNullException(nameof(elementType));
            if (elements == null)
                throw new ArgumentNullException(nameof(elements));

            try
            {
                if (targetType.IsArray)
                {
                    var array = Array.CreateInstance(elementType, elements.Length);
                    for (int i = 0; i < elements.Length; i++)
                        array.SetValue(elements[i], i);
                    return array;
                }

                var instanceType = targetType.IsInterface
                    ? typeof(List<>).MakeGenericType(elementType)
                    : targetType;
                var list = (IList)Activator.CreateInstance(instanceType);
                foreach (var element in elements)
                    list.Add(element);
                return list;
            }
            catch (Exception ex) when (ex is MissingMethodException || ex is MemberAccessException ||
                                       ex is InvalidCastException || ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                throw new NotSupportedException($"Failed to create collection of type {targetType.FullName}. Error: {ex.Message}", ex);
            }
        }

        private static object GetElementAt(IEnumerable collection, int index)
        {
            var currentIndex = 0;
            foreach (var item in collection)
            {
                if (currentIndex == index)
                    return item;
                currentIndex++;
            }
            return null;
        }

        // 复制父集合并替换本下标元素，构造新集合实例整体写回父条目。
        private void WriteMergedValue(object value)
        {
            var elements = CopyElements(Parent.Value as IEnumerable);
            if (ElementIndex >= elements.Length)
                throw new InvalidOperationException($"Element index {ElementIndex} is outside the current parent collection.");

            elements[ElementIndex] = value;
            Parent.Value = CreateCollection(Parent.ValueType, ValueType, elements);
            _onParentValueWritten?.Invoke();
        }
    }
}
