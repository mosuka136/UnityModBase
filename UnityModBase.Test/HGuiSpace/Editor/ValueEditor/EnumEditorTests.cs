using Moq;
using System.ComponentModel;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HEnumHelper;
using UnityModBase.HProvider;

namespace UnityModBase.Test.HGuiSpace.Editor.ValueEditor
{
    public class EnumEditorTests
    {
        [Fact]
        public void EnumEditor_WhenConstructed_StoresUnityGuiProvider()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);

            // Act
            var editor = new EnumEditor(unityGuiMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
        }

        [Theory]
        [InlineData(typeof(VisibleEnum), true)]
        [InlineData(typeof(string), false)]
        public void CanEdit_WhenEntryHasDifferentValueTypesAndNoMetadata_ReturnsExpectedResult(Type valueType, bool expected)
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            if (expected)
                entryMock.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            // Act
            var result = editor.CanEdit(entryMock.Object);

            // Assert
            Assert.Equal(expected, result);
            entryMock.VerifyGet(x => x.ValueType, Times.Once);
            entryMock.VerifyGet(x => x.Metadata, expected ? Times.Once() : Times.Never());
        }

        [Fact]
        public void DrawValue_WhenEntryTypeIsNotEnum_DoesNothing()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("NotEnumEntry", typeof(string), "text");
            var state = new EditableGuiContext();
            var changeSink = state.ChangeSink;

            // Act
            editor.DrawValue(entryMock.Object, state);

            // Assert
            Assert.Equal("text", entryMock.Object.Value);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawValue_WhenButtonIsNotClicked_DoesNotChangeExpandedState()
        {
            // Arrange
            GUILayoutOption expandWidth = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
            unityGuiMock
                .Setup(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))))
                .Returns(false);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("EnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var state = new EditableGuiContext();

            // Act
            editor.DrawValue(entryMock.Object, state);

            // Assert
            Assert.Equal(string.Empty, state.ExpandedEnumKey);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenButtonClickedAndExpanded_CollapsesEntryAndClearsExpandedKey()
        {
            // Arrange
            GUILayoutOption expandWidth = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
            unityGuiMock
                .Setup(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))))
                .Returns(true);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("ExpandedEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var state = new EditableGuiContext();
            state.ExpandedEnumKey = entryMock.Object.Key;

            // Act
            editor.DrawValue(entryMock.Object, state);

            // Assert
            Assert.Equal(string.Empty, state.ExpandedEnumKey);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenButtonClickedAndAnotherEntryExpanded_CollapsesPreviousEntryAndExpandsCurrentEntry()
        {
            // Arrange
            GUILayoutOption expandWidth = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
            unityGuiMock
                .Setup(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))))
                .Returns(true);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("CurrentEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var state = new EditableGuiContext();
            state.ExpandedEnumKey = "PreviousEntry";

            // Act
            editor.DrawValue(entryMock.Object, state);

            // Assert
            Assert.Equal(entryMock.Object.Key, state.ExpandedEnumKey);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))), Times.Once);
        }

        [Fact]
        public void DrawValue_WhenButtonClickedAndNoEntryExpanded_ExpandsCurrentEntry()
        {
            // Arrange
            GUILayoutOption expandWidth = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
            unityGuiMock
                .Setup(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))))
                .Returns(true);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("CollapsedEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var state = new EditableGuiContext();

            // Act
            editor.DrawValue(entryMock.Object, state);

            // Assert
            Assert.Equal(entryMock.Object.Key, state.ExpandedEnumKey);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.Button("Visible Option", It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))), Times.Once);
        }

        [Fact]
        public void DrawExtra_WhenEntryTypeIsNotEnum_DoesNothing()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("NotEnumEntry", typeof(string), "text");

            // Act
            editor.DrawExtra(entryMock.Object, new EditableGuiContext());

            // Assert
            Assert.Equal("text", entryMock.Object.Value);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawExtra_WhenEntryIsNotExpanded_DoesNothing()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("CollapsedEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var state = new EditableGuiContext();
            state.ExpandedEnumKey = "OtherEntry";

            // Act
            editor.DrawExtra(entryMock.Object, state);

            // Assert
            Assert.Equal(VisibleEnum.Visible, entryMock.Object.Value);
            unityGuiMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawExtra_WhenSelectionDoesNotChange_KeepsExpandedStateAndEntryValue()
        {
            // Arrange
            GUIStyle boxStyle = null;
            GUILayoutOption expandWidth = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGuiMock.Setup(x => x.BeginHorizontal());
            unityGuiMock.Setup(x => x.Space(12.5f));
            unityGuiMock.Setup(x => x.BeginVertical(boxStyle, It.Is<GUILayoutOption[]>(options => options.Length == 0)));
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
            unityGuiMock
                .Setup(x => x.SelectionGrid(0, It.Is<string[]>(names => names.Length == 2 && names[0] == "Visible Option" && names[1] == "Another Visible Option"), 1, It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))))
                .Returns(0);
            unityGuiMock.Setup(x => x.EndVertical());
            unityGuiMock.Setup(x => x.Space(8.5f));
            unityGuiMock.Setup(x => x.EndHorizontal());
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("VisibleEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var state = new EditableGuiContext
            {
                SelectedGroupKey = "General",
                TrailingActionWidth = 8.5f,
            };
            state.SetEntryLabelWidth("General", 12.5f);
            state.ExpandedEnumKey = entryMock.Object.Key;

            // Act
            editor.DrawExtra(entryMock.Object, state);

            // Assert
            Assert.Equal(entryMock.Object.Key, state.ExpandedEnumKey);
            Assert.Equal(VisibleEnum.Visible, entryMock.Object.Value);
            unityGuiMock.VerifyGet(x => x.BoxStyle, Times.Once);
            unityGuiMock.Verify(x => x.BeginHorizontal(), Times.Once);
            unityGuiMock.Verify(x => x.Space(12.5f), Times.Once);
            unityGuiMock.Verify(x => x.BeginVertical(boxStyle, It.Is<GUILayoutOption[]>(options => options.Length == 0)), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.SelectionGrid(0, It.Is<string[]>(names => names.Length == 2 && names[0] == "Visible Option" && names[1] == "Another Visible Option"), 1, It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))), Times.Once);
            unityGuiMock.Verify(x => x.EndVertical(), Times.Once);
            unityGuiMock.Verify(x => x.Space(8.5f), Times.Once);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
        }

        [Fact]
        public void DrawExtra_WhenSelectionGridThrows_ClosesNestedLayouts()
        {
            GUIStyle boxStyle = null;
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginHorizontal());
            unityGui.Setup(x => x.Space(12.5f));
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGui
                .Setup(x => x.SelectionGrid(0, It.IsAny<string[]>(), 1, It.IsAny<GUILayoutOption[]>()))
                .Throws(new InvalidOperationException("selection failed"));
            unityGui.Setup(x => x.EndVertical());
            unityGui.Setup(x => x.EndHorizontal());
            var editor = new EnumEditor(unityGui.Object);
            var entry = CreateEntry("VisibleEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);
            var context = new EditableGuiContext
            {
                SelectedGroupKey = "General",
                TrailingActionWidth = 8.5f
            };
            context.SetEntryLabelWidth("General", 12.5f);
            context.ExpandedEnumKey = entry.Object.Key;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                editor.DrawExtra(entry.Object, context));

            Assert.Equal("selection failed", exception.Message);
            Assert.Equal(entry.Object.Key, context.ExpandedEnumKey);
            Assert.Equal(VisibleEnum.Visible, entry.Object.Value);
            unityGui.Verify(x => x.EndVertical(), Times.Once);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
        }

        [Fact]
        public void DrawExtra_WhenCurrentValueIsHidden_FallsBackToFirstVisibleOptionAndUpdatesEntry()
        {
            // Arrange
            GUIStyle boxStyle = null;
            GUILayoutOption expandWidth = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGuiMock.Setup(x => x.BeginHorizontal());
            unityGuiMock.Setup(x => x.Space(0f));
            unityGuiMock.Setup(x => x.BeginVertical(boxStyle, It.Is<GUILayoutOption[]>(options => options.Length == 0)));
            unityGuiMock.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
            unityGuiMock
                .Setup(x => x.SelectionGrid(0, It.Is<string[]>(names => names.Length == 2 && names[0] == "Visible Option" && names[1] == "Another Visible Option"), 1, It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))))
                .Returns(1);
            unityGuiMock.Setup(x => x.EndVertical());
            unityGuiMock.Setup(x => x.Space(0f));
            unityGuiMock.Setup(x => x.EndHorizontal());
            var editor = new EnumEditor(unityGuiMock.Object);
            var entryMock = CreateEntry("HiddenEnumEntry", typeof(VisibleEnum), VisibleEnum.Hidden);
            var state = new EditableGuiContext
            {
                TrailingActionWidth = 0f,
            };
            state.ExpandedEnumKey = entryMock.Object.Key;

            // Act
            editor.DrawExtra(entryMock.Object, state);

            // Assert
            Assert.Equal(string.Empty, state.ExpandedEnumKey);
            Assert.Equal(VisibleEnum.AnotherVisible, entryMock.Object.Value);
            unityGuiMock.VerifyGet(x => x.BoxStyle, Times.Once);
            unityGuiMock.Verify(x => x.BeginHorizontal(), Times.Once);
            unityGuiMock.Verify(x => x.Space(0f), Times.Exactly(2));
            unityGuiMock.Verify(x => x.BeginVertical(boxStyle, It.Is<GUILayoutOption[]>(options => options.Length == 0)), Times.Once);
            unityGuiMock.Verify(x => x.ExpandWidth(true), Times.Once);
            unityGuiMock.Verify(x => x.SelectionGrid(0, It.Is<string[]>(names => names.Length == 2 && names[0] == "Visible Option" && names[1] == "Another Visible Option"), 1, It.Is<GUILayoutOption[]>(options => options.Length == 1 && ReferenceEquals(options[0], expandWidth))), Times.Once);
            unityGuiMock.Verify(x => x.EndVertical(), Times.Once);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
        }

        [Fact]
        public void GetEnumInfo_WhenEntryTypeIsNotEnum_ReturnsNullValues()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = CreateEntry("NotEnumEntry", typeof(string), "text");

            // Act
            var result = editor.GetEnumInfo(entryMock.Object);

            // Assert
            Assert.Null(result.values);
            Assert.Null(result.mapIndex);
            Assert.Null(result.names);
        }

        [Fact]
        public void GetEnumInfo_WhenEntryTypeIsEnum_ReturnsOnlyDisplayableValuesAndCachesResult()
        {
            // Arrange
            var editor = CreateEditor();
            var entryMock = CreateEntry("VisibleEnumEntry", typeof(VisibleEnum), VisibleEnum.Visible);

            // Act
            var firstResult = editor.GetEnumInfo(entryMock.Object);
            var secondResult = editor.GetEnumInfo(entryMock.Object);

            // Assert
            Assert.Equal(3, firstResult.values.Length);
            Assert.Equal(new List<int> { 0, 2 }, firstResult.mapIndex);
            Assert.Equal(new[] { "Visible Option", "Another Visible Option" }, firstResult.names);
            Assert.Same(firstResult.values, secondResult.values);
            Assert.Same(firstResult.mapIndex, secondResult.mapIndex);
            Assert.Same(firstResult.names, secondResult.names);
        }

        private static EnumEditor CreateEditor()
        {
            return new EnumEditor(new Mock<IUnityGuiProvider>(MockBehavior.Strict).Object);
        }

        private static Mock<IEntryBinding> CreateEntry(string key, Type valueType, object value)
        {
            var entryMock = new Mock<IEntryBinding>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entryMock.SetupGet(x => x.Key).Returns(key);
            entryMock.SetupGet(x => x.ValueType).Returns(valueType);
            entryMock.SetupProperty(x => x.Value, value);
            return entryMock;
        }

        private enum VisibleEnum
        {
            [Description("Visible Option")]
            Visible,

            [Description("Hidden Option")]
            [DisplayEnum(false)]
            Hidden,

            [Description("Another Visible Option")]
            AnotherVisible,
        }
    }
}
