using Moq;
using UnityModBase.HControlGUI;
using UnityModBase.HControlGUI.Bindings;
using UnityModBase.HControlSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HGuiSpace.Editor.ValueEditor;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;
using ControlUserEditor = UnityModBase.HControlGUI.Editor.UserEditor;

namespace UnityModBase.Test.HControlGUI
{
    public class ControlGuiTests
    {
        private static readonly Translator Name = new Translator("名称", "Name");
        private static readonly Translator Description = new Translator("说明", "Description");

        [Fact]
        public void BindingTree_ProjectsControlTablesAndGuiWritePublishesEntryEvent()
        {
            using var user = new UserContext("user", Name);
            user.Service.Control.CreateTable("Player", Name, Description);
            var entry = user.Service.Control.Bind(
                "Player", "Invincible", () => false, ControlUpdatePolicy.Never, Name, Description);
            var observed = false;
            entry.OnValueChanged += (_, value) => observed = value;

            var root = GroupBindingFactory.CreateRoot(user);
            var group = Assert.IsType<GroupBinding>(Assert.Single(root.Children));
            var binding = Assert.IsType<EntryBinding>(Assert.Single(group.Children));
            binding.Value = true;

            Assert.True(entry.Value);
            Assert.True(observed);
            Assert.Equal("Invincible", binding.Key);
            Assert.Same(entry, binding.Entry);
        }

        [Fact]
        public void EntryBinding_IsEditableButDoesNotExposeResetCapability()
        {
            using var user = new UserContext("user", Name);
            user.Service.Control.CreateTable("Player", Name);
            user.Service.Control.Bind(
                "Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);
            var root = GroupBindingFactory.CreateRoot(user);
            var group = Assert.IsType<GroupBinding>(Assert.Single(root.Children));
            var binding = Assert.IsType<EntryBinding>(Assert.Single(group.Children));

            Assert.IsAssignableFrom<IEntryBinding>(binding);
            Assert.False((object)binding is IResettableEntryBinding);
        }

        [Fact]
        public void UserEditor_UsesIndependentEditorsWithoutHotkeyOrResetControls()
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            using var editor = new ControlUserEditor(
                unityService.Object,
                unityGui.Object,
                new EntryStyleResource(unityGui.Object));

            var hotkeyBinding = new Mock<IEntryBinding>(MockBehavior.Strict);
            hotkeyBinding.SetupGet(x => x.ValueType).Returns(typeof(Hotkey));
            hotkeyBinding.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            var dualBinding = new Mock<IEntryBinding>(MockBehavior.Strict);
            dualBinding.SetupGet(x => x.ValueType).Returns(typeof(ControlEntryValue<int, string>));
            dualBinding.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);

            Assert.IsType<UnsupportedEditor>(editor.GroupEditor.ValueEditors.GetEditor(hotkeyBinding.Object));
            Assert.IsType<DualValueEditor>(editor.GroupEditor.ValueEditors.GetEditor(dualBinding.Object));
            Assert.Equal(0f, editor.GroupEditor.EntryEditor.TrailingActionWidth);
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void RegisterContext_WhenControlStructureChanges_RebuildsProjectedTree()
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var styleProvider = new EntryStyleResource(unityGui.Object);
            var host = new TestGuiHost();
            host.Configure(
                unityService.Object,
                new ToastEditor(unityService.Object, unityGui.Object, styleProvider));
            using var user = new UserContext("user", Name);

            host.RegisterContext(user);
            var context = Assert.IsType<GuiContext>(user.GetChildContext(TestGuiHost.ContextKey));
            var originalRoot = context.UserData;
            Assert.Empty(originalRoot.Children);

            user.Service.Control.CreateTable("Player", Name, Description);
            user.Service.Control.Bind(
                "Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);

            Assert.NotSame(originalRoot, context.UserData);
            var group = Assert.IsType<GroupBinding>(Assert.Single(context.UserData.Children));
            Assert.Single(group.Children);
            unityService.VerifyNoOtherCalls();
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Update_RefreshesAllUsersAndVisiblePolicyOnShowFrame()
        {
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityService.SetupGet(x => x.UnscaledDeltaTime).Returns(0.25f);
            var host = new TestGuiHost();
            using var first = new UserContext("first", Name);
            using var second = new UserContext("second", Name);
            var firstTarget = 1;
            var secondTarget = 10;
            first.Service.Control.CreateTable("Player", Name);
            second.Service.Control.CreateTable("Player", Name);
            var firstEntry = first.Service.Control.Bind(
                "Player", "Health", () => firstTarget, ControlUpdatePolicy.EveryFrame, Name, Description);
            var secondEntry = second.Service.Control.Bind(
                "Player", "Health", () => secondTarget, ControlUpdatePolicy.WhenVisibleEverySecond, Name, Description);
            host.ConfigureForUpdate(unityService.Object, new[] { first, second });

            firstTarget = 2;
            secondTarget = 20;
            host.Update();
            Assert.Equal(2, firstEntry.Value);
            Assert.Equal(10, secondEntry.Value);

            host.ToggleVisibility();
            host.Update();

            Assert.Equal(20, secondEntry.Value);
            unityService.VerifyGet(x => x.UnscaledDeltaTime, Times.Exactly(2));
            unityService.VerifyNoOtherCalls();
        }

        [Fact]
        public void PendingEdit_RemainsVisibleAcrossExternalRefreshUntilCommitted()
        {
            using var user = new UserContext("user", Name);
            user.Service.Control.CreateTable("Player", Name);
            var target = 10;
            var entry = user.Service.Control.Bind(
                "Player", "Health", () => target, ControlUpdatePolicy.EveryFrame, Name, Description);
            var root = GroupBindingFactory.CreateRoot(user);
            var group = (GroupBinding)Assert.Single(root.Children);
            var binding = Assert.IsType<EntryBinding>(Assert.Single(group.Children));
            var context = new GuiContext(user.Service.Control) { UserData = root };
            context.ChangeSink.SetConvertedValue(binding, "25", 0.5f);

            target = 15;
            user.Service.Control.Update(0.5f, false);

            Assert.Equal(25, ValueProvider.GetValue(binding));
            Assert.Equal(15, entry.Value);
            context.ChangeSink.FlushValue(0.5f);
            Assert.Equal(25, entry.Value);
        }

        private sealed class TestGuiHost : GuiHost
        {
            public const string ContextKey = "HControlGUI";

            public void Configure(IUnityProvider unityService, ToastEditor toastEditor)
            {
                UnityService = unityService;
                ToastEditor = toastEditor;
                GuiContextKey = ContextKey;
            }

            public void ConfigureForUpdate(IUnityProvider unityService, IEnumerable<UserContext> users)
            {
                UnityService = unityService;
                Users = users;
                GuiContextKey = ContextKey;
            }
        }
    }
}
