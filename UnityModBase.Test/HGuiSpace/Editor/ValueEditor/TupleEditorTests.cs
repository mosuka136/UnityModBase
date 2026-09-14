using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 元组行内组合编辑器把封闭 ValueTuple 条目投影为逐元素绑定并在同一行横向绘制：
    /// 元素写入合并为新元组实例整体写回父条目，外部值变化会清空元素暂存，
    /// 元组条目作为双元素槽位时整体不扩展宽度。
    /// </summary>
    public class TupleEditorTests : IDisposable
    {
        private readonly ValueEditorRegistry _registry = new ValueEditorRegistry();

        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new TupleEditor(null, _registry.GetEditor));
            Assert.Equal("unityGui", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenEditorResolverIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var exception = Assert.Throws<ArgumentNullException>(() => new TupleEditor(unityGuiMock.Object, null));
            Assert.Equal("editorResolver", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithUnityGui_StoresProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Theory]
        [InlineData(typeof(ValueTuple<int>), true)]
        [InlineData(typeof((int, string)), true)]
        [InlineData(typeof((int, int, int)), true)]
        [InlineData(typeof((float, bool, string, long)), true)]
        [InlineData(typeof((int, WidthEnum)), true)]
        [InlineData(typeof((int, int, int, int, int, int, int)), true)]
        [InlineData(typeof((int, List<int>)), false)]
        [InlineData(typeof(((int, int), int)), false)]
        [InlineData(typeof(List<int>), false)]
        [InlineData(typeof(string), false)]
        [InlineData(null, false)]
        public void CanEdit_WhenEntryHasVariousValueTypes_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange：封闭 1 至 7 元且元素全部为基础类型或枚举才可投影；
            // 元素为集合或嵌套元组的组合不可编辑，由注册表回退到占位编辑器。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            if (expected)
                entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CanEdit_WhenEntryHasMetadata_ReturnsFalse()
        {
            // Arrange：与简单编辑器相同的约定——带元数据的条目留给元数据驱动编辑器。
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof((int, string)));
            entryMock.SetupGet(x => x.Metadata).Returns(new UiSliderMetadata(0f, 10f, 1f));

            // Act & Assert
            Assert.False(editor.CanEdit(entryMock.Object));
        }

        [Fact]
        public void CanEdit_WhenEntryIsNull_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(CreateEditor().CanEdit(null));
        }

        [Fact]
        public void CanEdit_WhenEntryIsElementBinding_ReturnsFalse()
        {
            // Arrange：元素投影绑定不可再被本编辑器匹配，防止嵌套元组递归绘制。
            var parentMock = CreateTupleEntryMock((1, "a"));
            var element = new TupleElementBinding(parentMock.Object, 0);

            // Act & Assert
            Assert.False(CreateEditor().CanEdit(element));
        }

        [Fact]
        public void DrawValue_WhenEntryOrContextIsNull_DoesNothing()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateTupleEntryMock((1, "a"));

            // Act
            editor.DrawValue(null, new EditableGuiContext());
            editor.DrawValue(entryMock.Object, null);

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenEntryIsNotEditable_ReturnsWithoutDrawing()
        {
            // Arrange：不可编辑的条目静默跳过，不创建元素投影也不绘制。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(List<int>));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenDrawingElements_AppliesPerColumnSuggestedWidth()
        {
            // Arrange：独立元组条目按文本框样式逐列测量自身内容，各元素绑定获得本列的建议宽度。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "1234567")).Returns(72f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "hi")).Returns(24f);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateTupleEntryMock((1234567, "hi"));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：两列各自取本列测量值加内边距（72+16 与 24+16），互不影响。
            Assert.Equal(2, drawnElements.Count);
            Assert.Equal(88f, ((TupleElementBinding)drawnElements[0]).SuggestedWidth);
            Assert.Equal(40f, ((TupleElementBinding)drawnElements[1]).SuggestedWidth);
        }

        [Fact]
        public void DrawValue_WhenTupleContainsBoolean_MeasuresByToggleStyleNotValueToString()
        {
            // Arrange：布尔列的宽度不按值的 ToString（"True"/"False"）测量，
            // 而是按开关样式测量实际绘制的显示词（英文 "On"/"Off"），复选框宽度由样式计入。
            var originalLanguage = Translator.DefaultLanguage;
            Translator.DefaultLanguage = LanguageType.English;
            try
            {
                var unityGuiMock = CreateHorizontalAreaGuiMock();
                unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.ToggleStyle, "On")).Returns(40f);
                unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.ToggleStyle, "Off")).Returns(52f);
                var drawnElements = new List<IEntryBinding>();
                _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
                var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
                var entryMock = CreateTupleEntryMock((true, 1));

                // Act
                editor.DrawValue(entryMock.Object, new EditableGuiContext());

                // Assert：开关列取 On/Off 中较长的 52（控件样式测量不加内边距），数值列为默认测量 0 加内边距 16。
                Assert.Equal(52f, ((TupleElementBinding)drawnElements[0]).SuggestedWidth);
                Assert.Equal(16f, ((TupleElementBinding)drawnElements[1]).SuggestedWidth);
                unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.ToggleStyle, "On"), Times.Once);
                unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.ToggleStyle, "Off"), Times.Once);
                unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "True"), Times.Never);
                unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "False"), Times.Never);
                unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.ToggleStyle, "True"), Times.Never);
                unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.ToggleStyle, "False"), Times.Never);
            }
            finally
            {
                Translator.DefaultLanguage = originalLanguage;
            }
        }

        [Fact]
        public void DrawValue_WhenTupleContainsEnum_MeasuresByButtonStyleOfVisibleValues()
        {
            // Arrange：枚举列的宽度按该类型全部可见值的最长描述用按钮样式测量（与枚举子编辑器显示口径一致），
            // 与行的当前值无关；未标记描述时回退为枚举名。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.ButtonStyle, "Alpha")).Returns(40f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.ButtonStyle, "LongestEnumValue")).Returns(120f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.ButtonStyle, "Beta")).Returns(32f);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateTupleEntryMock((WidthEnum.Alpha, 1));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：枚举列取全部可见值中最长描述的按钮样式测量 120（控件样式测量不加内边距），数值列为默认测量 0 加内边距 16。
            Assert.Equal(120f, ((TupleElementBinding)drawnElements[0]).SuggestedWidth);
            Assert.Equal(16f, ((TupleElementBinding)drawnElements[1]).SuggestedWidth);
            unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.ButtonStyle, "LongestEnumValue"), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenElementTextContainsFullWidthCharacters_PassesRawTextToTextFieldStyle()
        {
            // Arrange：全角文本原样交给文本框样式测量，字形宽度由 Unity 字体度量，不再自行按码点加倍。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "你好世界！")).Returns(96f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "1")).Returns(16f);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateTupleEntryMock((1, "你好世界！"));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：中文列按样式测量 96 加内边距 16，数值列按自身内容测量互不影响。
            Assert.Equal(32f, ((TupleElementBinding)drawnElements[0]).SuggestedWidth);
            Assert.Equal(112f, ((TupleElementBinding)drawnElements[1]).SuggestedWidth);
            unityGuiMock.Verify(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "你好世界！"), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenOneColumnHasLongContent_DoesNotWidenSiblingColumns()
        {
            // Arrange：超长内容只加宽所在列，兄弟列仍按自身内容测量，避免单列撑破整行布局。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "1")).Returns(16f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "a-very-long-tuple-element-value")).Returns(400f);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateTupleEntryMock((1, "a-very-long-tuple-element-value"));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：长文本列为 400+16，短数值列保持 16+16，两列互不影响。
            Assert.Equal(32f, ((TupleElementBinding)drawnElements[0]).SuggestedWidth);
            Assert.Equal(416f, ((TupleElementBinding)drawnElements[1]).SuggestedWidth);
        }

        [Fact]
        public void DrawValue_WhenSubEditorMatches_DrawsAllElementsOnceInsideHorizontalArea()
        {
            // Arrange
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var entryMock = CreateTupleEntryMock((1, "a", true));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：三个元素各绘制一次，元素视图按元素类型和下标正确投影；
            // 复合值区域整体占满剩余宽度（单个 ExpandWidth(true) 选项），子编辑器在区域内部自行分配空间。
            Assert.Equal(3, drawnElements.Count);
            var element0 = Assert.IsType<TupleElementBinding>(drawnElements[0]);
            Assert.Equal(0, element0.ElementIndex);
            Assert.Equal(typeof(int), element0.ValueType);
            Assert.Same(entryMock.Object, element0.Parent);
            var element1 = Assert.IsType<TupleElementBinding>(drawnElements[1]);
            Assert.Equal(1, element1.ElementIndex);
            Assert.Equal(typeof(string), element1.ValueType);
            var element2 = Assert.IsType<TupleElementBinding>(drawnElements[2]);
            Assert.Equal(2, element2.ElementIndex);
            Assert.Equal(typeof(bool), element2.ValueType);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(
                x => x.BeginHorizontal(It.Is<GUILayoutOption[]>(options => options.Length == 1)), Times.Once);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenEntryIsDualSlot_DoesNotExpandWidth()
        {
            // Arrange：元组条目作为双元素槽位时不扩展宽度，避免挤压兄弟槽位。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var dualParentMock = CreateDualParentMock();
            var slot = new DualValueSlotBinding(dualParentMock.Object, 0);

            // Act
            editor.DrawValue(slot, new EditableGuiContext());

            // Assert
            unityGuiMock.Verify(x => x.ExpandWidth(false), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Never);
        }

        [Fact]
        public void DrawValue_WhenSubEditorThrows_StillEndsHorizontalAreaAndSkipsRemainingElement()
        {
            // Arrange
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Throws(new InvalidOperationException("element failed"));
            _registry.RegisterEditor(subEditorMock.Object);
            var entryMock = CreateTupleEntryMock((1, "a"));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(
                () => editor.DrawValue(entryMock.Object, new EditableGuiContext()));

            // Assert：异常向外传播时水平区域仍被关闭，且后续元素不再绘制。
            Assert.Equal("element failed", exception.Message);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
            subEditorMock.Verify(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenSubEditorCommitsElement_ParentReceivesMergedTuple()
        {
            // Arrange：子编辑器对第一个元素提交新值，父条目应收到只替换该元素的完整元组值。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((element, context) =>
                {
                    if (element.ValueType == typeof(int))
                        context.ChangeSink.SetValue(element, 7);
                });
            _registry.RegisterEditor(subEditorMock.Object);
            var entryMock = CreateTupleEntryMock((1, "a"));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            var result = Assert.IsType<(int, string)>(entryMock.Object.Value);
            Assert.Equal((7, "a"), result);
        }

        [Fact]
        public void DrawValue_WhenExternalWriteClearsElementBuffers()
        {
            // Arrange：元素存在未到期暂存时父条目被外部写入（重置、文件重载等），
            // 暂存应被清空，元素回显回退到新已提交值。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateOneShotStagingSubEditor(drawnElements).Object);
            var entryMock = CreateTupleEntryMock((1, "a"));

            // Act：首帧元素 0 暂存一次性延迟输入；随后外部替换父值，
            // 次帧同步应清空元素 0 的暂存（子编辑器不再补充暂存）。
            editor.DrawValue(entryMock.Object, new EditableGuiContext());
            var element0 = (TupleElementBinding)drawnElements[0];
            Assert.Equal(9, ValueProvider.GetValue(element0));
            entryMock.Object.Value = (5, "b");
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert：外部写入后元素 0 的暂存被清空，读取回退到新已提交值的第一个元素。
            Assert.Equal(5, ValueProvider.GetValue(element0));
        }

        [Fact]
        public void DrawValue_WhenTupleIsCollectionElementAndParentListUnchanged_KeepsElementBufferAcrossFrames()
        {
            // Arrange：list<(string, bool)> 的元素行由集合元素绑定投影为元组条目再由本编辑器绘制；
            // 父集合实例不变时元素读取必须引用稳定，否则每帧同步都误判为外部写入而清空暂存，
            // 延迟输入永远无法提交（回归：集合套值元组的字符串列无法输入）。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateOneShotStagingSubEditor(drawnElements).Object);
            var list = new List<(string, bool)> { ("a", true) };
            var listEntryMock = CreateCollectionEntryMock(list);
            var tupleElement = new CollectionElementBinding(listEntryMock.Object, 0);

            // Act：首帧元素 0 暂存长延迟输入；父集合未被任何路径写回，次帧同步不应清空暂存。
            editor.DrawValue(tupleElement, new EditableGuiContext());
            var stringElement = (TupleElementBinding)drawnElements[0];
            Assert.Equal(9, ValueProvider.GetValue(stringElement));
            editor.DrawValue(tupleElement, new EditableGuiContext());

            // Assert：暂存仍在，回显不回退到已提交值，父集合实例保持不变。
            Assert.Equal(9, ValueProvider.GetValue(stringElement));
            Assert.Same(list, listEntryMock.Object.Value);
        }

        [Fact]
        public void DrawExtra_ForwardsEachElementToSubEditorDrawExtra()
        {
            // Arrange
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock.Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            subEditorMock.Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            _registry.RegisterEditor(subEditorMock.Object);
            var entryMock = CreateTupleEntryMock((1, "a", true));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());
            editor.DrawExtra(entryMock.Object, new EditableGuiContext());

            // Assert：扩展区域按元素逐个转发，枚举元素的展开列表等子编辑器附加区天然可用。
            subEditorMock.Verify(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Exactly(3));
        }

        [Fact]
        public void DrawExtra_WhenEntryIsNotEditable_DoesNothing()
        {
            // Arrange：扩展区域与主值区域遵循同一契约，不可编辑的条目静默跳过。
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(List<int>));

            // Act
            editor.DrawExtra(entryMock.Object, new EditableGuiContext());

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenCollectionRowsDrawnRepeatedly_MeasuresColumnWidthsOncePerCollectionInstance()
        {
            // Arrange：集合内 150 行元组共享父集合实例，列宽测量只应按实例发生一次，
            // 后续行与后续趟全部命中缓存，文本测量次数不随行数增长。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var calcTextWidthCalls = 0;
            unityGuiMock
                .Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, It.IsAny<string>()))
                .Callback(() => calcTextWidthCalls++)
                .Returns(10f);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var collection = Enumerable.Range(1, 150).Select(i => (i, $"row{i}")).ToList();
            var parentMock = CreateCollectionEntryMock(collection);
            var elementBindings = Enumerable.Range(0, collection.Count)
                .Select(i => new CollectionElementBinding(parentMock.Object, i))
                .ToArray();

            // Act：同一集合实例的全部行各绘制一趟，再整体重复一趟。
            foreach (var binding in elementBindings)
                editor.DrawValue(binding, context);
            var callsAfterFirstPass = calcTextWidthCalls;
            foreach (var binding in elementBindings)
                editor.DrawValue(binding, context);

            // Assert：首趟仅首行触发一次全集合测量（150 行 × 2 个文本列 = 300 次），第二趟零增量。
            Assert.Equal(300, callsAfterFirstPass);
            Assert.Equal(callsAfterFirstPass, calcTextWidthCalls);
        }

        [Fact]
        public void DrawValue_WhenLayoutVersionAdvances_RemeasuresColumnWidths()
        {
            // Arrange：语言切换等布局失效会递增版本号，实例未变的缓存列宽也应随之失效并重测一次。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var calcTextWidthCalls = 0;
            unityGuiMock
                .Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, It.IsAny<string>()))
                .Callback(() => calcTextWidthCalls++)
                .Returns(10f);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var collection = Enumerable.Range(1, 10).Select(i => (i, $"row{i}")).ToList();
            var parentMock = CreateCollectionEntryMock(collection);
            var elementBindings = Enumerable.Range(0, collection.Count)
                .Select(i => new CollectionElementBinding(parentMock.Object, i))
                .ToArray();

            // Act：首趟建立缓存；递增布局版本后仅重画首行，应触发一次完整的重测量。
            foreach (var binding in elementBindings)
                editor.DrawValue(binding, context);
            var callsAfterFirstPass = calcTextWidthCalls;
            context.SetLayoutDirtyFlags();
            editor.DrawValue(elementBindings[0], context);

            // Assert：重测规模与首趟相同（10 行 × 2 列 = 20 次）。
            Assert.Equal(20, callsAfterFirstPass);
            Assert.Equal(40, calcTextWidthCalls);
        }

        [Fact]
        public void DrawValue_WhenEnumColumnAcrossPasses_MeasuresEnumWidthOncePerVersion()
        {
            // Arrange：枚举列宽只依赖类型与布局版本：首趟对全部可见枚举值测量一次，
            // 后续趟与同类型的其他行不再重复测量；版本递增后重测一轮。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            var calcButtonWidthCalls = 0;
            unityGuiMock
                .Setup(x => x.CalcSizeWidth(unityGuiMock.Object.ButtonStyle, It.IsAny<string>()))
                .Callback(() => calcButtonWidthCalls++)
                .Returns(10f);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var collection = Enumerable.Range(0, 10).Select(i => (WidthEnum.Alpha, $"row{i}")).ToList();
            var parentMock = CreateCollectionEntryMock(collection);
            var elementBindings = Enumerable.Range(0, collection.Count)
                .Select(i => new CollectionElementBinding(parentMock.Object, i))
                .ToArray();

            // Act
            foreach (var binding in elementBindings)
                editor.DrawValue(binding, context);
            var callsAfterFirstPass = calcButtonWidthCalls;
            foreach (var binding in elementBindings)
                editor.DrawValue(binding, context);
            context.SetLayoutDirtyFlags();
            editor.DrawValue(elementBindings[0], context);

            // Assert：首趟 3 个可见枚举值各测一次，第二趟零增量；版本递增后重测 3 次。
            Assert.Equal(3, callsAfterFirstPass);
            Assert.Equal(6, calcButtonWidthCalls);
        }

        [Fact]
        public void DrawValue_WhenCollectionInstanceReplaced_RemeasuresColumnWidthsForNewInstance()
        {
            // Arrange：列宽缓存以集合实例为键——集合写入必然替换实例，实例不变即内容不变；
            // 实例更换后旧测量不得复用，应按新集合内容整体重测，否则列宽停留在过期内容上。
            var unityGuiMock = CreateHorizontalAreaGuiMock();
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "1")).Returns(16f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "short")).Returns(24f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "a-much-longer-value")).Returns(400f);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var parentMock = CreateCollectionEntryMock(new List<(int, string)> { (1, "short") });
            var element0 = new CollectionElementBinding(parentMock.Object, 0);

            // Act：首趟按旧实例测得字符串列 24+16；集合被整体替换为新实例（文本更长）后再画一行。
            editor.DrawValue(element0, context);
            var stringColumn = (TupleElementBinding)drawnElements[1];
            Assert.Equal(40f, stringColumn.SuggestedWidth);
            parentMock.Object.Value = new List<(int, string)> { (1, "a-much-longer-value") };
            editor.DrawValue(element0, context);

            // Assert：新实例触发整体重测，同一列绑定上的建议宽度更新为新内容的最长文本（400+16），
            // 且测量确实发生在新实例的文本上。
            Assert.Equal(416f, stringColumn.SuggestedWidth);
            unityGuiMock.Verify(
                x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "a-much-longer-value"), Times.Once);
        }

        /// <summary>创建被测编辑器；子编辑器解析复用本测试的注册表。</summary>
        private TupleEditor CreateEditor()
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            return new TupleEditor(unityGuiMock.Object, _registry.GetEditor);
        }

        /// <summary>创建横向区域绘制所需的 GUI 替身，并提供可覆盖的样式宽度测量。</summary>
        private static Mock<IUnityGuiProvider> CreateHorizontalAreaGuiMock()
        {
            var textFieldStyle = CreateUninitializedGuiStyle();
            var toggleStyle = CreateUninitializedGuiStyle();
            var buttonStyle = CreateUninitializedGuiStyle();
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(It.IsAny<bool>())).Returns((GUILayoutOption)null);
            unityGuiMock.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGuiMock.Setup(x => x.EndHorizontal());
            unityGuiMock.SetupGet(x => x.TextFieldStyle).Returns(textFieldStyle);
            unityGuiMock.SetupGet(x => x.ToggleStyle).Returns(toggleStyle);
            unityGuiMock.SetupGet(x => x.ButtonStyle).Returns(buttonStyle);
            // 默认返回 0，使未覆盖的列落入最小宽度夹取；具体文本的测量由用例后续 Setup 覆盖。
            unityGuiMock.Setup(x => x.CalcSizeWidth(textFieldStyle, It.IsAny<string>())).Returns(0f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(toggleStyle, It.IsAny<string>())).Returns(0f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(buttonStyle, It.IsAny<string>())).Returns(0f);
            return unityGuiMock;
        }

        // GUIStyle 无参构造在无引擎环境会抛异常，测试统一使用未初始化实例（见 CollectionEditorTests 的同名做法）。
        private static GUIStyle CreateUninitializedGuiStyle()
        {
            var style = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(style);
            return style;
        }

        /// <summary>
        /// 创建值为指定元组并带独立编辑缓冲区的条目严格替身。
        /// </summary>
        private static Mock<IEntryBinding> CreateTupleEntryMock<TTuple>(TTuple value, string key = "TupleEntry")
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(TTuple));
            entryMock.SetupProperty(x => x.Value, value);
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupGet(x => x.Name).Returns(new Translator("元组", "Tuple"));
            entryMock.SetupGet(x => x.Description).Returns(new Translator());
            return entryMock;
        }

        /// <summary>
        /// 创建值为指定集合的条目严格替身，充当集合元素绑定的父条目，值用 SetupProperty 提供可读写的已提交值存储。
        /// </summary>
        private static Mock<IEntryBinding> CreateCollectionEntryMock<TCollection>(TCollection value)
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(TCollection));
            entryMock.SetupProperty(x => x.Value, value);
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            entryMock.SetupGet(x => x.Key).Returns("CollectionEntry");
            return entryMock;
        }

        private static Mock<IEntryBinding> CreateDualParentMock()
        {
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<(int, string), int>));
            parentMock.SetupProperty(x => x.Value, new EntryValue<(int, string), int>((1, "a"), 2));
            parentMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            parentMock.SetupGet(x => x.Key).Returns("DualParent");
            parentMock.SetupGet(x => x.Name).Returns(new Translator("双元素", "Dual"));
            parentMock.SetupGet(x => x.Description).Returns(new Translator());
            return parentMock;
        }

        /// <summary>创建记录每次绘制元素绑定的子编辑器替身。</summary>
        private static Mock<IValueEditor> CreateRecordingSubEditor(List<IEntryBinding> drawnElements)
        {
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((element, _) => drawnElements.Add(element));
            subEditorMock.Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            return subEditorMock;
        }

        /// <summary>创建仅首次绘制时对元素 0 暂存长延迟输入的子编辑器替身，用于验证外部写入清空暂存。</summary>
        private static Mock<IValueEditor> CreateOneShotStagingSubEditor(List<IEntryBinding> drawnElements)
        {
            var staged = false;
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((element, context) =>
                {
                    drawnElements.Add(element);
                    if (!staged && ((TupleElementBinding)element).ElementIndex == 0)
                    {
                        context.ChangeSink.SetValue(element, 9, delay: 10.0f);
                        staged = true;
                    }
                });
            subEditorMock.Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            return subEditorMock;
        }

        public void Dispose()
        {
            _registry.Dispose();
        }

        private enum WidthEnum
        {
            Alpha,
            LongestEnumValue,
            Beta,
        }
    }
}
