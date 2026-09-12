using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityModBase.HEnumHelper;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 元组条目的行内组合编辑器：把封闭 <see cref="ValueTuple"/>（1 至 7 元）条目投影为逐元素的
    /// <see cref="TupleElementBinding"/> 绑定，在同一行横向排列各元素控件；
    /// 元素控件按元素类型复用所属注册表 <see cref="ValueEditorRegistry"/> 中已注册的子编辑器绘制，本类自身不直接绘制值控件。
    /// </summary>
    /// <remarks>
    /// 元素绑定按父条目弱表缓存，同一父条目跨帧复用同一实例，保证延迟提交和文本回显状态稳定。
    /// 每帧绘制前对比父条目已提交值引用：非本编辑器写入的变化（外部重置、文件重载等）会丢弃全部元素的暂存输入，
    /// 避免过期回显或未到期的延迟提交覆盖最新值；元组定长，元素投影在绑定生命周期内不重建。
    /// 元素绑定不会再匹配本编辑器，避免嵌套元组递归绘制。元组条目作为双元素槽位时整体不占用额外水平空间，避免挤压兄弟槽位。
    /// </remarks>
    public sealed class TupleEditor : IValueEditor
    {
        // 父条目到元素投影状态的弱表：元素绑定与父条目同生命周期，界面重建绑定树后旧元素随父条目一起回收。
        private readonly ConditionalWeakTable<IEntryBinding, TupleEditorState> _states = new ConditionalWeakTable<IEntryBinding, TupleEditorState>();
        private readonly Func<IEntryBinding, IValueEditor> _editorResolver;

        /// <summary>
        /// 获取元组横向区域布局使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建元组条目的行内组合编辑器。
        /// </summary>
        /// <param name="unityGui">用于布局元组横向区域的 IMGUI 提供器。</param>
        /// <param name="editorResolver">用于选择元素子编辑器的解析器。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 <c>null</c>。</exception>
        public TupleEditor(IUnityGuiProvider unityGui, Func<IEntryBinding, IValueEditor> editorResolver)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
            _editorResolver = editorResolver ?? throw new ArgumentNullException(nameof(editorResolver));
        }

        /// <summary>
        /// 判断绑定是否可投影为逐元素编辑：要求值类型为可整体编辑的元组（封闭 1 至 7 元且元素全部为
        /// 基础类型或枚举，见 <see cref="TupleElementBinding.IsEditableTupleType"/>）；
        /// 元素投影绑定自身不可再被本编辑器匹配，防止嵌套元组递归绘制。绑定为 <c>null</c> 时不可编辑。
        /// </summary>
        public bool CanEdit(IEntryBinding entry)
        {
            return IsEditable(entry);
        }

        /// <summary>
        /// 在同一行横向依次绘制全部元素子编辑器的主值控件。
        /// 绘制前同步外部值变化：非本编辑器写入的父条目新值会清空全部元素的暂存输入。
        /// </summary>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null || context == null)
                return;
            if (!IsEditable(entry))
                return;

            var state = GetState(entry);
            SyncExternalValueChange(state);
            ApplySuggestedElementWidths(entry, state);

            // 复合值区域整体占用标签和可选尾部操作之间的剩余宽度；子编辑器再在区域内部
            // 按各自策略分配空间，避免紧凑控件直接参与外层布局后破坏行对齐。
            // 作为双元素槽位时不扩展宽度，避免挤压兄弟槽位。
            UnityGui.BeginHorizontal(entry is DualValueSlotBinding ? UnityGui.ExpandWidth(false) : UnityGui.ExpandWidth(true));
            try
            {
                foreach (var element in state.Elements)
                    _editorResolver(element).DrawValue(element, context);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }
        }

        /// <summary>
        /// 依次绘制全部元素子编辑器的扩展区域（如枚举展开列表）。
        /// </summary>
        public void DrawExtra(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null || context == null)
                return;
            if (!IsEditable(entry))
                return;

            var state = GetState(entry);
            foreach (var element in state.Elements)
                _editorResolver(element).DrawExtra(element, context);
        }

        /// <summary>
        /// 释放编辑器；子编辑器归所属注册表所有，元素绑定随父条目弱引用回收，此实现不持有需释放状态。
        /// </summary>
        public void Dispose()
        {
        }

        // 与 CanEdit 同一约束；DrawValue/DrawExtra 在绑定失效或上下文缺失时静默跳过。
        private static bool IsEditable(IEntryBinding entry)
        {
            if (entry == null || entry is TupleElementBinding)
                return false;
            if (!TupleElementBinding.IsEditableTupleType(entry.ValueType))
                return false;

            return entry.Metadata == null;
        }

        // 每帧绘制前按当前皮肤样式逐列测量元素宽度并下发给对应元素绑定：
        // GUILayout 弹性控件的首选宽度随内容变化，长文本行会打破平分导致各行错落，
        // 固定宽度约束压平差异后同一列表内各行元组的同列元素等宽对齐。
        private void ApplySuggestedElementWidths(IEntryBinding entry, TupleEditorState state)
        {
            var widths = MeasureElementWidth(entry, state);
            for (int i = 0; i < widths.Length; i++)
                state.Elements[i].SuggestedWidth = widths[i];
        }

        // 逐列测量固定宽度：布尔列按开关显示词、枚举列按全部可见值的最长描述，都以对应控件样式测量且与行的当前值无关；
        // 其余列按文本框样式测量测量组内各行文本取最长。
        private float[] MeasureElementWidth(IEntryBinding entry, TupleEditorState state)
        {
            var columnCount = state.Elements.Length;
            var columnWidths = new float[columnCount];
            var columnIsControlSized = new bool[columnCount];

            for (var i = 0; i < columnCount; i++)
            {
                var elementType = state.Elements[i].ValueType;
                if (elementType == typeof(bool))
                {
                    // 开关列绘制的是显示词而非值的 ToString，取 On/Off 中较宽者；复选框宽度由样式计入。
                    columnWidths[i] = Math.Max(
                        UnityGui.CalcSizeWidth(UnityGui.ToggleStyle, TranslatorResource.On),
                        UnityGui.CalcSizeWidth(UnityGui.ToggleStyle, TranslatorResource.Off));
                    columnIsControlSized[i] = true;
                }
                else if (elementType.IsEnum)
                {
                    columnWidths[i] = MeasureEnumWidth(elementType);
                    columnIsControlSized[i] = true;
                }
            }

            // 测量组决定文本列取最长的范围：集合元素按整个父集合逐行测量（同列跨行对齐的来源），
            // 独立条目只测自身单行。
            IEnumerable groupValues;
            if (entry is CollectionElementBinding collectionElement)
                groupValues = collectionElement.Parent.Value as IEnumerable;
            else
                groupValues = new[] { entry.Value };

            if (groupValues != null)
            {
                foreach (var tuple in groupValues)
                {
                    if (tuple == null)
                        continue;

                    for (var i = 0; i < columnCount; i++)
                    {
                        if (columnIsControlSized[i])
                            continue;

                        var text = state.ItemFields[i].GetValue(tuple)?.ToString() ?? string.Empty;
                        // 文本框在样式测量的纯文本宽度外还需内边距和光标余量，追加固定宽度补足；
                        // 开关、按钮列的样式测量已含完整控件宽度，不追加。
                        var width = UnityGui.CalcSizeWidth(UnityGui.TextFieldStyle, text) + 16f;
                        if (width > columnWidths[i])
                            columnWidths[i] = width;
                    }
                }
            }

            return columnWidths;
        }

        // 枚举按钮文本可能是本地化描述：与枚举子编辑器共用 EnumHelper 的可见性和描述规则，
        // 按全部可见值的最长描述用按钮样式测量，与行的当前值无关。
        private float MeasureEnumWidth(Type enumType)
        {
            var maxWidth = 0f;
            foreach (var value in Enum.GetValues(enumType))
            {
                var enumValue = (Enum)value;
                if (!EnumHelper.IsDisplay(enumType, enumValue))
                    continue;

                var width = UnityGui.CalcSizeWidth(UnityGui.ButtonStyle, EnumHelper.GetDescription(enumType, enumValue));
                if (width > maxWidth)
                    maxWidth = width;
            }
            return maxWidth;
        }

        private TupleEditorState GetState(IEntryBinding entry)
        {
            return _states.GetValue(entry, parent => new TupleEditorState(parent));
        }

        // 对比父条目已提交值引用识别外部写入；元素自身的合并写入通过回调标记，不触发清空。
        // 元组定长，元素投影布局不随值变化失效，无需重建。
        private static void SyncExternalValueChange(TupleEditorState state)
        {
            var currentCommitted = state.Parent.Value;
            if (ReferenceEquals(currentCommitted, state.LastCommittedValue))
                return;

            var isSelfWrite = state.HasSelfWriteSinceDraw;
            state.LastCommittedValue = currentCommitted;
            state.HasSelfWriteSinceDraw = false;
            if (!isSelfWrite)
            {
                foreach (var element in state.Elements)
                    element.EditBuffer.Clear();
            }
        }

        /// <summary>
        /// 缓存单个元组父条目的全部元素投影绑定及外部值变化跟踪状态。
        /// </summary>
        private sealed class TupleEditorState
        {
            /// <summary>
            /// 获取所属的元组父条目绑定。
            /// </summary>
            public IEntryBinding Parent { get; }

            /// <summary>
            /// 获取按元素下标排列的元素投影绑定数组；元组定长，绑定在状态生命周期内不变。
            /// </summary>
            public TupleElementBinding[] Elements { get; }

            /// <summary>
            /// 获取或设置上次绘制时观察到的父条目已提交值实例。
            /// </summary>
            public object LastCommittedValue { get; set; }

            /// <summary>
            /// 获取或设置自上次绘制以来元素是否向父条目写入过整体值。
            /// </summary>
            public bool HasSelfWriteSinceDraw { get; set; }

            /// <summary>
            /// 获取父元组类型的 Item1..ItemN 公共字段，供组内宽度测量反射读取各元素文本。
            /// </summary>
            public System.Reflection.FieldInfo[] ItemFields { get; }

            /// <summary>
            /// 校验父绑定并按元素类型数组建立全部元素投影。
            /// </summary>
            /// <exception cref="ArgumentException">父绑定值类型不是受支持的封闭元组泛型。</exception>
            public TupleEditorState(IEntryBinding parent)
            {
                if (!TupleElementBinding.IsSupportedTupleType(parent.ValueType, out var elementTypes))
                    throw new ArgumentException($"Parent entry value type is not a supported tuple: {parent.ValueType?.FullName}.", nameof(parent));

                Parent = parent;
                var onParentValueWritten = (Action)(() => HasSelfWriteSinceDraw = true);
                Elements = elementTypes
                    .Select((_, index) => new TupleElementBinding(parent, index, onParentValueWritten))
                    .ToArray();
                ItemFields = elementTypes
                    .Select((_, index) => parent.ValueType.GetField($"Item{index + 1}"))
                    .ToArray();
                LastCommittedValue = parent.Value;
            }
        }
    }
}
