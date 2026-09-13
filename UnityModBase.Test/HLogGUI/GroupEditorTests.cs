using System.Reflection;
using System.Runtime.CompilerServices;
using Moq;
using UnityEngine;
using UnityModBase.HLogGUI;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HLogGUI
{
    public class GroupEditorTests
    {
        [Fact]
        public void Constructor_WithDependencies_StoresDependenciesAndCreatesEntryEditor()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGui.Object);

            var editor = new GroupEditor(unityGui.Object, unityService.Object, styleProvider);

            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Same(unityService.Object, editor.UnityService);
            Assert.Same(styleProvider, editor.StyleProvider);
            Assert.NotNull(editor.EntryEditor);
            Assert.Equal(LogLevel.Info, editor.Level);
        }

        [Fact]
        public void Constructor_WhenDependencyIsNull_ThrowsMatchingArgumentNullException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var styleProvider = new StyleResource(unityGui.Object);

            Assert.Equal("unityGui", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor(null, unityService.Object, styleProvider)).ParamName);
            Assert.Equal("unityService", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor(unityGui.Object, null, styleProvider)).ParamName);
            Assert.Equal("styleProvider", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor(unityGui.Object, unityService.Object, null)).ParamName);
        }

        [Fact]
        public void ColumnEditor_ConstructorWithDependencies_StoresInitialState()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var style = CreateUninitializedGuiStyle();
            var header = new Translator("编号", "Id");

            var editor = new GroupEditor.ColumnEditor(
                EntryContentType.Id,
                header,
                style,
                unityGui.Object,
                unityService.Object);

            Assert.Equal(EntryContentType.Id, editor.ContentType);
            Assert.Same(header, editor.Header);
            Assert.Same(style, editor.Style);
            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Same(unityService.Object, editor.UnityService);
            Assert.True(editor.IsVisible);
            Assert.Equal(0f, editor.Width);
        }

        [Fact]
        public void ColumnEditor_ConstructorWhenDependencyIsNull_ThrowsMatchingArgumentNullException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var style = CreateUninitializedGuiStyle();
            var header = new Translator("编号", "Id");

            Assert.Equal("unityGui", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor.ColumnEditor(EntryContentType.Id, header, style, null, unityService.Object)).ParamName);
            Assert.Equal("unityService", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor.ColumnEditor(EntryContentType.Id, header, style, unityGui.Object, null)).ParamName);
            Assert.Equal("style", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor.ColumnEditor(EntryContentType.Id, header, null, unityGui.Object, unityService.Object)).ParamName);
            Assert.Equal("header", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor.ColumnEditor(EntryContentType.Id, null, style, unityGui.Object, unityService.Object)).ParamName);
        }

        [Fact]
        public void ColumnEditor_GetTextWhenEntryIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                GroupEditor.ColumnEditor.GetText(null, EntryContentType.Message));

            Assert.Equal("entry", exception.ParamName);
        }

        [Fact]
        public void ColumnEditor_DrawCellWhenEntryOrEditorIsNull_ThrowsArgumentNullException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var style = CreateUninitializedGuiStyle();
            var column = new GroupEditor.ColumnEditor(
                EntryContentType.Message,
                new Translator("消息", "Message"),
                style,
                unityGui.Object,
                unityService.Object);
            var entry = CreateEntry(LogLevel.Info);
            var entryEditor = new EntryEditor(unityGui.Object, unityService.Object);

            Assert.Equal("entry", Assert.Throws<ArgumentNullException>(() =>
                column.DrawCell(null, entryEditor, LogLevel.Info)).ParamName);
            Assert.Equal("editor", Assert.Throws<ArgumentNullException>(() =>
                column.DrawCell(entry, null, LogLevel.Info)).ParamName);
        }

        [Fact]
        public void ColumnEditor_DrawCellWhenHiddenOrBelowLevel_DoesNotInvokeGui()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var column = new GroupEditor.ColumnEditor(
                EntryContentType.Message,
                new Translator("消息", "Message"),
                CreateUninitializedGuiStyle(),
                unityGui.Object,
                unityService.Object);
            var entry = CreateEntry(LogLevel.Info);
            var entryEditor = new EntryEditor(unityGui.Object, unityService.Object);

            column.IsVisible = false;
            column.DrawCell(entry, entryEditor, LogLevel.Debug);
            column.IsVisible = true;
            column.DrawCell(entry, entryEditor, LogLevel.Warning);

            unityGui.VerifyNoOtherCalls();
            unityService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ContextMethods_WhenContextIsNull_ThrowArgumentNullException()
        {
            var editor = CreateGroupEditor(out _, out _);

            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => editor.Draw(null)).ParamName);
            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => editor.UpdateLayoutIfNeeded(null)).ParamName);
            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => editor.DrawColumnVisibilityMenu(null)).ParamName);
            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => editor.DrawColumnLogLevelMenu(null)).ParamName);
        }

        [Fact]
        public void UpdateLayoutIfNeeded_WhenColumnWidthNotDirty_SkipsMeasurementWithoutGuiCalls()
        {
            var editor = CreateGroupEditor(out var unityGui, out var unityService);
            SetColumnEditors(editor, new Dictionary<EntryContentType, GroupEditor.ColumnEditor>());
            var context = new GuiContext { UserData = new GroupBinding(), IsColumnWidthDirty = false };

            editor.UpdateLayoutIfNeeded(context);

            // 列宽未标脏时前置调用是纯状态操作，不产生任何 GUI 交互，可安全放在 GUI.Window 之前每帧执行。
            unityGui.VerifyNoOtherCalls();
            unityService.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawColumnVisibilityMenu_WhenToggleThrows_ClosesNestedLayouts()
        {
            var editor = CreateGroupEditor(out var unityGui, out var unityService);
            var boxStyle = CreateUninitializedGuiStyle();
            var toggleStyle = CreateUninitializedGuiStyle();
            var columnStyle = CreateUninitializedGuiStyle();
            var column = new GroupEditor.ColumnEditor(
                EntryContentType.Message,
                new Translator("消息", "Message"),
                columnStyle,
                unityGui.Object,
                unityService.Object);
            SetColumnEditors(editor, new Dictionary<EntryContentType, GroupEditor.ColumnEditor>
            {
                [EntryContentType.Message] = column
            });
            SetCachedStyle(editor.StyleProvider, "_columnVisibilityToggleStyle", toggleStyle);
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.Space(4f));
            unityGui.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGui
                .Setup(x => x.Toggle(true, column.Header, toggleStyle, It.IsAny<GUILayoutOption[]>()))
                .Throws(new InvalidOperationException("toggle failed"));
            unityGui.Setup(x => x.EndHorizontal());
            unityGui.Setup(x => x.EndVertical());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                editor.DrawColumnVisibilityMenu(new GuiContext()));

            Assert.Equal("toggle failed", exception.Message);
            Assert.True(column.IsVisible);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
            unityGui.Verify(x => x.EndVertical(), Times.Once);
        }

        [Fact]
        public void DrawColumnLogLevelMenu_WhenToggleThrows_ClosesNestedLayouts()
        {
            var editor = CreateGroupEditor(out var unityGui, out _);
            var boxStyle = CreateUninitializedGuiStyle();
            var toggleStyle = CreateUninitializedGuiStyle();
            SetCachedStyle(editor.StyleProvider, "_columnLogLevelToggleStyle", toggleStyle);
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.Space(4f));
            unityGui.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGui
                .Setup(x => x.Toggle(
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    toggleStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Throws(new InvalidOperationException("level toggle failed"));
            unityGui.Setup(x => x.EndHorizontal());
            unityGui.Setup(x => x.EndVertical());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                editor.DrawColumnLogLevelMenu(new GuiContext()));

            Assert.Equal("level toggle failed", exception.Message);
            Assert.Equal(LogLevel.Info, editor.Level);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
            unityGui.Verify(x => x.EndVertical(), Times.Once);
        }

        [Fact]
        public void DrawMiscMenu_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            var editor = CreateGroupEditor(out _, out _);

            var exception = Assert.Throws<ArgumentNullException>(() => editor.DrawMiscMenu(null));

            Assert.Equal("entry", exception.ParamName);
        }

        [Fact]
        public void DrawMiscMenu_WhenEntryIsBelowLevel_DoesNotInvokeGui()
        {
            var editor = CreateGroupEditor(out var unityGui, out var unityService);
            editor.Level = LogLevel.Warning;

            editor.DrawMiscMenu(CreateEntry(LogLevel.Info));

            unityGui.VerifyNoOtherCalls();
            unityService.VerifyNoOtherCalls();
        }

        [Fact]
        public void DrawMiscMenu_WhenCopyButtonIsClicked_CopiesLogAndRaisesEvent()
        {
            var originalLanguage = Translator.DefaultLanguage;
            Translator.DefaultLanguage = LanguageType.English;
            try
            {
                var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
                var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
                var style = CreateUninitializedGuiStyle();
                var styleProvider = new StyleResource(unityGui.Object);
                SetCachedStyle(styleProvider, "_miscButtonStyle", style);
                GUILayoutOption expandWidth = null;
                unityGui.Setup(x => x.ExpandWidth(true)).Returns(expandWidth);
                unityGui
                    .Setup(x => x.Button(
                        TranslatorResource.CopyLog.ToString(),
                        style,
                        It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == expandWidth)))
                    .Returns(true);
                var editor = new GroupEditor(unityGui.Object, unityService.Object, styleProvider);
                var entry = CreateEntry(LogLevel.Error);
                unityService.Setup(x => x.ClipboardCopy(entry.ToString()));
                string received = null;
                editor.OnLogCopied += message => received = message;

                editor.DrawMiscMenu(entry);

                unityService.Verify(x => x.ClipboardCopy(entry.ToString()), Times.Once);
                unityGui.Verify(x => x.ExpandWidth(true), Times.Once);
                unityGui.Verify(x => x.Button(
                    TranslatorResource.CopyLog.ToString(),
                    style,
                    It.IsAny<GUILayoutOption[]>()), Times.Once);
                Assert.Equal($"Copied: Log{entry.Id}", received);
                unityGui.VerifyNoOtherCalls();
                unityService.VerifyNoOtherCalls();
            }
            finally
            {
                Translator.DefaultLanguage = originalLanguage;
            }
        }

        private static GroupEditor CreateGroupEditor(
            out Mock<IUnityGuiProvider> unityGui,
            out Mock<IUnityProvider> unityService)
        {
            unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            return new GroupEditor(
                unityGui.Object,
                unityService.Object,
                new StyleResource(unityGui.Object));
        }

        private static EntryBinding CreateEntry(LogLevel level)
        {
            return new EntryBinding(new LogEntry(
                7,
                new DateTime(2026, 7, 15, 12, 0, 0),
                3,
                10,
                "Scene",
                level,
                "message",
                "File.cs",
                12,
                "Member",
                null));
        }

        private static GUIStyle CreateUninitializedGuiStyle()
        {
            var style = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(style);
            return style;
        }

        private static void SetCachedStyle(StyleResource resource, string fieldName, GUIStyle style)
        {
            var field = typeof(StyleResource).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(resource, style);
        }

        private static void SetColumnEditors(
            GroupEditor editor,
            Dictionary<EntryContentType, GroupEditor.ColumnEditor> columnEditors)
        {
            var property = typeof(GroupEditor).GetProperty(
                nameof(GroupEditor.ColumnEditorList),
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            property.SetValue(editor, columnEditors);
        }
    }
}
