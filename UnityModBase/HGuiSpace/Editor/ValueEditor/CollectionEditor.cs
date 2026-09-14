using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityModBase.BSpace;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 有序集合条目的展开式编辑器：把一维数组、<see cref="System.Collections.Generic.IList{T}"/> 实现或相应接口声明的条目
    /// 投影为逐元素的 <see cref="CollectionElementBinding"/> 绑定，按元素类型复用所属注册表
    /// <see cref="ValueEditorRegistry"/> 中已注册的子编辑器绘制控件，并提供整行的移除与追加操作。
    /// 主行仅绘制元素数与预览摘要，展开区由 <see cref="EditableGuiContext.ExpandedCollectionKey"/> 控制。
    /// </summary>
    /// <remarks>
    /// 元素绑定按父条目弱表缓存并在下标布局失效时整体重建：自身写入且元素数不变（纯元素修改）时保留绑定与暂存输入；
    /// 增删元素或外部写入（重置、文件重载等）会清空全部元素暂存并按需重建，避免过期回显或错位下标覆盖最新值。
    /// 增删前先提交各元素行未到期的延迟输入，减少整行替换时的输入丢失。元素绑定不会再匹配本编辑器，避免递归绘制。
    /// 大集合下的开销由三处机制约束：父集合元素按实例缓存为共享快照，全部元素按下标直接读取，
    /// 每次值替换只额外付出一次快照重建；摘要文本按集合实例缓存，收起状态不再逐事件全量枚举；
    /// 展开区元素数超过单页上限时按页裁剪绘制范围，同时布局的控件数不随元素总数增长。
    /// </remarks>
    public sealed class CollectionEditor : IValueEditor
    {
        // 摘要预览最多展示的元素数，其余以省略号表示。
        private const int PreviewElementCount = 3;

        // 预览文本的字符数预算，超出部分截断为省略号，防止长内容把摘要按钮换行成多行而撑高条目行。
        private const int MaxPreviewLength = 36;

        // 展开区单页元素数上限：超过后进入分页绘制，每趟布局的控件数被约束在单页规模。
        private const int PageSize = 100;

        // 父条目到元素投影状态的弱表：元素绑定与父条目同生命周期，界面重建绑定树后旧元素随父条目一起回收。
        private readonly ConditionalWeakTable<IEntryBinding, CollectionEditorState> _states = new ConditionalWeakTable<IEntryBinding, CollectionEditorState>();
        private readonly Func<IEntryBinding, IValueEditor> _editorResolver;

        /// <summary>
        /// 获取集合摘要按钮和展开区布局使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建有序集合条目的展开式编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制摘要按钮和展开区布局的 IMGUI 提供器。</param>
        /// <param name="editorResolver">用于选择元素子编辑器的解析器。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 <c>null</c>。</exception>
        public CollectionEditor(IUnityGuiProvider unityGui, Func<IEntryBinding, IValueEditor> editorResolver)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
            _editorResolver = editorResolver ?? throw new ArgumentNullException(nameof(editorResolver));
        }

        /// <summary>
        /// 判断绑定是否可投影为逐元素编辑：要求值类型为可按下标访问的有序集合（一维数组或实现
        /// <see cref="System.Collections.Generic.IList{T}"/>，含接口声明）且元素类型为受支持的基础类型、枚举或可整体编辑的元组；
        /// 元素投影绑定自身不可再被本编辑器匹配，防止嵌套集合递归绘制。绑定为 <c>null</c> 时不可编辑。
        /// </summary>
        public bool CanEdit(IEntryBinding entry)
        {
            return IsEditable(entry);
        }

        /// <summary>
        /// 绘制元素数与预览摘要按钮，点击切换展开状态。
        /// 绘制前同步外部值变化：非本编辑器写入的父条目新值会清空元素暂存并按需重建投影。
        /// 摘要文本按集合实例缓存：集合未被替换时各 IMGUI 事件趟直接复用，不再重复枚举与格式化。
        /// </summary>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null || context == null)
                return;
            if (!IsEditable(entry))
                return;

            var state = GetState(entry);
            SyncExternalValueChange(state);

            var collection = ValueProvider.GetValidValue(entry) as IEnumerable;
            var summary = GetCachedSummary(state, collection);

            bool buttonClicked;
            if (entry is DualValueSlotBinding)
            {
                // 槽位控件没有独立的名称标签，条目说明只能以悬停提示呈现；
                // GUIContent 按钮重载要求显式样式，传入 ButtonStyle 以保持默认按钮外观。
                buttonClicked = UnityGui.Button(
                    UnityGui.GetContent(summary, entry.Description),
                    UnityGui.ButtonStyle,
                    UnityGui.ExpandWidth(true));
            }
            else
            {
                buttonClicked = UnityGui.Button(summary, UnityGui.ExpandWidth(true));
            }

            if (buttonClicked)
            {
                if (entry.Key == context.ExpandedCollectionKey)
                    context.ExpandedCollectionKey = string.Empty;
                else
                    context.ExpandedCollectionKey = entry.Key;
            }
        }

        /// <summary>
        /// 绘制展开的逐元素编辑区：每行一个移除按钮加元素子编辑器的主值控件，末行提供追加按钮。
        /// 增删发生时当帧中止剩余行的绘制，下一帧按重建后的投影继续。
        /// 元素数超过单页上限时只绘制当前页范围并附加分页栏，每趟布局的控件数约束在单页规模。
        /// </summary>
        public void DrawExtra(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null || context == null)
                return;
            if (!IsEditable(entry))
                return;
            if (entry.Key != context.ExpandedCollectionKey)
                return;

            var state = GetState(entry);
            SyncExternalValueChange(state);

            UnityGui.BeginHorizontal();
            try
            {
                UnityGui.Space(context.GetEntryLabelWidth(context.SelectedGroupKey));

                UnityGui.BeginVertical(UnityGui.BoxStyle);
                try
                {
                    // 页码每趟收敛到有效范围：增删或外部写入改变元素数后，越界的当前页在绘制前回落。
                    state.Page = ClampPage(state.Page, state.Elements.Length);
                    var startIndex = state.Page * PageSize;
                    var endIndex = Math.Min(state.Elements.Length, startIndex + PageSize);
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (!DrawElementRow(state, i, context))
                            return;
                    }
                    DrawPaginationRow(state);
                    DrawAddRow(state, context);
                }
                finally
                {
                    UnityGui.EndVertical();
                }

                UnityGui.Space(context.TrailingActionWidth);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }
        }

        /// <summary>
        /// 释放编辑器；子编辑器归所属注册表所有，元素绑定随父条目弱引用回收，此实现不持有需释放状态。
        /// </summary>
        public void Dispose()
        {
        }

        private static bool IsEditable(IEntryBinding entry)
        {
            if (entry == null || entry is CollectionElementBinding)
                return false;
            if (!CollectionElementBinding.IsOrderedCollectionType(entry.ValueType, out var elementType))
                return false;

            // 元素限定基础类型、枚举或可整体编辑的元组：这些类型有现成子编辑器（元组经元组编辑器逐元素绘制），
            // 且追加元素的默认值构造是明确的。元素为集合或嵌套元组的组合整体拒绝，避免集合可展开但元素行不可编辑的不一致。
            if (!EntryModel.IsPrimitiveType(elementType) && !EntryModel.IsEnumType(elementType) &&
                !TupleElementBinding.IsEditableTupleType(elementType))
                return false;

            return entry.Metadata == null;
        }

        // 绘制单个元素行；移除按钮位于行尾，与元素内容保持视觉分隔。
        // 返回 false 表示发生了增删、投影已重建，调用方应中止本轮绘制（本行内容此时已经画完，中止的是后续行）。
        private bool DrawElementRow(CollectionEditorState state, int index, EditableGuiContext context)
        {
            var element = state.Elements[index];
            var editor = _editorResolver(element);
            UnityGui.BeginHorizontal();
            try
            {
                editor.DrawValue(element, context);

                if (UnityGui.Button(TranslatorResource.CollectionRemove, UnityGui.ExpandWidth(false)))
                {
                    RemoveElement(state, index, context);
                    return false;
                }
            }
            finally
            {
                UnityGui.EndHorizontal();
            }

            editor.DrawExtra(element, context);
            return true;
        }

        // 分页栏：元素数超过单页上限时绘制翻页按钮与页码指示；单页容纳全部元素时不绘制，保持小集合的原有形态。
        // 到达首页或末页后按钮保持可用但页码被钳制，不再前进。
        private void DrawPaginationRow(CollectionEditorState state)
        {
            var pageCount = GetPageCount(state.Elements.Length);
            if (pageCount <= 1)
                return;

            UnityGui.BeginHorizontal();
            try
            {
                if (UnityGui.Button(TranslatorResource.CollectionPreviousPage, UnityGui.ExpandWidth(true)))
                    state.Page = Math.Max(0, state.Page - 1);
                UnityGui.Label(
                    UnityGui.GetContent(string.Format(TranslatorResource.CollectionPageIndicator, state.Page + 1, pageCount)),
                    UnityGui.ExpandWidth(false));
                if (UnityGui.Button(TranslatorResource.CollectionNextPage, UnityGui.ExpandWidth(true)))
                    state.Page = Math.Min(pageCount - 1, state.Page + 1);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }
        }

        private void DrawAddRow(CollectionEditorState state, EditableGuiContext context)
        {
            UnityGui.BeginHorizontal();
            try
            {
                if (UnityGui.Button(TranslatorResource.CollectionAdd, UnityGui.ExpandWidth(true)))
                    AddElement(state, context);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }
        }

        // 移除指定下标的元素：先提交各元素行未到期的输入，再以刷新后的共享快照为底稿去掉目标元素后整体替换。
        private void RemoveElement(CollectionEditorState state, int index, EditableGuiContext context)
        {
            CommitElements(state, context);
            state.HasSelfWriteSinceDraw = true;

            // 提交可能已替换父集合实例，读取底稿前先刷新共享快照。
            var elements = (object[])state.RefreshSnapshot().Clone();
            if (index >= elements.Length)
                return;

            var remaining = new object[elements.Length - 1];
            Array.Copy(elements, 0, remaining, 0, index);
            Array.Copy(elements, index + 1, remaining, index, remaining.Length - index);

            WriteNewCollection(state, remaining, context);
        }

        // 追加一个默认值元素：先提交各元素行未到期的输入，再以刷新后的共享快照为底稿追加元素后整体替换，并把当前页切到包含新元素的末页。
        private void AddElement(CollectionEditorState state, EditableGuiContext context)
        {
            CommitElements(state, context);
            state.HasSelfWriteSinceDraw = true;

            var elements = (object[])state.RefreshSnapshot().Clone();
            Array.Resize(ref elements, elements.Length + 1);
            elements[elements.Length - 1] = CreateDefaultElement(state.ElementType);

            WriteNewCollection(state, elements, context);
            state.Page = GetPageCount(elements.Length) - 1;
        }

        private void WriteNewCollection(CollectionEditorState state, object[] elements, EditableGuiContext context)
        {
            try
            {
                var newCollection = CollectionElementBinding.CreateCollection(state.Parent.ValueType, state.ElementType, elements);
                context.ChangeSink.SetValue(state.Parent, newCollection);
                // 等值写入（如追加结果与原集合内容相同）会被父条目忽略而保持旧实例，新集合未生效，不能采纳为快照来源。
                if (ReferenceEquals(state.Parent.Value, newCollection))
                    state.AdoptSnapshot(newCollection, elements);
            }
            catch (NotSupportedException ex)
            {
                // 集合构造失败（如类型缺少可用创建路径）只记录日志不传播，避免异常中断整帧 GUI 绘制。
                BLog.Error($"Failed to write collection entry '{state.Parent.Key}'. ElementType='{state.ElementType.FullName}'.", ex);
            }
        }

        // 提交各元素行的暂存输入，使增删前的有效修改先并入父集合。
        private static void CommitElements(CollectionEditorState state, EditableGuiContext context)
        {
            foreach (var element in state.Elements)
                context.ChangeSink.Commit(element);
        }

        private static object CreateDefaultElement(Type elementType)
        {
            // 字符串元素用空字符串：编码层不支持 null 元素，default(string) 会产生不可保存的值。
            if (elementType == typeof(string))
                return string.Empty;

            // 元组元素按各元素默认值显式构造：无参构造产生的 default 元组会把字符串位留成 null，同样不可保存。
            // 元组的元素已由可编辑判定约束为标量，递归深度封顶两层。
            if (TupleElementBinding.IsSupportedTupleType(elementType, out var tupleElementTypes))
                return Activator.CreateInstance(elementType, tupleElementTypes.Select(CreateDefaultElement).ToArray());

            return Activator.CreateInstance(elementType);
        }

        // 摘要文本按集合实例缓存：集合未被替换时各事件趟直接复用，调用方传入的集合引用是缓存的唯一失效条件。
        private static string GetCachedSummary(CollectionEditorState state, IEnumerable collection)
        {
            if (!ReferenceEquals(collection, state.SummarySource))
            {
                state.SummaryText = FormatSummary(state.ElementType, collection);
                state.SummarySource = collection;
            }

            return state.SummaryText;
        }

        // 摘要只依赖元素数和前几个元素：计数优先取 ICollection.Count（数组和 List<T> 等常见实现可用），
        // 预览仅枚举头部元素，避免大集合在每个事件趟为生成摘要做全量枚举与装箱分配。
        private static string FormatSummary(Type elementType, IEnumerable collection)
        {
            var count = 0;
            var filled = 0;
            var preview = new object[PreviewElementCount];
            if (collection != null)
            {
                if (collection is ICollection collectionWithCount)
                {
                    count = collectionWithCount.Count;
                    foreach (var item in collection)
                    {
                        if (filled == preview.Length)
                            break;
                        preview[filled++] = item;
                    }
                }
                else
                {
                    // 防御回退：非 ICollection 的有序集合一次枚举同时计数和收集预览。
                    foreach (var item in collection)
                    {
                        if (filled < preview.Length)
                            preview[filled++] = item;
                        count++;
                    }
                }
            }

            if (count == 0)
                return string.Format(TranslatorResource.CollectionCount, count);

            var previewBuilder = new StringBuilder();
            for (int i = 0; i < filled; i++)
            {
                if (i > 0)
                    previewBuilder.Append(", ");

                var text = preview[i]?.ToString() ?? string.Empty;
                // 字符串元素加引号与配置编码层口径一致，使空字符串在预览中可见。
                if (elementType == typeof(string))
                    text = $"\"{text}\"";
                previewBuilder.Append(text);
            }

            var previewText = previewBuilder.ToString();
            if (previewText.Length > MaxPreviewLength)
                previewText = previewText.Substring(0, MaxPreviewLength) + "…";
            else if (count > filled)
                previewText += ", …";

            return string.Format(TranslatorResource.CollectionSummary, count, previewText);
        }

        // 按元素数计算总页数；不足一页按一页计，分页栏据此决定是否绘制。
        private static int GetPageCount(int elementCount)
        {
            return Math.Max(1, (elementCount + PageSize - 1) / PageSize);
        }

        // 把页码收敛到有效范围；元素数变化（增删或外部写入）后由绘制入口调用，防止停留在越界页。
        private static int ClampPage(int page, int elementCount)
        {
            return Math.Min(Math.Max(0, page), GetPageCount(elementCount) - 1);
        }

        private CollectionEditorState GetState(IEntryBinding entry)
        {
            return _states.GetValue(entry, parent => new CollectionEditorState(parent));
        }

        // 对比父条目已提交值引用识别状态失效：自身写入且元素数不变时按下标投影仍有效；
        // 增删（自身或外部）与外部写入会清空元素暂存，元素数变化时整体重建投影绑定。
        // 引用变化时先重建共享快照：元素数取自快照长度，元素绑定随后的读取按下标直接命中，免去逐元素从头枚举。
        private static void SyncExternalValueChange(CollectionEditorState state)
        {
            var currentCommitted = state.Parent.Value;
            if (ReferenceEquals(currentCommitted, state.LastCommittedValue))
                return;

            var isSelfWrite = state.HasSelfWriteSinceDraw;
            var snapshot = state.RefreshSnapshot();
            var newCount = snapshot.Length;
            var oldCount = state.Elements.Length;
            state.LastCommittedValue = currentCommitted;
            state.HasSelfWriteSinceDraw = false;

            if (isSelfWrite && newCount == oldCount)
                return;

            foreach (var element in state.Elements)
                element.EditBuffer.Clear();
            if (newCount != oldCount)
                state.RebuildElements();
        }

        /// <summary>
        /// 缓存单个集合父条目的元素投影绑定、共享元素快照、摘要文本及外部值变化跟踪状态。
        /// </summary>
        private sealed class CollectionEditorState : ICollectionElementSnapshotSink
        {
            /// <summary>
            /// 获取所属的有序集合父条目绑定。
            /// </summary>
            public IEntryBinding Parent { get; }

            /// <summary>
            /// 获取父集合的元素类型。
            /// </summary>
            public Type ElementType { get; }

            /// <summary>
            /// 获取按当前下标布局缓存的元素投影绑定数组。
            /// </summary>
            public CollectionElementBinding[] Elements { get; private set; }

            /// <summary>
            /// 获取或设置上次绘制时观察到的父条目已提交值实例。
            /// </summary>
            public object LastCommittedValue { get; set; }

            /// <summary>
            /// 获取或设置自上次绘制以来元素是否向父条目写入过整体值。
            /// </summary>
            public bool HasSelfWriteSinceDraw { get; set; }

            /// <summary>
            /// 获取或设置展开区当前页码（从 0 起）；绘制入口每趟收敛到有效范围。
            /// </summary>
            public int Page { get; set; }

            /// <summary>
            /// 获取或设置摘要文本缓存对应的集合实例。
            /// </summary>
            public object SummarySource { get; set; }

            /// <summary>
            /// 获取或设置缓存的摘要文本。
            /// </summary>
            public string SummaryText { get; set; }

            // 共享快照的来源集合实例；与父条目当前值一致时快照可被全部元素绑定按下标直接读取。
            private object _snapshotSource;

            /// <summary>
            /// 获取或设置与 <see cref="_snapshotSource"/> 对应的元素快照数组。
            /// </summary>
            private object[] Snapshot { get; set; } = Array.Empty<object>();

            /// <summary>
            /// 校验父绑定并按当前父集合长度建立元素投影。
            /// </summary>
            /// <exception cref="ArgumentException">父绑定值类型不是可按下标访问的有序集合。</exception>
            public CollectionEditorState(IEntryBinding parent)
            {
                if (!CollectionElementBinding.IsOrderedCollectionType(parent.ValueType, out var elementType))
                    throw new ArgumentException($"Parent entry value type is not an ordered collection: {parent.ValueType?.FullName}.", nameof(parent));

                Parent = parent;
                ElementType = elementType;
                LastCommittedValue = parent.Value;
                RebuildElements();
            }

            /// <inheritdoc/>
            public object[] GetSnapshotIfCurrent()
            {
                return ReferenceEquals(Parent.Value, _snapshotSource) ? Snapshot : null;
            }

            /// <inheritdoc/>
            public void AdoptSnapshot(object collection, object[] elements)
            {
                _snapshotSource = collection;
                Snapshot = elements;
            }

            /// <summary>
            /// 确保共享快照与父条目当前值一致：引用不匹配时全量枚举重建一次，匹配时直接返回现有快照。
            /// </summary>
            /// <returns>与父条目当前值匹配的元素快照数组。</returns>
            public object[] RefreshSnapshot()
            {
                var collection = Parent.Value;
                if (!ReferenceEquals(collection, _snapshotSource))
                    AdoptSnapshot(collection, CollectionElementBinding.CopyElements(collection as IEnumerable));

                return Snapshot;
            }

            /// <summary>
            /// 按当前父集合长度重建全部元素投影绑定；旧绑定的暂存输入由调用方决定是否清空。
            /// </summary>
            public void RebuildElements()
            {
                var snapshot = RefreshSnapshot();
                var onParentValueWritten = (Action)(() => HasSelfWriteSinceDraw = true);

                var elements = new CollectionElementBinding[snapshot.Length];
                for (int i = 0; i < snapshot.Length; i++)
                    elements[i] = new CollectionElementBinding(Parent, i, onParentValueWritten, this);
                Elements = elements;
            }
        }
    }
}
