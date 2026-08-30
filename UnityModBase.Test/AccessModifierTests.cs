using System.Reflection;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;
using ConfigEntryBinding = UnityModBase.HConfigGUI.Bindings.EntryBinding;
using ConfigGroupBindingFactory = UnityModBase.HConfigGUI.Bindings.GroupBindingFactory;
using ConfigGuiHost = UnityModBase.HConfigGUI.GuiHost;
using ControlGuiHost = UnityModBase.HControlGUI.GuiHost;
using LogGuiHost = UnityModBase.HLogGUI.GuiHost;

namespace UnityModBase.Test
{
    public class AccessModifierTests
    {
        [Fact]
        public void ConcreteProductionTypes_OnlyDocumentedExtensionBasesRemainUnsealed()
        {
            var expected = new[]
            {
                "UnityModBase.HGuiSpace.EditableGuiContext",
                "UnityModBase.HGuiSpace.EditableUserEditorBase",
                "UnityModBase.HGuiSpace.Editor.EntryEditor",
                "UnityModBase.HGuiSpace.Editor.GroupEditor",
                "UnityModBase.HGuiSpace.Editor.ValueEditor.NumberEditor",
                "UnityModBase.HGuiSpace.Resource.EntryStyleResource",
            };

            var actual = typeof(global::UnityModBase.UnityModBase).Assembly
                .GetTypes()
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    !type.IsSealed &&
                    (type.IsPublic || type.IsNotPublic || type.IsNestedPublic || type.IsNestedAssembly))
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConfigGuiAdapters_AreInternalImplementationTypes()
        {
            Assert.True(typeof(ConfigEntryBinding).IsNotPublic);
            Assert.True(typeof(ConfigGroupBindingFactory).IsNotPublic);

            var createRoot = typeof(ConfigGroupBindingFactory).GetMethod(
                "CreateRoot",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(createRoot);
            Assert.True(createRoot.IsAssembly);
        }

        [Fact]
        public void ConfigCollectionConstructionHelpers_AreInternal()
        {
            var methodNames = new[]
            {
                "CreateCollectionResult",
                "CreateTypedArray",
                "CreateTypedList",
                "FindAddMethod",
                "TryCreateCollectionFromAddMethod",
                "TryCreateCollectionFromConstructor",
                "ValidateCollectionElement",
                "ValidateCollectionElements",
            };

            foreach (var methodName in methodNames)
            {
                var method = typeof(ConfigFileModel).GetMethod(
                    methodName,
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(method);
                Assert.True(method.IsAssembly, $"{methodName} should be internal.");
            }
        }

        [Fact]
        public void UnityMessageMethods_AreProtected()
        {
            AssertProtectedInstanceMethod(typeof(GuiHostBase), "Awake");
            AssertProtectedInstanceMethod(typeof(GuiHostBase), "Update");
            AssertProtectedInstanceMethod(typeof(GuiHostBase), "OnGUI");
            AssertProtectedInstanceMethod(typeof(ConfigGuiHost), "Awake");
            AssertProtectedInstanceMethod(typeof(ConfigGuiHost), "Update");
            AssertProtectedInstanceMethod(typeof(ConfigGuiHost), "OnGUI");
            AssertProtectedInstanceMethod(typeof(ControlGuiHost), "Awake");
            AssertProtectedInstanceMethod(typeof(ControlGuiHost), "Update");
            AssertProtectedInstanceMethod(typeof(LogGuiHost), "Awake");
            AssertProtectedInstanceMethod(typeof(LogGuiHost), "OnGUI");
        }

        [Fact]
        public void SubsystemLifecycleMethods_AreNotPublic()
        {
            AssertPrivateStaticMethod(typeof(GameQuitManager), "Initialize");
            AssertInternalStaticMethod(typeof(GameQuitManager), "Dispose");
            AssertInternalStaticMethod(typeof(GameBootRegistry), "Initialize");
            AssertInternalStaticMethod(typeof(GameBootRegistry), "Dispose");
            AssertInternalStaticMethod(typeof(FrameUpdateManager), "Dispose");
            AssertInternalStaticMethod(typeof(Translator), "Dispose");
            AssertInternalStaticMethod(typeof(UserManager), "Dispose");
        }

        [Fact]
        public void MutableSentinelsAndLogState_AreExternallyReadOnly()
        {
            Assert.Null(typeof(global::UnityModBase.HConfigGUI.GuiContext).GetField(
                "InvalidGuiContext",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));

            Assert.Null(typeof(EntryEditBuffer.OrderedEntry).GetProperty("Order").SetMethod);
            Assert.Null(typeof(EntryEditBuffer.OrderedEntry).GetProperty("Value").SetMethod);
            Assert.Null(typeof(EntryEditBuffer.OrderedEntry).GetProperty("IsValid").SetMethod);

            Assert.True(typeof(LogEntry).GetProperty("LastRepeatTime").GetSetMethod(true).IsPrivate);
            Assert.True(typeof(LogEntry).GetProperty("RepeatCount").GetSetMethod(true).IsPrivate);
        }

        [Fact]
        public void LogGuiCollectionProperties_ExposeReadOnlyInterfaces()
        {
            Assert.Equal(
                typeof(IReadOnlyList<global::UnityModBase.HLogGUI.EntryBinding>),
                typeof(global::UnityModBase.HLogGUI.GroupBinding).GetProperty("SortedGroup").PropertyType);
            Assert.Equal(
                typeof(IReadOnlyDictionary<global::UnityModBase.HLogGUI.EntryContentType, global::UnityModBase.HLogGUI.GroupEditor.ColumnEditor>),
                typeof(global::UnityModBase.HLogGUI.GroupEditor).GetProperty("ColumnEditorList").PropertyType);
        }

        private static void AssertProtectedInstanceMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.True(method.IsFamily, $"{type.FullName}.{methodName} should be protected.");
        }

        private static void AssertPrivateStaticMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.True(method.IsPrivate, $"{type.FullName}.{methodName} should be private.");
        }

        private static void AssertInternalStaticMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.True(method.IsAssembly, $"{type.FullName}.{methodName} should be internal.");
        }
    }
}
