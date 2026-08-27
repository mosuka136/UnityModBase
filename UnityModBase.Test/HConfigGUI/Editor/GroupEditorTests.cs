using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HConfigSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using Moq;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class GroupEditorTests
    {
        /// <summary>
        /// 构造函数会向全进程共享的静态注册表注册内置编辑器；每个测试开始前先释放并清空，
        /// 保证注册顺序和 Dispose 断言只反映本测试构造的编辑器。
        /// </summary>
        public GroupEditorTests()
        {
            ValueEditorRegistry.Dispose();
        }

        [Fact]
        public void Constructor_ValidDependencies_InitializesDependenciesAndEditorsWithoutContext()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleResource = new StyleResource(null);

            // Act
            var editor = new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                styleResource);

            // Assert
            Assert.Same(unityProvider.Object, editor.UnityService);
            Assert.Same(unityGui.Object, editor.UnityGui);
            Assert.Same(styleResource, editor.StyleProvider);
            Assert.NotNull(editor.EntryEditor);
            Assert.Same(unityGui.Object, editor.EntryEditor.UnityGui);
            Assert.IsType<BooleanEditor>(ValueEditorRegistry.GetEditor(CreateEntryBindingMock(typeof(bool)).Object));
            Assert.IsType<StringEditor>(ValueEditorRegistry.GetEditor(CreateEntryBindingMock(typeof(string)).Object));
            Assert.IsType<SliderEditor>(ValueEditorRegistry.GetEditor(CreateEntryBindingMock(typeof(int), metadata: new UiSliderMetadata(0f, 10f, 1f)).Object));
            Assert.IsType<NumberEditor>(ValueEditorRegistry.GetEditor(CreateEntryBindingMock(typeof(float)).Object));
            Assert.IsType<EnumEditor>(ValueEditorRegistry.GetEditor(CreateEntryBindingMock(typeof(TestEnum)).Object));
            Assert.Same(editor.HotkeyEditor, ValueEditorRegistry.GetEditor(CreateEntryBindingMock(typeof(Hotkey)).Object));
            Assert.IsType<DualValueEditor>(ValueEditorRegistry.GetEditor(
                CreateEntryBindingMock(typeof(ConfigEntryValue<int, string>)).Object));
        }

        [Fact]
        public void Constructor_WhenDependencyIsNull_ThrowsMatchingArgumentNullException()
        {
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleResource = new StyleResource(null);

            Assert.Equal("unityService", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor(null, unityGui.Object, styleResource)).ParamName);
            Assert.Equal("unityGui", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor(unityProvider.Object, null, styleResource)).ParamName);
            Assert.Equal("styleProvider", Assert.Throws<ArgumentNullException>(() =>
                new GroupEditor(unityProvider.Object, unityGui.Object, null)).ParamName);
        }

        [Fact]
        public void ContextMethods_WhenArgumentIsNull_ThrowMatchingArgumentNullException()
        {
            var editor = CreateEditor(out _, out _);
            var root = new GroupBinding("Root", null, null);

            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => editor.Draw(null)).ParamName);
            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => editor.Update(null, 0.1f)).ParamName);
            Assert.Equal("root", Assert.Throws<ArgumentNullException>(() =>
                editor.UpdateLayoutIfNeeded(null, new GuiContext())).ParamName);
            Assert.Equal("context", Assert.Throws<ArgumentNullException>(() =>
                editor.UpdateLayoutIfNeeded(root, null)).ParamName);
        }

        [Fact]
        public void Draw_WhenContextIsInvalid_DoesNotInvokeGui()
        {
            var editor = CreateEditor(out _, out var unityGui);

            editor.Draw(GuiContext.InvalidGuiContext);

            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenRootHasNoGroups_DrawsContentWithoutSidebar()
        {
            var editor = CreateEditor(out _, out var unityGui);
            var root = new GroupBinding("Root", null, null);
            var context = new GuiContext
            {
                UserData = root,
                IsEntryLabelWidthDirty = false,
                IsGroupButtonWidthDirty = false,
                IsResetButtonWidthDirty = false
            };
            SetCurrentRoot(editor, root);
            var boxStyle = CreateUninitializedGuiStyle();
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui
                .Setup(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()))
                .Returns(new Vector2(3f, 4f));
            unityGui.Setup(x => x.BeginVertical());
            unityGui.Setup(x => x.Space(10f));
            unityGui.Setup(x => x.EndVertical());
            unityGui.Setup(x => x.EndScrollView());

            editor.Draw(context);

            unityGui.VerifyGet(x => x.BoxStyle, Times.Once);
            unityGui.Verify(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGui.Verify(x => x.BeginVertical(), Times.Once);
            unityGui.Verify(x => x.Space(10f), Times.Once);
            unityGui.Verify(x => x.EndScrollView(), Times.Once);
            unityGui.Verify(x => x.EndVertical(), Times.Exactly(2));
            unityGui.Verify(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()), Times.Never);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenEntryRenderingThrows_ClosesEveryOpenedLayout()
        {
            var editor = CreateEditor(out _, out var unityGui);
            var entry = CreateEntryBindingMock(typeof(string), value: "value");
            entry.SetupGet(x => x.Name).Throws(new InvalidOperationException("name failed"));
            var root = new GroupBinding("Root", null, null, new[] { entry.Object });
            var context = new GuiContext
            {
                UserData = root,
                IsEntryLabelWidthDirty = false,
                IsGroupButtonWidthDirty = false,
                IsResetButtonWidthDirty = false
            };
            SetCurrentRoot(editor, root);
            var boxStyle = CreateUninitializedGuiStyle();
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui
                .Setup(x => x.BeginScrollView(Vector2.zero, It.IsAny<GUILayoutOption[]>()))
                .Returns(Vector2.zero);
            unityGui.Setup(x => x.BeginVertical());
            unityGui.Setup(x => x.BeginHorizontal(It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.EndHorizontal());
            unityGui.Setup(x => x.EndVertical());
            unityGui.Setup(x => x.EndScrollView());

            var exception = Assert.Throws<InvalidOperationException>(() => editor.Draw(context));

            Assert.Equal("name failed", exception.Message);
            unityGui.Verify(x => x.EndHorizontal(), Times.Once);
            unityGui.Verify(x => x.EndVertical(), Times.Exactly(2));
            unityGui.Verify(x => x.EndScrollView(), Times.Once);
        }

        [Fact]
        public void Update_WhenDifferentContextIsPassed_FlushesOnlyThatContextsPendingValue()
        {
            // Arrange
            var unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var firstContext = new GuiContext();
            var secondContext = new GuiContext();
            var firstEntry = CreateEntryBindingMock(typeof(string), value: "first-old", key: "FirstEntry");
            var secondEntry = CreateEntryBindingMock(typeof(string), value: "second-old", key: "SecondEntry");
            firstEntry.SetupSet(x => x.Value = "first-new");
            secondEntry.SetupSet(x => x.Value = "second-new");
            var editor = new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                new StyleResource(null));
            firstContext.ChangeSink.SetValue(firstEntry.Object, "first-new", delay: 0.5f);
            secondContext.ChangeSink.SetValue(secondEntry.Object, "second-new", delay: 0.5f);

            // Act
            editor.Update(secondContext, 0.5f);

            // Assert
            firstEntry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            secondEntry.VerifySet(x => x.Value = "second-new", Times.Once);
        }

        [Fact]
        public void Update_WhenContextIsInvalid_DoesNotFlushPendingValue()
        {
            var editor = CreateEditor(out _, out _);
            var entry = CreateEntryBindingMock(typeof(string), value: "old", key: "InvalidContextEntry");
            entry.SetupSet(x => x.Value = "new");
            var sink = GuiContext.InvalidGuiContext.ChangeSink;
            sink.SetValue(entry.Object, "new", delay: 1f);

            try
            {
                editor.Update(GuiContext.InvalidGuiContext, 1f);

                entry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            }
            finally
            {
                sink.FlushValue(float.MaxValue);
            }

            entry.VerifySet(x => x.Value = "new", Times.Once);
        }

        [Fact]
        public void UpdateLayoutIfNeeded_WhenFlagsAreClean_PreservesLayoutWithoutGuiCalls()
        {
            var editor = CreateEditor(out _, out var unityGui);
            var context = new GuiContext
            {
                GroupButtonWidth = 121f,
                ResetButtonWidth = 34f,
                IsEntryLabelWidthDirty = false,
                IsGroupButtonWidthDirty = false,
                IsResetButtonWidthDirty = false
            };
            context.SetEntryLabelWidth("Group", 56f);
            var root = new GroupBinding("Root", null, null);

            editor.UpdateLayoutIfNeeded(root, context);

            Assert.Equal(56f, context.GetEntryLabelWidth("Group"));
            Assert.Equal(121f, context.GroupButtonWidth);
            Assert.Equal(34f, context.ResetButtonWidth);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void UpdateLayoutIfNeeded_WhenEntryLabelWidthIsDirty_StoresPaddingForEmptyChildGroup()
        {
            var editor = CreateEditor(out _, out var unityGui);
            var child = new GroupBinding("EmptyGroup", null, null);
            var root = new GroupBinding("Root", null, null, new[] { child });
            var context = new GuiContext
            {
                IsEntryLabelWidthDirty = true,
                IsGroupButtonWidthDirty = false,
                IsResetButtonWidthDirty = false
            };

            editor.UpdateLayoutIfNeeded(root, context);

            Assert.Equal(10f, context.GetEntryLabelWidth(child.Key));
            Assert.False(context.IsEntryLabelWidthDirty);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void UpdateLayoutIfNeeded_WhenGroupWidthIsDirtyAndRootIsEmpty_StoresPadding()
        {
            var editor = CreateEditor(out _, out var unityGui);
            var context = new GuiContext
            {
                GroupButtonWidth = -1f,
                IsEntryLabelWidthDirty = false,
                IsGroupButtonWidthDirty = true,
                IsResetButtonWidthDirty = false
            };

            editor.UpdateLayoutIfNeeded(new GroupBinding("Root", null, null), context);

            Assert.Equal(60f, context.GroupButtonWidth);
            Assert.False(context.IsGroupButtonWidthDirty);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Dispose_WhenHotkeySessionIsRecording_ClearsSessionRestoresValidityAndSharedRegistry()
        {
            var editor = CreateEditor(out _, out _);
            var originalValue = new Hotkey();
            var entry = CreateEntryBindingMock(typeof(Hotkey), originalValue, key: "Hotkey");
            editor.HotkeyEditor.Session.BeginEdit(entry.Object);
            editor.HotkeyEditor.Session.BeginRecord(new HotkeyChord(UnityProvider.Instance));

            try
            {
                Assert.False(Hotkey.GlobalValid);
                Assert.False(originalValue.Valid);

                editor.Dispose();

                Assert.True(Hotkey.GlobalValid);
                Assert.True(originalValue.Valid);
                Assert.Null(editor.HotkeyEditor.Session.Entry);
                Assert.Equal(HotkeyEditState.Idle, editor.HotkeyEditor.Session.State);
                Assert.Null(editor.HotkeyEditor.Session.WorkingValue);
                Assert.Null(editor.HotkeyEditor.Session.WorkingChord);
                Assert.Same(UnsupportedEditor.Default, ValueEditorRegistry.GetEditor(entry.Object));
            }
            finally
            {
                Hotkey.GlobalValid = true;
                originalValue.Valid = true;
            }
        }

        private static Mock<IEntryBinding> CreateEntryBindingMock(System.Type valueType, object value = null, IUiMetadata metadata = null, string key = "Entry")
        {
            var mock = new Mock<IEntryBinding>(MockBehavior.Strict);
            mock.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            mock.SetupGet(x => x.Key).Returns(key);
            mock.SetupGet(x => x.ValueType).Returns(valueType);
            mock.SetupGet(x => x.Value).Returns(value);
            mock.SetupGet(x => x.Metadata).Returns(metadata);
            return mock;
        }

        private static GroupEditor CreateEditor(
            out Mock<IUnityProvider> unityProvider,
            out Mock<IUnityGuiProvider> unityGui)
        {
            unityProvider = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            return new GroupEditor(
                unityProvider.Object,
                unityGui.Object,
                new StyleResource(null));
        }

        private static GUIStyle CreateUninitializedGuiStyle()
        {
            var style = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(style);
            return style;
        }

        private static void SetCurrentRoot(GroupEditor editor, GroupBinding root)
        {
            var field = typeof(GroupEditor).GetField("_currentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(editor, root);
        }

        private enum TestEnum
        {
            One,
            Two,
        }
    }
}
