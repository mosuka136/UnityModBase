using System.Reflection;
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

namespace UnityModBase.Test.HGuiSpace.Editor
{
    public class GroupEditorTests
    {
        [Fact]
        public void SearchQuery_WhenSetToNull_StoresEmptyString()
        {
            var editor = CreateEditor(out _, out _, out _, out _);

            editor.SearchQuery = null;

            Assert.Equal(string.Empty, editor.SearchQuery);
        }

        [Fact]
        public void Draw_WhenQueryUnchanged_PreservesContentScrollPositionAcrossPasses()
        {
            var editor = CreateEditor(out _, out var unityGui, out _, out _);
            var entry = CreateEntry("Volume", "音量", "Volume");
            var root = new GroupBinding("Root", null, null, new[] { entry.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            SetupSearchBar(unityGui, string.Empty);
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);
            // 滚动视图回传非零位置模拟用户已滚动；无一级分组时布局里只有内容区一个滚动视图。
            // Vector2 不能用 It.IsAny 匹配（会触发加载 UnityEngine.SharedInternalsModule），按具体位置分别设置。
            unityGui.Setup(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()))
                .Returns(new Vector2(3f, 4f));
            unityGui.Setup(x => x.BeginScrollView(new Vector2(3f, 4f), It.IsAny<GUILayoutOption[]>()))
                .Returns(new Vector2(3f, 4f));

            // 搜索栏每趟都会把文本框当前值整体赋回关键字，同值赋值不得重置滚动位置。
            editor.Draw(context);
            editor.Draw(context);

            unityGui.Verify(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(
                x => x.BeginScrollView(new Vector2(3f, 4f), It.IsAny<GUILayoutOption[]>()), Times.Once);
        }

        [Fact]
        public void Draw_WhenQueryChanges_ResetsContentScrollPosition()
        {
            var editor = CreateEditor(out _, out var unityGui, out _, out _);
            var entry = CreateEntry("Volume", "音量", "Volume");
            var root = new GroupBinding("Root", null, null, new[] { entry.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            SetupSearchBar(unityGui, string.Empty);
            SetupSearchBar(unityGui, "volume");
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);
            // 滚动视图回传非零位置模拟用户已滚动；关键字重置后内容区再次从零位置开始。
            unityGui.Setup(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()))
                .Returns(new Vector2(3f, 4f));

            editor.Draw(context);
            editor.SearchQuery = "volume";
            editor.Draw(context);

            // 首趟与关键字变化后的第二趟都从零位置开始，已滚动位置不再传入。
            unityGui.Verify(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()), Times.Exactly(2));
            unityGui.Verify(
                x => x.BeginScrollView(new Vector2(3f, 4f), It.IsAny<GUILayoutOption[]>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenContextIsInvalid_DoesNotDrawSearchBar()
        {
            var editor = CreateEditor(out _, out var unityGui, out _, out _);

            editor.Draw(new InvalidContext());

            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenRootHasNoGroups_DrawsSearchBarThenContentWithoutSidebar()
        {
            var editor = CreateEditor(out _, out var unityGui, out _, out _);
            var root = new GroupBinding("Root", null, null);
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            SetupSearchBar(unityGui, string.Empty);
            SetupContentChrome(unityGui, includeOuterHorizontal: false);

            editor.Draw(context);

            unityGui.Verify(x => x.Label(TranslatorResource.Search.ToString(), It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.TextField(string.Empty, It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.Label(TranslatorResource.SearchNoResults.ToString(), It.IsAny<GUILayoutOption[]>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenQueryMatchesEntry_HidesNonMatchingSibling()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var matching = CreateEntry("Volume", "音量", "Volume");
            var other = CreateEntry("Mute", "静音", "Mute");
            var root = new GroupBinding("Root", null, null, new[] { matching.Object, other.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "volume";
            SetupSearchBar(unityGui, "volume");
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);

            editor.Draw(context);

            valueEditor.Verify(x => x.DrawValue(matching.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawExtra(matching.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(other.Object, It.IsAny<EditableGuiContext>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenTextFieldReturnsQuery_FiltersInSameDraw()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var matching = CreateEntry("Volume", "音量", "Volume");
            var other = CreateEntry("Mute", "静音", "Mute");
            var root = new GroupBinding("Root", null, null, new[] { matching.Object, other.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            SetupSearchBar(unityGui, string.Empty, returnedQuery: "volume");
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);

            editor.Draw(context);

            Assert.Equal("volume", editor.SearchQuery);
            valueEditor.Verify(x => x.DrawValue(matching.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(other.Object, It.IsAny<EditableGuiContext>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenQueryMatchesGroup_DrawsAllChildEntries()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out var style);
            var volume = CreateEntry("Volume", "音量", "Volume");
            var mute = CreateEntry("Mute", "静音", "Mute");
            var audio = new GroupBinding(
                "Audio",
                new Translator("音频", "Audio"),
                null,
                new[] { volume.Object, mute.Object });
            var graphics = new GroupBinding(
                "Graphics",
                new Translator("画面", "Graphics"),
                null,
                new[] { CreateEntry("VSync", "垂直同步", "VSync").Object });
            var root = new GroupBinding("Root", null, null, new[] { audio, graphics });
            var context = CreateContext(root, selectedGroupKey: audio.Key);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "audio";
            SetupSearchBar(unityGui, "audio");
            SetupSidebarStyles(style, out var sidebarStyle, out var selectedStyle, out var titleStyle);
            var content = SetupEntryDrawing(unityGui);
            SetupContentChrome(unityGui, includeOuterHorizontal: true, titleStyle, content);
            SetupSidebarButtons(unityGui, selected: audio, selectedStyle: selectedStyle, others: Array.Empty<(GroupBinding, GUIStyle)>());

            editor.Draw(context);

            valueEditor.Verify(x => x.DrawValue(volume.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(mute.Object, context), Times.Once);
            unityGui.Verify(
                x => x.Button(graphics.Name.ToString(), sidebarStyle, It.IsAny<GUILayoutOption[]>()),
                Times.Never);
            unityGui.Verify(
                x => x.Button(graphics.Name.ToString(), selectedStyle, It.IsAny<GUILayoutOption[]>()),
                Times.Never);
        }

        [Fact]
        public void Draw_WhenSelectedGroupIsFilteredOut_SelectsFirstVisibleGroup()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out var style);
            var apple = CreateEntry("Apple", "苹果", "Apple");
            var banana = CreateEntry("Banana", "香蕉", "Banana");
            var first = new GroupBinding("First", new Translator("第一组", "First"), null, new[] { apple.Object });
            var second = new GroupBinding("Second", new Translator("第二组", "Second"), null, new[] { banana.Object });
            var root = new GroupBinding("Root", null, null, new[] { first, second });
            var context = CreateContext(root, selectedGroupKey: second.Key);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "apple";
            SetupSearchBar(unityGui, "apple");
            SetupSidebarStyles(style, out _, out var selectedStyle, out var titleStyle);
            var content = SetupEntryDrawing(unityGui);
            SetupContentChrome(unityGui, includeOuterHorizontal: true, titleStyle, content);
            SetupSidebarButtons(unityGui, selected: first, selectedStyle: selectedStyle, others: Array.Empty<(GroupBinding, GUIStyle)>());

            editor.Draw(context);

            Assert.Equal(first.Key, context.SelectedGroupKey);
            valueEditor.Verify(x => x.DrawValue(apple.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(banana.Object, It.IsAny<EditableGuiContext>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenQueryMatchesNothing_ShowsNoResultsAndSkipsSidebar()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var group = new GroupBinding(
                "Audio",
                new Translator("音频", "Audio"),
                null,
                new[] { CreateEntry("Volume", "音量", "Volume").Object });
            var root = new GroupBinding("Root", null, null, new[] { group });
            var context = CreateContext(root, selectedGroupKey: group.Key);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "zzz";
            SetupSearchBar(unityGui, "zzz");
            var boxStyle = CreateUninitializedGuiStyle();
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.EndVertical());
            unityGui.Setup(x => x.Label(TranslatorResource.SearchNoResults.ToString(), It.IsAny<GUILayoutOption[]>()));

            editor.Draw(context);

            unityGui.Verify(x => x.Label(TranslatorResource.SearchNoResults.ToString(), It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()), Times.Never);
            valueEditor.Verify(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenRootHasNoGroupsAndNothingMatches_ShowsNoResultsWithoutContentView()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var entry = CreateEntry("Volume", "音量", "Volume");
            var root = new GroupBinding("Root", null, null, new[] { entry.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "zzz";
            SetupSearchBar(unityGui, "zzz");
            var boxStyle = CreateUninitializedGuiStyle();
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.EndVertical());
            unityGui.Setup(x => x.Label(TranslatorResource.SearchNoResults.ToString(), It.IsAny<GUILayoutOption[]>()));

            editor.Draw(context);

            // 没有一级分组的根也遵循过滤：整体无匹配时不进入内容滚动视图，只显示无匹配提示。
            unityGui.Verify(x => x.Label(TranslatorResource.SearchNoResults.ToString(), It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()), Times.Never);
            valueEditor.Verify(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Never);
        }

        [Fact]
        public void Draw_WhenClearButtonClicked_ClearsQueryAndShowsAllEntries()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var matching = CreateEntry("Volume", "音量", "Volume");
            var other = CreateEntry("Mute", "静音", "Mute");
            var root = new GroupBinding("Root", null, null, new[] { matching.Object, other.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "volume";
            SetupSearchBar(unityGui, "volume", clearClicked: true);
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);

            editor.Draw(context);

            Assert.Equal(string.Empty, editor.SearchQuery);
            valueEditor.Verify(x => x.DrawValue(matching.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(other.Object, context), Times.Once);
        }

        [Fact]
        public void Draw_WhenQueryIsWhitespace_DoesNotFilter()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var first = CreateEntry("Volume", "音量", "Volume");
            var second = CreateEntry("Mute", "静音", "Mute");
            var root = new GroupBinding("Root", null, null, new[] { first.Object, second.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "   ";
            SetupSearchBar(unityGui, "   ");
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);

            editor.Draw(context);

            valueEditor.Verify(x => x.DrawValue(first.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(second.Object, context), Times.Once);
            unityGui.Verify(
                x => x.Button(TranslatorResource.SearchClear.ToString(), It.IsAny<GUILayoutOption[]>()),
                Times.Never);
        }

        [Fact]
        public void Draw_WhenNestedEntryMatches_DrawsAncestorTitleAndHidesUnrelatedSibling()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out var style);
            var nestedEntry = CreateEntry("Volume", "音量", "Volume");
            var sibling = CreateEntry("Unrelated", "无关", "Unrelated");
            var nested = new GroupBinding("Audio", new Translator("音频", "Audio"), null, new[] { nestedEntry.Object });
            var parent = new GroupBinding(
                "Table",
                new Translator("表", "Table"),
                null,
                new INodeBinding[] { nested, sibling.Object });
            var root = new GroupBinding("Root", null, null, new[] { parent });
            var context = CreateContext(root, selectedGroupKey: parent.Key);
            SetCurrentRoot(editor, root);
            editor.SearchQuery = "volume";
            SetupSearchBar(unityGui, "volume");
            SetupSidebarStyles(style, out _, out var selectedStyle, out var titleStyle);
            var content = SetupEntryDrawing(unityGui);
            SetupContentChrome(unityGui, includeOuterHorizontal: true, titleStyle, content);
            SetupSidebarButtons(unityGui, selected: parent, selectedStyle: selectedStyle, others: Array.Empty<(GroupBinding, GUIStyle)>());

            editor.Draw(context);

            valueEditor.Verify(x => x.DrawValue(nestedEntry.Object, context), Times.Once);
            valueEditor.Verify(x => x.DrawValue(sibling.Object, It.IsAny<EditableGuiContext>()), Times.Never);
            unityGui.Verify(
                x => x.Label(content, titleStyle, It.IsAny<GUILayoutOption[]>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void Draw_WhenEntryRenderingThrows_ClosesSearchBarLayout()
        {
            var editor = CreateEditor(out _, out var unityGui, out var valueEditor, out _);
            var entry = CreateEntry("Volume", "音量", "Volume");
            entry.SetupGet(x => x.Name).Throws(new InvalidOperationException("name failed"));
            var root = new GroupBinding("Root", null, null, new[] { entry.Object });
            var context = CreateContext(root);
            SetCurrentRoot(editor, root);
            SetupSearchBar(unityGui, string.Empty);
            SetupContentChrome(unityGui, includeOuterHorizontal: false);
            SetupEntryDrawing(unityGui);

            var exception = Assert.Throws<InvalidOperationException>(() => editor.Draw(context));

            Assert.Equal("name failed", exception.Message);
            unityGui.Verify(x => x.EndHorizontal(), Times.Exactly(2));
            unityGui.Verify(x => x.EndVertical(), Times.Exactly(2));
            unityGui.Verify(x => x.EndScrollView(), Times.Once);
            valueEditor.Verify(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()), Times.Never);
        }

        private static GroupEditor CreateEditor(
            out Mock<IUnityProvider> unityService,
            out Mock<IUnityGuiProvider> unityGui,
            out Mock<IValueEditor> valueEditor,
            out Mock<IEntryStyleResource> style)
        {
            unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            style = new Mock<IEntryStyleResource>(MockBehavior.Strict);
            valueEditor = new Mock<IValueEditor>(MockBehavior.Strict);
            valueEditor.Setup(x => x.CanEdit(It.IsAny<IEntryBinding>())).Returns(true);
            valueEditor.Setup(x => x.DrawValue(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            valueEditor.Setup(x => x.DrawExtra(It.IsAny<IEntryBinding>(), It.IsAny<EditableGuiContext>()));
            var registry = new ValueEditorRegistry();
            registry.RegisterEditor(valueEditor.Object);
            return new GroupEditor(unityService.Object, unityGui.Object, style.Object, registry);
        }

        private static EditableGuiContext CreateContext(GroupBinding root, string selectedGroupKey = "")
        {
            return new EditableGuiContext
            {
                UserData = root,
                SelectedGroupKey = selectedGroupKey,
                GroupButtonWidth = 80f,
                TrailingActionWidth = 0f,
                IsEntryLabelWidthDirty = false,
                IsGroupButtonWidthDirty = false,
                IsTrailingActionWidthDirty = false
            };
        }

        private static Mock<IEntryBinding> CreateEntry(string key, string chinese, string english)
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.Key).Returns(key);
            entry.SetupGet(x => x.Name).Returns(new Translator(chinese, english));
            entry.SetupGet(x => x.Description).Returns(new Translator());
            entry.SetupGet(x => x.ValueType).Returns(typeof(string));
            entry.SetupGet(x => x.Value).Returns("value");
            entry.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            return entry;
        }

        private static void SetupSearchBar(
            Mock<IUnityGuiProvider> unityGui,
            string displayedQuery,
            string returnedQuery = null,
            bool clearClicked = false)
        {
            var next = returnedQuery ?? displayedQuery;
            unityGui.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.EndHorizontal());
            unityGui.Setup(x => x.Label(TranslatorResource.Search.ToString(), It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.ExpandWidth(false)).Returns((GUILayoutOption)null);
            unityGui.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGui.Setup(x => x.TextField(displayedQuery, It.IsAny<GUILayoutOption[]>())).Returns(next);
            unityGui.Setup(x => x.Space(4f));
            unityGui.Setup(x => x.Space(5f));
            if (NodeSearch.IsActive(next) && !clearClicked)
            {
                unityGui.Setup(x => x.Button(TranslatorResource.SearchClear.ToString(), It.IsAny<GUILayoutOption[]>()))
                    .Returns(false);
            }
            else if (NodeSearch.IsActive(displayedQuery) && clearClicked)
            {
                // 清除按钮根据 TextField 赋值后的关键字决定是否绘制；
                // 点击清除时当前显示值仍是非空白关键字。
                unityGui.Setup(x => x.Button(TranslatorResource.SearchClear.ToString(), It.IsAny<GUILayoutOption[]>()))
                    .Returns(true);
            }
        }

        private static void SetupContentChrome(
            Mock<IUnityGuiProvider> unityGui,
            bool includeOuterHorizontal,
            GUIStyle titleStyle = null,
            GUIContent content = null)
        {
            var boxStyle = CreateUninitializedGuiStyle();
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.BeginVertical());
            unityGui.Setup(x => x.EndVertical());
            unityGui.Setup(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()))
                .Returns(Vector2.zero);
            unityGui.Setup(x => x.EndScrollView());
            unityGui.Setup(x => x.Space(10f));
            if (!includeOuterHorizontal)
                return;

            unityGui.Setup(x => x.Width(It.IsAny<float>())).Returns((GUILayoutOption)null);
            unityGui.Setup(x => x.Label(content, titleStyle, It.IsAny<GUILayoutOption[]>()));
        }

        private static GUIContent SetupEntryDrawing(Mock<IUnityGuiProvider> unityGui)
        {
            var content = new GUIContent();
            unityGui.Setup(x => x.GetContent(It.IsAny<string>(), It.IsAny<string>())).Returns(content);
            unityGui.Setup(x => x.Width(It.IsAny<float>())).Returns((GUILayoutOption)null);
            unityGui.Setup(x => x.Label(content, It.IsAny<GUILayoutOption[]>()));
            return content;
        }

        private static void SetupSidebarStyles(
            Mock<IEntryStyleResource> style,
            out GUIStyle sidebar,
            out GUIStyle selected,
            out GUIStyle title)
        {
            sidebar = CreateUninitializedGuiStyle();
            selected = CreateUninitializedGuiStyle();
            title = CreateUninitializedGuiStyle();
            style.SetupGet(x => x.SidebarEntryStyle).Returns(sidebar);
            style.SetupGet(x => x.SidebarSelectedEntryStyle).Returns(selected);
            style.SetupGet(x => x.TableTitleStyle).Returns(title);
        }

        private static void SetupSidebarButtons(
            Mock<IUnityGuiProvider> unityGui,
            GroupBinding selected,
            GUIStyle selectedStyle,
            IReadOnlyList<(GroupBinding Group, GUIStyle Style)> others)
        {
            unityGui.Setup(x => x.Button(
                    selected.Name.ToString(),
                    selectedStyle,
                    It.IsAny<GUILayoutOption[]>()))
                .Returns(false);
            foreach (var other in others)
            {
                unityGui.Setup(x => x.Button(
                        other.Group.Name.ToString(),
                        other.Style,
                        It.IsAny<GUILayoutOption[]>()))
                    .Returns(false);
            }
        }

        private static void SetCurrentRoot(GroupEditor editor, GroupBinding root)
        {
            var field = typeof(GroupEditor).GetField("_currentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(editor, root);
        }

        private static GUIStyle CreateUninitializedGuiStyle()
        {
            var style = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(style);
            return style;
        }

        private sealed class InvalidContext : EditableGuiContext
        {
            internal InvalidContext()
                : base(false)
            {
            }
        }
    }
}
