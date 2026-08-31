using System;
using System.Collections.Generic;
using Moq;
using UnityEngine;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 双元素组合编辑器把值为封闭 EntryValue&lt;,&gt; 的 <see cref="IEntryMultipleBinding"/> 条目投影为两个槽位绑定，
    /// 槽位说明取自父绑定的分元素说明数组；并复用所属注册表按槽位值类型选出的子编辑器绘制；
    /// 元素写入合并为整体值写回父条目，外部值变化会清空槽位暂存。
    /// </summary>
    public class DualValueEditorTests : IDisposable
    {
        private static readonly Translator BaseDescription = new Translator("整体说明", "Overall");
        private static readonly Translator Slot0Description = new Translator("元素一", "First");
        private static readonly Translator Slot1Description = new Translator("元素二", "Second");

        private readonly ValueEditorRegistry _registry = new ValueEditorRegistry();

        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new DualValueEditor(null, _registry.GetEditor));
            Assert.Equal("unityGui", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithUnityGui_StoresProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new DualValueEditor(unityGuiMock.Object, _registry.GetEditor);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Theory]
        [InlineData(typeof(EntryValue<int, float>), true)]
        [InlineData(typeof(EntryValue<string, string>), true)]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(KeyValuePair<int, string>), false)]
        [InlineData(typeof(ConventionalDualValue<int, string>), false)]
        public void CanEdit_WhenMultipleBindingValueTypeIsClosedEntryValueGeneric_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange：多元素绑定上只识别 EntryValue<,> 的封闭泛型，
            // 其他泛型（如 KeyValuePair<,>）与非泛型均不可编辑。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryMultipleBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CanEdit_WhenEntryIsNotMultipleBinding_ReturnsFalse()
        {
            // Arrange：即使值类型是 EntryValue<,>，未实现多元素契约的绑定拿不到分元素说明，
            // 组合编辑器拒绝接管，由注册表回退到占位编辑器。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanEdit_WhenEntryIsNull_ReturnsFalse()
        {
            // Arrange
            var editor = CreateEditor();

            // Act
            var result = editor.CanEdit(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanEdit_WhenEntryIsSlotBinding_ReturnsFalse()
        {
            // Arrange：槽位投影绑定不可再被本编辑器匹配，防止嵌套双元素值递归绘制。
            var editor = CreateEditor();
            var parentMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var slot0 = new DualValueSlotBinding(parentMock.Object, 0);

            // Act
            var result = editor.CanEdit(slot0);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DrawValue_WhenEntryOrContextIsNull_DoesNothing()
        {
            // Arrange：空引用直接返回，不创建槽位投影也不绘制。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);

            // Act
            editor.DrawValue(null, new EditableGuiContext());
            editor.DrawValue(entryMock.Object, null);
            editor.DrawExtra(null, null);

            // Assert
            entryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenEntryIsNotMultipleBinding_DoesNothing()
        {
            // Arrange：未实现多元素契约的绑定静默跳过，不进入复合布局也不创建槽位投影。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new DualValueEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawExtra_WhenEntryIsNotMultipleBinding_DoesNothing()
        {
            // Arrange：扩展区域与主值区域遵循同一契约；非多元素绑定静默跳过，
            // 不创建槽位投影（否则会因缺少分元素说明来源在投影时失败）。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new DualValueEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));

            // Act
            editor.DrawExtra(entryMock.Object, new EditableGuiContext());

            // Assert
            entryMock.VerifyNoOtherCalls();
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenMultipleBindingCountIsNotTwo_ThrowsArgumentException()
        {
            // Arrange：槽位投影目前只支持双元素；元素数不符属于契约违反，应立即失败而不是画出残缺行。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryMultipleBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));
            entryMock.SetupGet(x => x.Count).Returns(3);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(
                () => editor.DrawValue(entryMock.Object, new EditableGuiContext()));
            Assert.Equal("parent", exception.ParamName);
        }

        [Fact]
        public void DrawValue_WhenSubEditorMatches_DrawsBothSlotsOnceInsideExpandedHorizontalArea()
        {
            // Arrange
            var unityGuiMock = CreateCompositeAreaGuiMock();
            var editor = new DualValueEditor(unityGuiMock.Object, _registry.GetEditor);
            var sliderMetadata = new UiSliderMetadata(0f, 10f, 1f);
            var entryMock = CreateDualEntryMock(
                new EntryValue<int, string>(1, "a"),
                new UiCompositeMetadata(new IUiMetadata[] { sliderMetadata, null }));
            var drawnSlots = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnSlots).Object);

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：两个槽位各绘制一次，槽位视图按元素类型、下标和分元素说明正确投影；
            // 复合值区域整体占满剩余宽度（单个 ExpandWidth(true) 选项），子编辑器在区域内部自行分配空间。
            Assert.Equal(2, drawnSlots.Count);
            Assert.NotSame(drawnSlots[0], drawnSlots[1]);
            var slot0 = Assert.IsType<DualValueSlotBinding>(drawnSlots[0]);
            Assert.Equal(0, slot0.SlotIndex);
            Assert.Equal(typeof(int), slot0.ValueType);
            Assert.Same(sliderMetadata, slot0.Metadata);
            Assert.Same(Slot0Description, slot0.Description);
            var slot1 = Assert.IsType<DualValueSlotBinding>(drawnSlots[1]);
            Assert.Equal(1, slot1.SlotIndex);
            Assert.Equal(typeof(string), slot1.ValueType);
            Assert.Null(slot1.Metadata);
            Assert.Same(Slot1Description, slot1.Description);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(
                x => x.BeginHorizontal(It.Is<GUILayoutOption[]>(options => options.Length == 1)), Times.Once);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenSubEditorThrows_StillEndsCompositeHorizontalAreaAndSkipsRemainingSlot()
        {
            // Arrange
            var unityGuiMock = CreateCompositeAreaGuiMock();
            var editor = new DualValueEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Throws(new InvalidOperationException("slot failed"));
            _registry.RegisterEditor(subEditorMock.Object);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(
                () => editor.DrawValue(entryMock.Object, new EditableGuiContext()));

            // Assert：异常向外传播时水平区域仍被关闭，且后续槽位不再绘制。
            Assert.Equal("slot failed", exception.Message);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
            subEditorMock.Verify(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenSubEditorCommitsSlot_ParentReceivesMergedTuple()
        {
            // Arrange：子编辑器对第一个槽位提交新值，父条目应收到只替换该元素的完整双元素值。
            var editor = new DualValueEditor(CreateCompositeAreaGuiMock().Object, _registry.GetEditor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((slot, context) =>
                {
                    if (slot.ValueType == typeof(int))
                        context.ChangeSink.SetValue(slot, 7);
                });
            _registry.RegisterEditor(subEditorMock.Object);

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            var result = Assert.IsType<EntryValue<int, string>>(entryMock.Object.Value);
            Assert.Equal(7, result.Value1);
            Assert.Equal("a", result.Value2);
        }

        [Fact]
        public void DelayedCommit_WhenBothSlotsHavePendingValues_BothChangesLandInParent()
        {
            // Arrange：两槽先后挂起延迟输入，各自提交不应互相覆盖。
            var editor = new DualValueEditor(CreateCompositeAreaGuiMock().Object, _registry.GetEditor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var drawnSlots = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnSlots).Object);
            var context = new EditableGuiContext();
            editor.DrawValue(entryMock.Object, context);

            // Act：槽0 输入数字文本，槽1 输入字符串文本，均在延迟到期后提交。
            context.ChangeSink.SetConvertedValue(drawnSlots[0], "7", 1.0f);
            context.ChangeSink.SetConvertedValue(drawnSlots[1], "b", 1.0f);
            context.ChangeSink.FlushValue(1.0f);

            // Assert：最终整体值同时保留两槽修改。
            var result = Assert.IsType<EntryValue<int, string>>(entryMock.Object.Value);
            Assert.Equal(7, result.Value1);
            Assert.Equal("b", result.Value2);
        }

        [Fact]
        public void DrawValue_WhenParentValueReplacedExternally_ClearsStaleSlotBuffers()
        {
            // Arrange
            var editor = new DualValueEditor(CreateCompositeAreaGuiMock().Object, _registry.GetEditor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var drawnSlots = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnSlots).Object);
            var context = new EditableGuiContext();
            editor.DrawValue(entryMock.Object, context);

            // Act：槽0 留下无效输入的回显暂存并进入延迟队列，随后父条目被外部写入新整体值。
            context.ChangeSink.SetConvertedValue(drawnSlots[0], "not-a-number", 10.0f);
            var slot0 = Assert.IsType<DualValueSlotBinding>(drawnSlots[0]);
            Assert.True(slot0.EditBuffer.IsUsing);
            entryMock.Object.Value = new EntryValue<int, string>(42, "z");
            editor.DrawValue(entryMock.Object, context);

            // Assert：外部变化清空槽位暂存，过期延迟输入到期后也不会覆盖外部值。
            Assert.False(slot0.EditBuffer.IsUsing);
            context.ChangeSink.FlushValue(100.0f);
            var result = Assert.IsType<EntryValue<int, string>>(entryMock.Object.Value);
            Assert.Equal(42, result.Value1);
            Assert.Equal("z", result.Value2);
        }

        [Fact]
        public void DrawValue_WhenParentValueWrittenBySlot_KeepsOtherSlotPendingBuffer()
        {
            // Arrange：槽位自身的合并写入不属于外部变化，不应清空另一槽位的暂存输入。
            var editor = new DualValueEditor(CreateCompositeAreaGuiMock().Object, _registry.GetEditor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var drawnSlots = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnSlots).Object);
            var context = new EditableGuiContext();
            editor.DrawValue(entryMock.Object, context);

            // Act：槽1 留下回显暂存，随后槽0 立即提交新值（整体替换父条目值）。
            context.ChangeSink.SetConvertedValue(drawnSlots[1], "typing", 10.0f);
            context.ChangeSink.SetValue(drawnSlots[0], 7);
            editor.DrawValue(entryMock.Object, context);

            // Assert：下一帧绘制后槽1 暂存仍在，父条目保留槽0 的提交值。
            var slot1 = Assert.IsType<DualValueSlotBinding>(drawnSlots[1]);
            Assert.True(slot1.EditBuffer.IsUsing);
            var result = Assert.IsType<EntryValue<int, string>>(entryMock.Object.Value);
            Assert.Equal(7, result.Value1);
        }

        [Fact]
        public void DrawExtra_WhenCalledAfterDrawValue_ReusesSameSlotInstances()
        {
            // Arrange：扩展区域与主值控件使用同一批槽位绑定，保证子编辑器跨阶段状态连续。
            var editor = new DualValueEditor(CreateCompositeAreaGuiMock().Object, _registry.GetEditor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));
            var valueSlots = new List<IEntryBinding>();
            var extraSlots = new List<IEntryBinding>();
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((slot, _) => valueSlots.Add(slot));
            subEditorMock
                .Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((slot, _) => extraSlots.Add(slot));
            _registry.RegisterEditor(subEditorMock.Object);

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());
            editor.DrawExtra(entryMock.Object, new EditableGuiContext());

            // Assert
            Assert.Equal(2, extraSlots.Count);
            Assert.Same(valueSlots[0], extraSlots[0]);
            Assert.Same(valueSlots[1], extraSlots[1]);
        }

        [Fact]
        public void GetEditor_AfterRegistered_ReturnsDualValueEditorForDualEntry()
        {
            // Arrange：登记后双元素条目由本编辑器接管，不再回退占位编辑器。
            var editor = new DualValueEditor(new Mock<IUnityGuiProvider>().Object, _registry.GetEditor);
            _registry.RegisterEditor(editor);
            var entryMock = CreateDualEntryMock(new EntryValue<int, string>(1, "a"));

            // Act
            var result = _registry.GetEditor(entryMock.Object);

            // Assert
            Assert.Same(editor, result);
        }

        private DualValueEditor CreateEditor()
        {
            return new DualValueEditor(new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object, _registry.GetEditor);
        }

        /// <summary>
        /// 创建值为 <see cref="EntryValue{T1, T2}"/> 的双元素多元素绑定严格替身；
        /// 值用 SetupProperty 提供可读写的已提交值存储，分元素说明按静态实例提供。
        /// </summary>
        private static Mock<IEntryMultipleBinding> CreateDualEntryMock(
            EntryValue<int, string> value,
            IUiMetadata metadata = null)
        {
            var entryMock = new Mock<IEntryMultipleBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));
            entryMock.SetupProperty(x => x.Value, value);
            entryMock.SetupGet(x => x.Metadata).Returns(metadata);
            entryMock.SetupGet(x => x.Count).Returns(2);
            entryMock.SetupGet(x => x.BaseDescription).Returns(BaseDescription);
            entryMock.SetupGet(x => x.ValueDescription)
                .Returns(new[] { Slot0Description, Slot1Description });
            return entryMock;
        }

        /// <summary>
        /// 创建只允许复合值区域布局调用的 IMGUI 严格替身；
        /// 子编辑器的控件绘制由各自的替身验证，不经过本提供器。
        /// </summary>
        private static Mock<IUnityGuiProvider> CreateCompositeAreaGuiMock()
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGuiMock.Setup(x => x.EndHorizontal());
            return unityGuiMock;
        }

        /// <summary>
        /// 创建记录 DrawValue 收到的槽位绑定的子编辑器替身；匹配所有传入条目。
        /// </summary>
        private static Mock<IValueEditor> CreateRecordingSubEditor(List<IEntryBinding> drawnSlots)
        {
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((slot, _) => drawnSlots.Add(slot));
            return subEditorMock;
        }

        private sealed class ConventionalDualValue<T1, T2>
        {
            public T1 Value1 { get; }

            public T2 Value2 { get; }

            public ConventionalDualValue(T1 value1, T2 value2)
            {
                Value1 = value1;
                Value2 = value2;
            }
        }

        public void Dispose()
        {
            _registry.Dispose();
        }
    }
}
