using UnityModBase.HControlSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HControlSpace
{
    public class ControlServiceTests
    {
        private static readonly Translator Name = new Translator("名称", "Name");
        private static readonly Translator Description = new Translator("说明", "Description");

        [Theory]
        [InlineData("Player")]
        [InlineData("玩家_1")]
        public void CreateTable_WhenKeyUsesControlKeyRules_AcceptsUnicodeLettersDigitsAndUnderscore(string key)
        {
            using var service = new ControlService();

            service.CreateTable(key, Name);

            Assert.True(service.Sheet.Contains(key));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Player-Stats")]
        [InlineData("Player.Stats")]
        public void CreateTable_WhenKeyViolatesControlKeyRules_ThrowsArgumentException(string key)
        {
            using var service = new ControlService();

            Assert.Equal("key", Assert.Throws<ArgumentException>(() => service.CreateTable(key, Name)).ParamName);
        }

        [Fact]
        public void CreateTable_WhenNameIsNull_UsesKeyDerivedTranslator()
        {
            using var service = new ControlService();

            service.CreateTable("Player", null, Description);

            var table = service.Sheet["Player"];
            Assert.Equal("Player", table.Name.Chinese);
            Assert.Equal("Player", table.Name.English);
            Assert.Same(Description, table.Description);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Current-Health")]
        public void Bind_WhenEntryKeyViolatesControlKeyRules_ThrowsArgumentException(string key)
        {
            using var service = CreateServiceWithTable();

            Assert.Equal("key", Assert.Throws<ArgumentException>(() => service.Bind(
                "Player", key, () => 10, ControlUpdatePolicy.Never, Name, Description)).ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Player-Stats")]
        public void Bind_WhenTableKeyViolatesSharedKeyRules_ThrowsArgumentException(string tableKey)
        {
            using var service = CreateServiceWithTable();

            Assert.Equal("tableKey", Assert.Throws<ArgumentException>(() => service.Bind(
                tableKey, "Health", () => 10, ControlUpdatePolicy.Never, Name, Description)).ParamName);
        }

        [Fact]
        public void CreateTableAndBind_WithValidValues_PreservesDeclarationOrderAndReadsInitialValue()
        {
            using var service = new ControlService();
            var getterCalls = 0;
            service.CreateTable("Player", Name, Description);

            var entry = service.Bind(
                "Player",
                "Invincible",
                () =>
                {
                    getterCalls++;
                    return true;
                },
                ControlUpdatePolicy.Never,
                Name,
                Description);

            var table = Assert.Single(service.Sheet).Value;
            Assert.Same(entry, Assert.Single(table));
            Assert.Equal("Player", entry.TableKey);
            Assert.Equal("Invincible", entry.Key);
            Assert.True(entry.Value);
            Assert.Equal(1, getterCalls);
        }

        [Fact]
        public void Bind_WhenGetterThrows_PropagatesAndDoesNotAddEntry()
        {
            using var service = CreateServiceWithTable();

            Assert.Throws<InvalidOperationException>(() => service.Bind<int>(
                "Player",
                "Health",
                () => throw new InvalidOperationException("getter failed"),
                ControlUpdatePolicy.Never,
                Name,
                Description));

            Assert.Empty(service.Sheet["Player"]);
        }

        [Fact]
        public void Bind_WhenGetterReturnsNull_CachesNullWithoutFailing()
        {
            using var service = CreateServiceWithTable();

            var entry = service.Bind<string>(
                "Player",
                "Name",
                () => null,
                ControlUpdatePolicy.Never,
                Name,
                Description);

            Assert.Null(entry.Value);
            Assert.Null(entry.BoxedValue);
            Assert.Same(entry, Assert.Single(service.Sheet["Player"]));
        }

        [Theory]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(object))]
        public void Bind_WhenValueTypeOutsideEntryModel_ThrowsBeforeCallingGetter(Type valueType)
        {
            // 服务层按共享条目模型拒绝不受支持的值类型，getter 不应被调用。
            using var service = CreateServiceWithTable();
            var getterCalls = 0;
            var method = typeof(ControlServiceTests)
                .GetMethod(nameof(BindUnsupported), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .MakeGenericMethod(valueType);

            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method.Invoke(null, new object[] { service, (Action)(() => getterCalls++) }));

            var inner = Assert.IsType<ArgumentException>(exception.InnerException);
            Assert.Equal("valueType", inner.ParamName);
            Assert.Equal(0, getterCalls);
            Assert.Empty(service.Sheet["Player"]);
        }

        [Theory]
        [InlineData(typeof(EntryValue<int, int>))]
        [InlineData(typeof(MalformedMultipleValue<int, int>))]
        public void Bind_WhenValueTypeIsMultipleValue_ThrowsBeforeCallingGetter(Type valueType)
        {
            // 多元素类型属于共享条目模型、能通过服务层类型检查，但实时控制只接受单值类型，由条目构造函数拒绝。
            using var service = CreateServiceWithTable();
            var getterCalls = 0;
            var method = typeof(ControlServiceTests)
                .GetMethod(nameof(BindUnsupported), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .MakeGenericMethod(valueType);

            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method.Invoke(null, new object[] { service, (Action)(() => getterCalls++) }));

            var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("not a supported single value type", inner.Message);
            Assert.Equal(0, getterCalls);
            Assert.Empty(service.Sheet["Player"]);
        }

        [Fact]
        public void Bind_WithSliderMetadata_PreservesMetadataInstance()
        {
            using var service = CreateServiceWithTable();
            var slider = new UiSliderMetadata(0f, 100f, 1f);

            var number = service.Bind(
                "Player", "Health", () => 50, ControlUpdatePolicy.Never, Name, Description, slider);

            Assert.Same(slider, number.Metadata);
            Assert.Equal(50, number.Value);
        }

        [Fact]
        public void Bind_WhenMetadataOmitted_UsesKeyDerivedNameAndEmptyDescription()
        {
            using var service = CreateServiceWithTable();

            var entry = service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, null, null);

            Assert.Equal("Health", entry.Name.Chinese);
            Assert.Equal("Health", entry.Name.English);
            Assert.Equal(string.Empty, entry.Description.Chinese);
            Assert.Equal(string.Empty, entry.Description.English);
            Assert.Equal(10, entry.Value);
        }

        [Fact]
        public void Bind_WhenValueTypeIsMultipleValue_ThrowsEvenWithCompositeMetadata()
        {
            using var service = CreateServiceWithTable();
            var dualMetadata = new UiCompositeMetadata(new IUiMetadata[] { new UiSliderMetadata(0f, 100f, 1f), null });

            Assert.Throws<InvalidOperationException>(() => service.Bind<EntryValue<int, string>>(
                "Player",
                "Position",
                () => new EntryValue<int, string>(7, "north"),
                ControlUpdatePolicy.Never,
                Name,
                Description,
                dualMetadata));
            Assert.Empty(service.Sheet["Player"]);
        }

        [Fact]
        public void Bind_WhenTableOrEntryAlreadyExists_ThrowsWithoutReplacingModel()
        {
            using var service = CreateServiceWithTable();
            var original = service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);

            Assert.Throws<InvalidOperationException>(() => service.CreateTable("Player", Name));
            Assert.Throws<InvalidOperationException>(() =>
                service.Bind("Player", "Health", () => 20, ControlUpdatePolicy.Never, Name, Description));
            Assert.Same(original, Assert.Single(service.Sheet["Player"]));
        }

        [Fact]
        public void GuiValueChange_UpdatesCacheAndPublishesOnlyActualChanges()
        {
            using var service = CreateServiceWithTable();
            var entry = service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);
            var values = new List<int>();
            entry.OnValueChanged += (_, value) => values.Add(value);

            ((IControlEntryInternal)entry).SetBoxedValueFromGui(10);
            ((IControlEntryInternal)entry).SetBoxedValueFromGui(20);
            ((IControlEntryInternal)entry).SetBoxedValueFromGui(20);

            Assert.Equal(20, entry.Value);
            Assert.Equal(new[] { 20 }, values);
        }

        [Fact]
        public void GuiValueChange_WhenSubscriberThrows_InvokesRemainingSubscriberAndKeepsValue()
        {
            using var service = CreateServiceWithTable();
            var entry = service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);
            var observed = 0;
            entry.OnValueChanged += (_, _) => throw new InvalidOperationException("subscriber failed");
            entry.OnValueChanged += (_, value) => observed = value;

            var exception = Record.Exception(() => ((IControlEntryInternal)entry).SetBoxedValueFromGui(25));

            Assert.Null(exception);
            Assert.Equal(25, entry.Value);
            Assert.Equal(25, observed);
        }

        [Fact]
        public void GuiValueChange_WhenValueIsNull_ThrowsArgumentNullExceptionAndKeepsCache()
        {
            using var service = CreateServiceWithTable();
            var entry = service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);
            var eventCalls = 0;
            entry.OnValueChanged += (_, _) => eventCalls++;

            Assert.Throws<ArgumentNullException>(() => ((IControlEntryInternal)entry).SetBoxedValueFromGui(null));

            Assert.Equal(10, entry.Value);
            Assert.Equal(0, eventCalls);
        }

        [Fact]
        public void GuiValueChange_WhenValueTypeIncompatible_ThrowsInvalidOperationExceptionAndKeepsCache()
        {
            using var service = CreateServiceWithTable();
            var entry = service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);
            var eventCalls = 0;
            entry.OnValueChanged += (_, _) => eventCalls++;

            Assert.Throws<InvalidOperationException>(() => ((IControlEntryInternal)entry).SetBoxedValueFromGui("not-a-number"));
            Assert.Throws<InvalidOperationException>(() => ((IControlEntryInternal)entry).SetBoxedValueFromGui(new EntryValue<int, int>(1, 2)));

            Assert.Equal(10, entry.Value);
            Assert.Equal(0, eventCalls);
        }

        [Fact]
        public void EveryFrame_RefreshesEachUpdateWithoutPublishingGuiEvent()
        {
            using var service = CreateServiceWithTable();
            var target = 1;
            var eventCalls = 0;
            var entry = service.Bind("Player", "Health", () => target, ControlUpdatePolicy.EveryFrame, Name, Description);
            entry.OnValueChanged += (_, _) => eventCalls++;

            target = 2;
            service.Update(0.01f, false);
            target = 3;
            service.Update(0.01f, false);

            Assert.Equal(3, entry.Value);
            Assert.Equal(0, eventCalls);
        }

        [Fact]
        public void Refresh_WhenGetterReturnsEquivalentNewCollection_PreservesCachedReference()
        {
            using var service = CreateServiceWithTable();
            var target = new[] { 1, 2 };
            var entry = service.Bind(
                "Player", "Health", () => target, ControlUpdatePolicy.EveryFrame, Name, Description);
            var initial = entry.Value;

            target = new[] { 1, 2 };
            service.Update(0.1f, false);

            Assert.Same(initial, entry.Value);

            target = new[] { 1, 3 };
            service.Update(0.1f, false);

            Assert.Equal(new[] { 1, 3 }, entry.Value);
        }

        [Fact]
        public void EverySecond_UsesUnscaledAccumulationAndRefreshesAtMostOncePerUpdate()
        {
            using var service = CreateServiceWithTable();
            var target = 1;
            var getterCalls = 0;
            var entry = service.Bind(
                "Player", "Health", () => { getterCalls++; return target; }, ControlUpdatePolicy.EverySecond, Name, Description);

            target = 2;
            service.Update(0.4f, false);
            service.Update(0.59f, false);
            Assert.Equal(1, entry.Value);
            service.Update(0.01f, false);
            Assert.Equal(2, entry.Value);
            target = 3;
            service.Update(5f, false);

            Assert.Equal(3, entry.Value);
            Assert.Equal(3, getterCalls);
        }

        [Fact]
        public void WhenVisibleEveryFrame_RefreshesOnlyWhileWindowIsVisible()
        {
            using var service = CreateServiceWithTable();
            var target = 1;
            var entry = service.Bind("Player", "Health", () => target, ControlUpdatePolicy.WhenVisibleEveryFrame, Name, Description);

            target = 2;
            service.Update(0.1f, false);
            Assert.Equal(1, entry.Value);
            service.Update(0.1f, true, true);

            Assert.Equal(2, entry.Value);
        }

        [Fact]
        public void WhenVisibleEverySecond_RefreshesOnShowThenRestartsVisibleTimer()
        {
            using var service = CreateServiceWithTable();
            var target = 1;
            var getterCalls = 0;
            var entry = service.Bind(
                "Player", "Health", () => { getterCalls++; return target; }, ControlUpdatePolicy.WhenVisibleEverySecond, Name, Description);

            target = 2;
            service.Update(20f, false);
            service.Update(0f, true, true);
            Assert.Equal(2, entry.Value);
            target = 3;
            service.Update(0.99f, true);
            Assert.Equal(2, entry.Value);
            service.Update(0.01f, true);

            Assert.Equal(3, entry.Value);
            Assert.Equal(3, getterCalls);
        }

        [Fact]
        public void When_UsesLevelTriggeredConditionEachFrame()
        {
            using var service = CreateServiceWithTable();
            var condition = false;
            var target = 1;
            var getterCalls = 0;
            var entry = service.Bind(
                "Player", "Health", () => { getterCalls++; return target; }, ControlUpdatePolicy.When(() => condition), Name, Description);

            target = 2;
            service.Update(0.1f, false);
            condition = true;
            service.Update(0.1f, false);
            target = 3;
            service.Update(0.1f, false);

            Assert.Equal(3, entry.Value);
            Assert.Equal(3, getterCalls);
        }

        [Fact]
        public void WhenVisible_DoesNotEvaluateConditionWhileHidden()
        {
            using var service = CreateServiceWithTable();
            var conditionCalls = 0;
            var target = 1;
            var entry = service.Bind(
                "Player",
                "Health",
                () => target,
                ControlUpdatePolicy.WhenVisible(() => { conditionCalls++; return true; }),
                Name,
                Description);

            target = 2;
            service.Update(0.1f, false);
            Assert.Equal(0, conditionCalls);
            service.Update(0.1f, true, true);

            Assert.Equal(1, conditionCalls);
            Assert.Equal(2, entry.Value);
        }

        [Fact]
        public void Never_DoesNotReadAgainAfterBind()
        {
            using var service = CreateServiceWithTable();
            var getterCalls = 0;
            var entry = service.Bind(
                "Player", "Health", () => { getterCalls++; return getterCalls; }, ControlUpdatePolicy.Never, Name, Description);

            service.Update(10f, true, true);

            Assert.Equal(1, getterCalls);
            Assert.Equal(1, entry.Value);
        }

        [Fact]
        public void Update_WhenOneGetterFails_PreservesItsCacheAndContinuesOtherEntries()
        {
            using var service = CreateServiceWithTable();
            var shouldThrow = false;
            var target = 1;
            var failing = service.Bind(
                "Player", "Failing", () => shouldThrow ? throw new InvalidOperationException("failed") : target,
                ControlUpdatePolicy.EveryFrame, Name, Description);
            var healthyTarget = 10;
            var healthy = service.Bind(
                "Player", "Healthy", () => healthyTarget, ControlUpdatePolicy.EveryFrame, Name, Description);

            shouldThrow = true;
            target = 2;
            healthyTarget = 20;
            var exception = Record.Exception(() => service.Update(0.1f, false));

            Assert.Null(exception);
            Assert.Equal(1, failing.Value);
            Assert.Equal(20, healthy.Value);
        }

        [Fact]
        public void StructureChanged_FiresAfterCommittedTableAndEntrySnapshots()
        {
            using var service = new ControlService();
            var counts = new List<(int Tables, int Entries)>();
            service.OnStructureChanged += () => counts.Add((
                service.Sheet.Count,
                service.Sheet.Sum(table => table.Value.Count())));

            service.CreateTable("Player", Name);
            service.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);

            Assert.Equal(new[] { (1, 0), (1, 1) }, counts);
        }

        [Fact]
        public void GuiValueChange_DoesNotModifyUnrelatedFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "UnityModBase.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "settings.cfg");
                File.WriteAllText(path, "Persistent = 7");
                var before = File.ReadAllText(path);

                using var controls = CreateServiceWithTable();
                var entry = controls.Bind("Player", "Health", () => 10, ControlUpdatePolicy.Never, Name, Description);
                ((IControlEntryInternal)entry).SetBoxedValueFromGui(25);

                Assert.Equal(before, File.ReadAllText(path));
                Assert.Equal(25, entry.Value);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static ControlService CreateServiceWithTable()
        {
            var service = new ControlService();
            service.CreateTable("Player", Name, Description);
            return service;
        }

        private static void BindUnsupported<T>(ControlService service, Action onGetterCalled)
        {
            service.Bind<T>(
                "Player",
                "Unsupported",
                () =>
                {
                    onGetterCalled();
                    return default;
                },
                ControlUpdatePolicy.Never,
                Name,
                Description);
        }

        // 仅实现接口但违反 Value1..ValueN 属性契约的形状，用于验证注册入口的类型校验。
        private sealed class MalformedMultipleValue<T1, T2> : IEntryMultipleValue
        {
            public bool Equals(IEntryValue other)
            {
                return ReferenceEquals(this, other);
            }
        }
    }
}
