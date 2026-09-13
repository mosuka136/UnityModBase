using System.Reflection;
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
        private const string ContextKey = "HControlGUI";
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
            // 双元素值只能通过多元素绑定进入组合编辑器；控制 GUI 复用同一共享编辑器池。
            var dualBinding = new Mock<IEntryMultipleBinding>(MockBehavior.Strict);
            dualBinding.SetupGet(x => x.ValueType).Returns(typeof(EntryValue<int, string>));
            dualBinding.SetupGet(x => x.Metadata).Returns((IUiMetadata)null);
            dualBinding.SetupGet(x => x.Count).Returns(2);
            dualBinding.SetupGet(x => x.BaseDescription).Returns(new Translator());
            dualBinding.SetupGet(x => x.ValueDescription)
                .Returns(new[] { new Translator(), new Translator() });

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
            var host = new GuiHost();
            Configure(
                host,
                unityService.Object,
                new ToastEditor(unityService.Object, unityGui.Object, styleProvider));
            using var user = new UserContext("user", Name);

            host.RegisterContext(user);
            var context = Assert.IsType<GuiContext>(user.GetChildContext(ContextKey));
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
            // ToggleVisibility 显示分支会读取 FrameCount 记录打开帧，Strict 替身需要预置该值。
            unityService.SetupGet(x => x.FrameCount).Returns(1);
            var host = new GuiHost();
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
            ConfigureForUpdate(host, unityService.Object, new[] { first, second });

            firstTarget = 2;
            secondTarget = 20;
            InvokeUpdate(host);
            Assert.Equal(2, firstEntry.Value);
            Assert.Equal(10, secondEntry.Value);

            host.ToggleVisibility();
            InvokeUpdate(host);

            Assert.Equal(20, secondEntry.Value);
            unityService.VerifyGet(x => x.UnscaledDeltaTime, Times.Exactly(2));
            unityService.VerifyGet(x => x.FrameCount, Times.Once);
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

            // 暂存输入在提交前保留原文（"25"），不被外部刷新覆盖也不会提前写入条目。
            Assert.Equal("25", ValueProvider.GetValue(binding));
            Assert.Equal(15, entry.Value);
            context.ChangeSink.FlushValue(0.5f);
            Assert.Equal(25, entry.Value);
        }

        private static void Configure(GuiHost host, IUnityProvider unityService, ToastEditor toastEditor)
        {
            SetProperty(host, "UnityService", unityService);
            SetProperty(host, "ToastEditor", toastEditor);
            SetProperty(host, "GuiContextKey", ContextKey);
        }

        private static void ConfigureForUpdate(
            GuiHost host,
            IUnityProvider unityService,
            IEnumerable<UserContext> users)
        {
            SetProperty(host, "UnityService", unityService);
            SetProperty(host, "Users", users);
            SetProperty(host, "GuiContextKey", ContextKey);
        }

        private static void SetProperty(GuiHost host, string propertyName, object value)
        {
            var property = typeof(GuiHostBase).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            property.SetValue(host, value);
        }

        private static void InvokeUpdate(GuiHost host)
        {
            var method = typeof(GuiHost).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(host, null);
        }
    }
}
