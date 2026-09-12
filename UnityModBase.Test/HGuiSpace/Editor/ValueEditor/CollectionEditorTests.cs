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
    /// 集合展开式编辑器把有序集合条目投影为逐元素绑定并复用注册表子编辑器绘制：
    /// 摘要按钮切换展开状态，元素写入合并为新集合整体写回，增删前先提交未到期的元素输入，
    /// 外部写入清空元素暂存并按需重建投影，自身写入且元素数不变时保留暂存。
    /// </summary>
    public class CollectionEditorTests : IDisposable
    {
        private readonly ValueEditorRegistry _registry = new ValueEditorRegistry();

        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new CollectionEditor(null, _registry.GetEditor));
            Assert.Equal("unityGui", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenEditorResolverIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var exception = Assert.Throws<ArgumentNullException>(() => new CollectionEditor(unityGuiMock.Object, null));
            Assert.Equal("editorResolver", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithUnityGui_StoresProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Theory]
        [InlineData(typeof(List<int>), true)]
        [InlineData(typeof(int[]), true)]
        [InlineData(typeof(IList<string>), true)]
        [InlineData(typeof(List<FieldType>), true)]
        [InlineData(typeof(List<(int, string)>), true)]
        [InlineData(typeof(List<(int, FieldType)>), true)]
        [InlineData(typeof(HashSet<int>), false)]
        [InlineData(typeof(List<List<int>>), false)]
        [InlineData(typeof(List<(int, List<int>)>), false)]
        [InlineData(typeof(List<((int, int), int)>), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(int), false)]
        [InlineData(null, false)]
        public void CanEdit_WhenEntryHasVariousValueTypes_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange：有序集合且元素为基础类型、枚举或可整体编辑的元组才可投影；
            // 无序集合、嵌套集合、元素为集合或嵌套元组的组合和单值类型不可编辑。
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
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(List<int>));
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
            // Arrange：元素投影绑定不可再被本编辑器匹配，防止嵌套集合递归绘制。
            var parentMock = CreateListEntryMock(new List<int> { 1 });
            var element = new CollectionElementBinding(parentMock.Object, 0);

            // Act & Assert
            Assert.False(CreateEditor().CanEdit(element));
        }

        [Fact]
        public void DrawValue_WhenEntryOrContextIsNull_DoesNothing()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateListEntryMock(new List<int> { 1 });

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
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(typeof(HashSet<int>));

            // Act
            editor.DrawValue(entryMock.Object, new EditableGuiContext());

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenButtonNotClicked_KeepsCollapsedState()
        {
            // Arrange
            var unityGuiMock = CreateSummaryGuiMock(buttonClicked: false);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var entryMock = CreateListEntryMock(new List<int> { 1, 2 });

            // Act
            editor.DrawValue(entryMock.Object, context);

            // Assert
            Assert.Equal(string.Empty, context.ExpandedCollectionKey);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Button(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenButtonClickedTwice_TogglesExpandedCollectionKey()
        {
            // Arrange
            var unityGuiMock = CreateSummaryGuiMock(buttonClicked: true);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var entryMock = CreateListEntryMock(new List<int> { 1 });

            // Act
            editor.DrawValue(entryMock.Object, context);
            var expandedKey = context.ExpandedCollectionKey;
            editor.DrawValue(entryMock.Object, context);

            // Assert
            Assert.Equal("ListEntry", expandedKey);
            Assert.Equal(string.Empty, context.ExpandedCollectionKey);
        }

        [Fact]
        public void DrawValue_WhenPreviewExceedsWidthBudget_TruncatesSummaryText()
        {
            // Arrange：超长内容截断到预览字符预算（36）并以省略号收尾，
            // 防止摘要按钮文本换成多行把条目行撑高。
            var drawnSummaries = new List<string>();
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock
                .Setup(x => x.Button(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()))
                .Returns(false)
                .Callback<string, GUILayoutOption[]>((text, _) => drawnSummaries.Add(text));
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = new EditableGuiContext();
            var entryMock = CreateListEntryMock(new List<string> { "very-long-element", "another-much-longer-one" });

            // Act
            editor.DrawValue(entryMock.Object, context);

            // Assert：预览截断到预算内，第二个元素的尾部不再出现在摘要中。
            var summary = Assert.Single(drawnSummaries);
            Assert.EndsWith("…", summary);
            Assert.DoesNotContain("longer-one", summary);
        }

        [Fact]
        public void DrawValue_WhenEntryIsDualSlot_UsesContentButtonWithTooltip()
        {
            // Arrange：集合条目作为双元素槽位时没有独立标签，条目说明只能以悬停提示呈现。
            var buttonStyle = CreateUninitializedGuiStyle();
            var content = new GUIContent("summary");
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock.SetupGet(x => x.ButtonStyle).Returns(buttonStyle);
            unityGuiMock.Setup(x => x.GetContent(It.IsAny<string>(), It.IsAny<string>())).Returns(content);
            unityGuiMock.Setup(x => x.Button(content, buttonStyle, It.IsAny<GUILayoutOption[]>())).Returns(false);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var dualParentMock = CreateDualParentMock();
            var slot = new DualValueSlotBinding(dualParentMock.Object, 0);

            // Act
            editor.DrawValue(slot, new EditableGuiContext());

            // Assert
            unityGuiMock.Verify(x => x.Button(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()), Times.Never);
            unityGuiMock.Verify(
                x => x.Button(content, buttonStyle, It.IsAny<GUILayoutOption[]>()), Times.Once);
        }

        [Fact]
        public void DrawExtra_WhenNotExpanded_DoesNothing()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var entryMock = CreateListEntryMock(new List<int> { 1 });

            // Act
            editor.DrawExtra(entryMock.Object, new EditableGuiContext());

            // Assert
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawExtra_WhenExpanded_DrawsElementRowsAndAddRow()
        {
            // Arrange
            var unityGuiMock = CreateExpandedAreaGuiMock();
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<int> { 1, 2, 3 });

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert：三个元素行按各自投影绑定绘制，元素值类型和下标正确；
            // 每行一个移除按钮，末行一个添加按钮，整体在盒式垂直区域内对齐条目标签。
            Assert.Equal(3, drawnElements.Count);
            for (int i = 0; i < drawnElements.Count; i++)
            {
                var element = Assert.IsType<CollectionElementBinding>(drawnElements[i]);
                Assert.Equal(i, element.ElementIndex);
                Assert.Equal(typeof(int), element.ValueType);
                Assert.Same(entryMock.Object, element.Parent);
            }
            unityGuiMock.Verify(
                x => x.Button(It.Is<string>(s => IsRemoveText(s)), It.IsAny<GUILayoutOption[]>()), Times.Exactly(3));
            unityGuiMock.Verify(
                x => x.Button(It.Is<string>(s => IsAddText(s)), It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGuiMock.Verify(x => x.BeginVertical(unityGuiMock.Object.BoxStyle, It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGuiMock.Verify(x => x.EndVertical(), Times.Once);
        }

        [Fact]
        public void DrawExtra_WhenRemoveButtonClicked_CommitsImmediatelyAndSkipsRemainingRows()
        {
            // Arrange
            var unityGuiMock = CreateExpandedAreaGuiMock();
            SetupRemoveButtonClicked(unityGuiMock);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var original = new List<int> { 1, 2, 3 };
            var entryMock = CreateListEntryMock(original);

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert：点击首行行尾的移除按钮后父条目立即收到去掉该元素的新集合；
            // 本行内容在点击前已经画完，中止的是后续行（第二、三个元素不再绘制）。
            var result = Assert.IsType<List<int>>(entryMock.Object.Value);
            Assert.NotSame(original, result);
            Assert.Equal(new[] { 2, 3 }, result);
            Assert.Single(drawnElements);
        }

        [Fact]
        public void DrawExtra_WhenRemoveButtonClicked_CommitsPendingElementEditsFirst()
        {
            // Arrange：元素行存在未到期的延迟输入时执行移除，输入应先并入父集合再删除目标元素。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            _registry.RegisterEditor(CreateDelayedSubEditor(elementIndex: 1, delayedValue: 99).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<int> { 1, 2, 3 });

            // Act：第一帧对元素 1 暂存延迟输入（移除按钮不点击），第二帧点击移除元素 0。
            editor.DrawExtra(entryMock.Object, context);
            SetupRemoveButtonClicked(unityGuiMock);
            editor.DrawExtra(entryMock.Object, context);

            // Assert：延迟输入 99 先提交（[1,99,3]），随后删除元素 0 得到 [99,3]。
            var result = Assert.IsType<List<int>>(entryMock.Object.Value);
            Assert.Equal(new[] { 99, 3 }, result);
        }

        [Fact]
        public void DrawExtra_WhenAddButtonClicked_AppendsDefaultElement()
        {
            // Arrange
            var unityGuiMock = CreateExpandedAreaGuiMock();
            SetupAddButtonClicked(unityGuiMock);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<int> { 1, 2 });

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert：追加的元素使用元素类型默认值（数值为 0）并立即整体提交。
            var result = Assert.IsType<List<int>>(entryMock.Object.Value);
            Assert.Equal(new[] { 1, 2, 0 }, result);
        }

        [Fact]
        public void DrawExtra_WhenAddButtonClickedOnStringCollection_AppendsEmptyString()
        {
            // Arrange：编码层不支持 null 元素，字符串集合追加的默认值是空字符串。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            SetupAddButtonClicked(unityGuiMock);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<string> { "a" }, typeof(List<string>));

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert
            var result = Assert.IsType<List<string>>(entryMock.Object.Value);
            Assert.Equal(new[] { "a", string.Empty }, result);
        }

        [Fact]
        public void DrawExtra_WhenAddButtonClickedOnTupleCollection_AppendsExplicitDefaultTuple()
        {
            // Arrange：元组元素按各元素默认值显式构造——无参构造产生的 default 元组会把字符串位
            // 留成 null，而编码层不支持 null 元素。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            SetupAddButtonClicked(unityGuiMock);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<(int, string)> { (1, "a") });

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert
            var result = Assert.IsType<List<(int, string)>>(entryMock.Object.Value);
            Assert.Equal(new[] { (1, "a"), (0, string.Empty) }, result);
        }

        [Fact]
        public void DrawExtra_WhenTupleElementEditedViaSubEditors_CommitsMergedCollection()
        {
            // Arrange：集合的元组元素由元组编辑器并排绘制（真实数值与字符串子编辑器），
            // 数值位输入延迟到期后沿"元组元素→集合元素→父集合"两层投影合并整体写回。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            unityGuiMock
                .Setup(x => x.TextField(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()))
                .Returns((string text, GUILayoutOption[] options) => text == "1" ? "9" : text);
            // 元组元素按元组编辑器测量的本列宽度绘制。
            unityGuiMock.Setup(x => x.Width(It.IsAny<float>())).Returns((GUILayoutOption)null);
            _registry.RegisterEditor(new NumberEditor(unityGuiMock.Object));
            _registry.RegisterEditor(new StringEditor(unityGuiMock.Object));
            _registry.RegisterEditor(new TupleEditor(unityGuiMock.Object, _registry.GetEditor));
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var original = new List<(int, string)> { (1, "a") };
            var entryMock = CreateListEntryMock(original);

            // Act
            editor.DrawExtra(entryMock.Object, context);
            context.ChangeSink.FlushValue(0.5f);

            // Assert：父集合收到只替换元组数值位的新 List 实例，字符串位与其他元素语义保留。
            var result = Assert.IsType<List<(int, string)>>(entryMock.Object.Value);
            Assert.NotSame(original, result);
            Assert.Equal(new[] { (9, "a") }, result);
        }

        [Fact]
        public void DrawExtra_WhenTupleElements_MeasuresPerColumnWidthAcrossWholeCollection()
        {
            // Arrange：集合内的元组行以整个父集合为测量组，各列跨行取本列最长文本，
            // 同列元素跨行等宽——同一列表的各行元组对齐的来源。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "1")).Returns(16f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "abcd")).Returns(40f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "123456")).Returns(80f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(unityGuiMock.Object.TextFieldStyle, "x")).Returns(16f);
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(new TupleEditor(unityGuiMock.Object, _registry.GetEditor));
            _registry.RegisterEditor(CreateRecordingSubEditor(drawnElements).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<(int, string)> { (1, "abcd"), (123456, "x") });

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert：数值列取全组最长 "123456"（80+16），字符串列取最长 "abcd"（40+16）；
            // 两行的同列元素同宽，行内两列宽度互不影响。
            Assert.Equal(4, drawnElements.Count);
            Assert.Equal(96f, ((TupleElementBinding)drawnElements[0]).SuggestedWidth);
            Assert.Equal(56f, ((TupleElementBinding)drawnElements[1]).SuggestedWidth);
            Assert.Equal(96f, ((TupleElementBinding)drawnElements[2]).SuggestedWidth);
            Assert.Equal(56f, ((TupleElementBinding)drawnElements[3]).SuggestedWidth);
        }

        [Fact]
        public void DrawExtra_WhenSubEditorCommitsDelayedElementEdit_ParentReceivesNewCollection()
        {
            // Arrange：真实数值子编辑器的完整链路——文本输入延迟到期后合并为新集合并整体写回。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            unityGuiMock
                .Setup(x => x.TextField(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()))
                .Returns((string text, GUILayoutOption[] options) => text == "1" ? "9" : text);
            _registry.RegisterEditor(new NumberEditor(unityGuiMock.Object));
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var original = new List<int> { 1, 2 };
            var entryMock = CreateListEntryMock(original);

            // Act
            editor.DrawExtra(entryMock.Object, context);
            context.ChangeSink.FlushValue(0.4f);
            var valueBeforeDelayExpires = entryMock.Object.Value;
            context.ChangeSink.FlushValue(0.1f);

            // Assert
            Assert.Same(original, valueBeforeDelayExpires);
            var result = Assert.IsType<List<int>>(entryMock.Object.Value);
            Assert.NotSame(original, result);
            Assert.Equal(new[] { 9, 2 }, result);
        }

        [Fact]
        public void DrawValue_WhenExternalWriteClearsElementBuffersAndRebuildsProjection()
        {
            // Arrange：元素行存在未到期暂存时父条目被外部写入（重置、文件重载等），
            // 暂存应被清空、投影按新集合重建，回显回退到新已提交值。
            var receivedTexts = new List<string>();
            var unityGuiMock = CreateExpandedAreaGuiMock();
            unityGuiMock
                .Setup(x => x.TextField(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>()))
                .Returns((string text, GUILayoutOption[] options) =>
                {
                    receivedTexts.Add(text);
                    return text == "1" ? "9" : text;
                });
            _registry.RegisterEditor(new NumberEditor(unityGuiMock.Object));
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<int> { 1, 2 });

            // Act：首帧输入暂存；次帧无外部写入，回显暂存值 9 证明暂存生效；
            // 随后外部替换父值，第三帧回显应回退到新已提交值。
            editor.DrawValue(entryMock.Object, context);
            editor.DrawExtra(entryMock.Object, context);
            editor.DrawValue(entryMock.Object, context);
            editor.DrawExtra(entryMock.Object, context);
            entryMock.Object.Value = new List<int> { 5 };
            editor.DrawValue(entryMock.Object, context);
            editor.DrawExtra(entryMock.Object, context);

            // Assert：外部写入后元素暂存被清空，文本框回显新已提交值 5 而不是过期输入 9。
            Assert.Equal(new[] { "1", "2", "9", "2", "5" }, receivedTexts);
        }

        [Fact]
        public void DrawValue_WhenSelfWriteKeepsElementCount_PreservesElementBuffers()
        {
            // Arrange：另一元素立即提交（自身写入且元素数不变）时，未到期元素的暂存输入应保留。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            var drawnElements = new List<IEntryBinding>();
            _registry.RegisterEditor(CreateMixedSubEditor(drawnElements).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new List<int> { 1, 2 });

            // Act：首帧元素 0 暂存延迟输入、元素 1 立即提交；次帧重绘触发外部值同步。
            editor.DrawExtra(entryMock.Object, context);
            var element0 = (CollectionElementBinding)drawnElements[0];
            var bufferedBeforeSync = ValueProvider.GetValue(element0);
            editor.DrawValue(entryMock.Object, context);
            editor.DrawExtra(entryMock.Object, context);

            // Assert：自身写入且元素数不变，元素 0 的暂存输入跨帧保留。
            Assert.Equal(9, bufferedBeforeSync);
            Assert.Equal(9, ValueProvider.GetValue(element0));
        }

        [Fact]
        public void DrawExtra_WhenRemoveButtonClickedOnArrayEntry_ProducesNewArray()
        {
            // Arrange：数组条目增删后仍应产出同元素类型的数组实例。
            var unityGuiMock = CreateExpandedAreaGuiMock();
            SetupRemoveButtonClicked(unityGuiMock);
            _registry.RegisterEditor(CreateRecordingSubEditor(new List<IEntryBinding>()).Object);
            var editor = new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
            var context = CreateExpandedContext();
            var entryMock = CreateListEntryMock(new[] { 1, 2, 3 }, typeof(int[]));

            // Act
            editor.DrawExtra(entryMock.Object, context);

            // Assert
            var result = Assert.IsType<int[]>(entryMock.Object.Value);
            Assert.Equal(new[] { 2, 3 }, result);
        }

        /// <summary>创建被测编辑器；子编辑器解析复用本测试的注册表。</summary>
        private CollectionEditor CreateEditor()
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            return new CollectionEditor(unityGuiMock.Object, _registry.GetEditor);
        }

        /// <summary>创建主行摘要按钮所需的 GUI 替身。</summary>
        private static Mock<IUnityGuiProvider> CreateSummaryGuiMock(bool buttonClicked)
        {
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGuiMock.Setup(x => x.Button(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>())).Returns(buttonClicked);
            return unityGuiMock;
        }

        /// <summary>创建展开区绘制所需的全套 GUI 替身；所有按钮默认不点击。</summary>
        private static Mock<IUnityGuiProvider> CreateExpandedAreaGuiMock()
        {
            var boxStyle = CreateUninitializedGuiStyle();
            var textFieldStyle = CreateUninitializedGuiStyle();
            var toggleStyle = CreateUninitializedGuiStyle();
            var buttonStyle = CreateUninitializedGuiStyle();
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(It.IsAny<bool>())).Returns((GUILayoutOption)null);
            unityGuiMock.Setup(x => x.Button(It.IsAny<string>(), It.IsAny<GUILayoutOption[]>())).Returns(false);
            unityGuiMock.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGuiMock.Setup(x => x.EndHorizontal());
            unityGuiMock.Setup(x => x.Space(It.IsAny<float>()));
            unityGuiMock.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGuiMock.SetupGet(x => x.TextFieldStyle).Returns(textFieldStyle);
            unityGuiMock.SetupGet(x => x.ToggleStyle).Returns(toggleStyle);
            unityGuiMock.SetupGet(x => x.ButtonStyle).Returns(buttonStyle);
            unityGuiMock.Setup(x => x.CalcSizeWidth(textFieldStyle, It.IsAny<string>())).Returns(0f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(toggleStyle, It.IsAny<string>())).Returns(0f);
            unityGuiMock.Setup(x => x.CalcSizeWidth(buttonStyle, It.IsAny<string>())).Returns(0f);
            // UnityEngine 参数用具体实例匹配：It.IsAny<GUIStyle>() 会触发 Moq 解析引擎类型属性，
            // 在缺少 UnityEngine.SharedInternalsModule 的测试环境直接抛 FileNotFoundException。
            unityGuiMock.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGuiMock.Setup(x => x.EndVertical());
            return unityGuiMock;
        }

        private static void SetupRemoveButtonClicked(Mock<IUnityGuiProvider> unityGuiMock)
        {
            unityGuiMock
                .Setup(x => x.Button(It.Is<string>(s => IsRemoveText(s)), It.IsAny<GUILayoutOption[]>()))
                .Returns(true);
        }

        private static void SetupAddButtonClicked(Mock<IUnityGuiProvider> unityGuiMock)
        {
            unityGuiMock
                .Setup(x => x.Button(It.Is<string>(s => IsAddText(s)), It.IsAny<GUILayoutOption[]>()))
                .Returns(true);
        }

        // 文案匹配不依赖全局语言：双语任一命中即视为对应按钮（并行测试可能切换默认语言）。
        private static bool IsRemoveText(string text)
        {
            return text == TranslatorResource.CollectionRemove.Chinese || text == TranslatorResource.CollectionRemove.English;
        }

        private static bool IsAddText(string text)
        {
            return text == TranslatorResource.CollectionAdd.Chinese || text == TranslatorResource.CollectionAdd.English;
        }

        private static EditableGuiContext CreateExpandedContext()
        {
            return new EditableGuiContext { ExpandedCollectionKey = "ListEntry" };
        }

        /// <summary>
        /// 创建值为指定集合并带独立编辑缓冲区的条目严格替身。
        /// </summary>
        private static Mock<IEntryBinding> CreateListEntryMock(object value, Type valueType = null, string key = "ListEntry")
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType ?? value.GetType());
            entryMock.SetupProperty(x => x.Value, value);
            entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupGet(x => x.Name).Returns(new Translator("列表", "List"));
            entryMock.SetupGet(x => x.Description).Returns(new Translator());
            return entryMock;
        }

        private static Mock<IEntryBinding> CreateDualParentMock()
        {
            var parentMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            parentMock.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<List<int>, int>));
            parentMock.SetupProperty(x => x.Value, new EntryValue<List<int>, int>(new List<int> { 1 }, 2));
            parentMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            parentMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
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

        /// <summary>创建对指定下标元素写入长延迟暂存输入的子编辑器替身。</summary>
        private static Mock<IValueEditor> CreateDelayedSubEditor(int elementIndex, object delayedValue)
        {
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((element, context) =>
                {
                    if (((CollectionElementBinding)element).ElementIndex == elementIndex)
                        context.ChangeSink.SetValue(element, delayedValue, delay: 10.0f);
                });
            subEditorMock.Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            return subEditorMock;
        }

        /// <summary>创建元素 0 写长延迟暂存、元素 1 立即提交的子编辑器替身。</summary>
        private static Mock<IValueEditor> CreateMixedSubEditor(List<IEntryBinding> drawnElements)
        {
            var subEditorMock = new Mock<IValueEditor>(MockBehavior.Strict);
            subEditorMock.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            subEditorMock
                .Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()))
                .Callback<IEntryBinding, EditableGuiContext>((element, context) =>
                {
                    drawnElements.Add(element);
                    if (((CollectionElementBinding)element).ElementIndex == 0)
                        context.ChangeSink.SetValue(element, 9, delay: 10.0f);
                    else
                        context.ChangeSink.SetValue(element, 8);
                });
            subEditorMock.Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            return subEditorMock;
        }

        // GUIStyle 无参构造在无引擎环境会抛异常，测试统一使用未初始化实例（见 GroupEditorTests 的同名做法）。
        private static GUIStyle CreateUninitializedGuiStyle()
        {
            var style = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(style);
            return style;
        }

        public void Dispose()
        {
            _registry.Dispose();
        }

        private enum FieldType
        {
            First,
            Second,
        }
    }
}
