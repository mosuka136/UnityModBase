using System;
using System.Runtime.CompilerServices;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// 双元素条目的组合编辑器：把实现了 <see cref="IEntryMultipleBinding"/> 且值类型为封闭
    /// <see cref="EntryValue{T1,T2}"/> 的条目投影为两个 <see cref="DualValueSlotBinding"/> 槽位绑定，
    /// 槽位说明按下标取自绑定的分元素说明，并按槽位值类型复用所属注册表
    /// <see cref="ValueEditorRegistry"/> 中已注册的子编辑器绘制控件；本类自身不直接绘制值控件。
    /// </summary>
    /// <remarks>
    /// 槽位绑定按父条目弱表缓存，同一父条目跨帧复用同一实例，保证延迟提交和文本回显状态稳定。
    /// 每帧绘制前对比父条目已提交值引用：非本编辑器写入的变化（外部重置、文件重载等）会丢弃两槽位的全部暂存输入，
    /// 避免过期回显或未到期的延迟提交覆盖最新值。槽位绑定不会再匹配本编辑器，避免递归绘制。
    /// </remarks>
    public sealed class DualValueEditor : IValueEditor
    {
        // 父条目到槽位状态的弱表：槽位绑定与父条目同生命周期，界面重建绑定树后旧槽位随父条目一起回收。
        private readonly ConditionalWeakTable<IEntryBinding, DualEditorState> _slotStates = new ConditionalWeakTable<IEntryBinding, DualEditorState>();
        private readonly Func<IEntryBinding, IValueEditor> _editorResolver;

        /// <summary>
        /// 获取复合值横向区域布局使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建双元素条目的组合编辑器。
        /// </summary>
        /// <param name="unityGui">用于布局复合值横向区域的 IMGUI 提供器。</param>
        /// <param name="editorResolver">用于选择槽位子编辑器的解析器。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 <c>null</c>。</exception>
        public DualValueEditor(IUnityGuiProvider unityGui, Func<IEntryBinding, IValueEditor> editorResolver)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
            _editorResolver = editorResolver ?? throw new ArgumentNullException(nameof(editorResolver));
        }

        /// <summary>
        /// 判断绑定是否可投影为双元素槽位：要求实现 <see cref="IEntryMultipleBinding"/>
        /// （槽位分元素说明的唯一来源，仅有复合值类型不够）且值类型为封闭 <see cref="EntryValue{T1,T2}"/>；
        /// 槽位投影绑定自身不可再被本编辑器匹配，防止手工构造的嵌套双元素值递归绘制。
        /// 绑定为 <c>null</c> 时不可编辑。
        /// </summary>
        public bool CanEdit(IEntryBinding entry)
        {
            if (entry == null || entry is DualValueSlotBinding)
                return false;
            if (!(entry is IEntryMultipleBinding))
                return false;
            return DualValueSlotBinding.IsSupportedValueType(entry.ValueType);
        }

        /// <summary>
        /// 依次绘制两个槽位子编辑器的主值控件。
        /// 绘制前同步外部值变化：非本编辑器写入的父条目新值会清空两槽位的暂存输入。
        /// </summary>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null || context == null)
                return;
            // 与 CanEdit 同一约束：缺少多元素契约即缺少槽位说明来源，无从投影槽位。
            if (!(entry is IEntryMultipleBinding))
                return;

            var state = GetSlotState(entry);
            SyncExternalValueChange(state);

            // 复合值区域整体占用标签和可选尾部操作之间的剩余宽度；子编辑器再在区域内部
            // 按各自策略分配空间，避免紧凑控件直接参与外层布局后破坏行对齐。
            UnityGui.BeginHorizontal(UnityGui.ExpandWidth(true));
            try
            {
                DrawSlot(state.Slot0, context);
                DrawSlot(state.Slot1, context);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }
        }

        /// <summary>
        /// 依次绘制两个槽位子编辑器的扩展区域（如枚举展开列表、热键编辑会话）。
        /// </summary>
        public void DrawExtra(IEntryBinding entry, EditableGuiContext context)
        {
            if (entry == null || context == null)
                return;
            if (!(entry is IEntryMultipleBinding))
                return;

            var state = GetSlotState(entry);
            _editorResolver(state.Slot0).DrawExtra(state.Slot0, context);
            _editorResolver(state.Slot1).DrawExtra(state.Slot1, context);
        }

        private void DrawSlot(IEntryBinding slot, EditableGuiContext context)
        {
            _editorResolver(slot).DrawValue(slot, context);
        }

        private DualEditorState GetSlotState(IEntryBinding entry)
        {
            return _slotStates.GetValue(entry, parent => new DualEditorState(parent));
        }

        // 对比父条目已提交值引用识别外部写入；槽位自身的合并写入通过回调标记，不触发清空。
        private void SyncExternalValueChange(DualEditorState state)
        {
            var currentCommitted = state.Parent.Value;
            if (ReferenceEquals(currentCommitted, state.LastCommittedValue))
                return;

            var isSelfWrite = state.HasSelfWriteSinceDraw;
            state.LastCommittedValue = currentCommitted;
            state.HasSelfWriteSinceDraw = false;
            if (!isSelfWrite)
            {
                state.Slot0.EditBuffer.Clear();
                state.Slot1.EditBuffer.Clear();
            }
        }

        /// <summary>
        /// 释放编辑器；子编辑器归所属注册表所有，槽位绑定随父条目弱引用回收，此实现不持有需释放状态。
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// 缓存单个双元素父条目的两个槽位绑定及外部值变化跟踪状态。
        /// </summary>
        private sealed class DualEditorState
        {
            /// <summary>
            /// 获取所属的双元素父条目绑定。
            /// </summary>
            public IEntryMultipleBinding Parent { get; }

            /// <summary>
            /// 获取第一个元素的槽位绑定。
            /// </summary>
            public DualValueSlotBinding Slot0 { get; }

            /// <summary>
            /// 获取第二个元素的槽位绑定。
            /// </summary>
            public DualValueSlotBinding Slot1 { get; }

            /// <summary>
            /// 获取或设置上次绘制时观察到的父条目已提交值实例。
            /// </summary>
            public object LastCommittedValue { get; set; }

            /// <summary>
            /// 获取或设置自上次绘制以来槽位是否向父条目写入过整体值。
            /// </summary>
            public bool HasSelfWriteSinceDraw { get; set; }

            /// <summary>
            /// 校验父绑定并创建两个槽位：父绑定必须是恰好包含两个元素的多元素绑定，
            /// 槽位说明按下标取自父绑定的分元素说明。
            /// </summary>
            /// <exception cref="ArgumentException">父绑定未实现多元素契约或元素数量不为 2。</exception>
            public DualEditorState(IEntryBinding parent)
            {
                if (!(parent is IEntryMultipleBinding parentMultiple))
                    throw new ArgumentException("Parent binding must be a multiple value binding.", nameof(parent));
                if (parentMultiple.Count != 2)
                    throw new ArgumentException("Parent binding must have exactly two values.", nameof(parent));

                Parent = parentMultiple;
                var onParentValueWritten = (Action)(() => HasSelfWriteSinceDraw = true);
                Slot0 = new DualValueSlotBinding(
                    parent,
                    0,
                    onParentValueWritten,
                    parentMultiple.ValueDescription[0]);
                Slot1 = new DualValueSlotBinding(
                    parent,
                    1,
                    onParentValueWritten,
                    parentMultiple.ValueDescription[1]);
                LastCommittedValue = parent.Value;
            }
        }
    }
}
