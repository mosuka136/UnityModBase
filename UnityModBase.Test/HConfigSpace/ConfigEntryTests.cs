using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;
using System.Collections;
using UnityModBase.HEntrySpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigEntryTests
    {
        // 重构后 ConfigEntry<T> 只保留五参构造函数（含 name/description）。
        // 绝大多数测试不关心展示文本，统一通过该工厂构造，避免在每个用例里重复空 Translator。
        private static ConfigEntry<T> CreateEntry<T>(string tableKey, ConfigFileEntry entry, T defaultValue)
        {
            return new ConfigEntry<T>(entry, defaultValue, new Translator(), new Translator());
        }

        [Fact]
        public void Name_WhenEntryHasName_ReturnsEntryName()
        {
            // Arrange
            var expectedName = new Translator(chinese: "名称", english: "Name");
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "\"test\"",
                Name = expectedName
            };
            var entry = new ConfigEntry<string>(model, "default", expectedName, new Translator());

            // Act
            var actualName = entry.Name;

            // Assert
            Assert.Same(expectedName, actualName);
        }

        [Fact]
        public void Description_WhenEntryHasDescription_ReturnsEntryDescription()
        {
            // Arrange
            var expectedDescription = new Translator(chinese: "描述", english: "Description");
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "\"test\"",
                Description = expectedDescription
            };
            var entry = new ConfigEntry<string>(model, "default", new Translator(), expectedDescription);

            // Act
            var actualDescription = entry.Description;

            // Assert
            Assert.Same(expectedDescription, actualDescription);
        }

        [Fact]
        public void ValueType_WhenCreated_ReturnsTypeOfGenericParameter()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);

            // Act
            var valueType = entry.ValueType;

            // Assert
            Assert.Equal(typeof(int), valueType);
        }

        // 注：原 RebindEntry 系列测试覆盖的“单项直接重绑定”入口已在本次重构中随 RebindEntry 一并移除。
        // 其行为由公开的事务协议方法覆盖：
        //   - 空候选项 → PrepareBind_WhenCandidateIsNull_ReturnsFalseAndProducesErrorMessage
        //   - 有效重绑定更新值与绑定 → ApplyBind_WhenValueChanged_SwitchesBindingAndUpdatesValueWithoutPublishing
        //     及 PublishBind_AfterApply_RaisesTypedAndBaseEventsOnce
        //   - 解码失败 → PrepareBind_WhenDecodeFails_ReturnsFalseWithoutChangingActiveBinding
        //     （新协议改为返回 false 并附带诊断，不再抛 InvalidOperationException）
        // 因此删除这三个只验证已移除 API 的测试。

        [Fact]
        public void Value_WhenSetToNull_ThrowsInvalidOperationException()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "\"test\""
            };
            var entry = CreateEntry("General", model, "default");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => entry.Value = null);
            Assert.Contains("Failed to encode value for key", exception.Message);
            Assert.Contains("TestKey", exception.Message);
        }

        [Fact]
        public void Value_WhenAssignedEquivalentValue_DoesNotRaiseTypedOrBaseEvents()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            var typedInvocationCount = 0;
            var baseInvocationCount = 0;
            entry.OnValueChanged += (sender, value) => typedInvocationCount++;
            entry.OnValueChangedBase += (sender, args) => baseInvocationCount++;

            // Act
            entry.Value = 42;

            // Assert
            Assert.Equal(0, typedInvocationCount);
            Assert.Equal(0, baseInvocationCount);
            Assert.Equal(42, entry.Value);
            Assert.Equal("42", model.Value);
        }

        [Fact]
        public void Value_WhenTypedAndBaseHandlersThrow_UpdatesValueAndInvokesRemainingHandlers()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            var invocationOrder = new List<string>();
            var typedValue = 0;
            var baseValue = 0;

            entry.OnValueChanged += (sender, value) =>
            {
                invocationOrder.Add("typed-throw");
                throw new InvalidOperationException("Typed handler failed.");
            };
            entry.OnValueChanged += (sender, value) =>
            {
                invocationOrder.Add("typed-next");
                typedValue = value;
            };
            entry.OnValueChangedBase += (sender, args) =>
            {
                invocationOrder.Add("base-throw");
                throw new InvalidOperationException("Base handler failed.");
            };
            entry.OnValueChangedBase += (sender, args) =>
            {
                invocationOrder.Add("base-next");
                baseValue = Assert.IsType<EntryValueChangedEventArgs<int>>(args).Value;
            };

            // Act
            entry.Value = 100;

            // Assert
            Assert.Equal(100, entry.Value);
            Assert.Equal("100", model.Value);
            Assert.Equal(100, typedValue);
            Assert.Equal(100, baseValue);
            Assert.Equal(new[] { "typed-throw", "typed-next", "base-throw", "base-next" }, invocationOrder);
        }

        [Fact]
        public void BoxedValue_WhenGetting_ReturnsValue()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            IConfigEntry iEntry = entry;

            // Act
            var boxedValue = iEntry.BoxedValue;

            // Assert
            Assert.Equal(42, boxedValue);
        }

        [Fact]
        public void BoxedValue_WhenSetting_UpdatesValue()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            IConfigEntry iEntry = entry;

            // Act
            iEntry.BoxedValue = 100;

            // Assert
            Assert.Equal(100, entry.Value);
            Assert.Equal(100, iEntry.BoxedValue);
        }

        [Fact]
        public void BoxedDefaultValue_WhenGetting_ReturnsDefaultValue()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "\"test\""
            };
            var entry = CreateEntry("General", model, "defaultValue");
            IConfigEntry iEntry = entry;

            // Act
            var boxedDefaultValue = iEntry.BoxedDefaultValue;

            // Assert
            Assert.Equal("defaultValue", boxedDefaultValue);
        }

        [Fact]
        public void OnValueChangedBase_WhenAdding_SubscribesToOnValueChanged()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            IConfigEntry iEntry = entry;
            var invocationCount = 0;
            object actualSender = null;
            EventArgs actualArgs = null;
            EventHandler handler = (sender, args) =>
            {
                invocationCount++;
                actualSender = sender;
                actualArgs = args;
            };

            // Act
            iEntry.OnValueChangedBase += handler;
            entry.Value = 100;

            // Assert
            Assert.Equal(1, invocationCount);
            Assert.Same(entry, actualSender);
            var valueChangedArgs = Assert.IsType<EntryValueChangedEventArgs<int>>(actualArgs);
            Assert.Equal(100, valueChangedArgs.Value);
        }

        [Fact]
        public void OnValueChangedBase_WhenRemoving_UnsubscribesFromOnValueChanged()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            IConfigEntry iEntry = entry;
            var invocationCount = 0;
            EventHandler handler = (sender, args) => invocationCount++;

            // Act
            iEntry.OnValueChangedBase += handler;
            iEntry.OnValueChangedBase -= handler;
            entry.Value = 100;

            // Assert
            Assert.Equal(0, invocationCount);
            Assert.Equal(100, entry.Value);
        }

        [Fact]
        public void Constructor_BindsFileEntryAndDecodesValueOverDefault()
        {
            // 重构后构造函数不再接收 tableKey，TableKey 改由绑定文件项派生。
            // 该用例覆盖其核心契约：文件项中已有的有效值优先于声明默认值，默认值只写入元数据。
            // Arrange
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");
            var model = new ConfigFileEntry
            {
                TableKey = "General",
                Key = "TestKey",
                Value = "42",
            };

            // Act
            var entry = new ConfigEntry<int>(model, 10, name, description);

            // Assert
            // TableKey 不再由构造参数提供，而是从绑定的文件项读取。
            Assert.Equal("General", entry.TableKey);
            Assert.Equal(10, entry.DefaultValue);
            Assert.Same(model, entry.Entry);
            // 文件中的 42 优先于声明的默认值 10。
            Assert.Equal(42, entry.Value);
            Assert.Equal(typeof(int), entry.ValueType);
        }

        [Fact]
        public void Constructor_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ConfigEntry<int>(null, 0, name, description));
        }

        // 构造函数对 null 元数据不再抛异常：名称回退为键名派生翻译，说明回退为空翻译。
        // 这两个测试覆盖回退语义，并与下方元数据回读测试互为补充。
        [Fact]
        public void Constructor_WhenNameIsNull_UsesKeyDerivedTranslator()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var description = new Translator(chinese: "描述", english: "Description");

            // Act
            var entry = new ConfigEntry<int>(model, 0, null, description);

            // Assert
            Assert.Equal("TestKey", entry.Name.Chinese);
            Assert.Equal("TestKey", entry.Name.English);
        }

        [Fact]
        public void Constructor_WhenDescriptionIsNull_UsesEmptyTranslator()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var name = new Translator(chinese: "名称", english: "Name");

            // Act
            var entry = new ConfigEntry<int>(model, 0, name, null);

            // Assert
            Assert.Equal(string.Empty, entry.Description.Chinese);
            Assert.Equal(string.Empty, entry.Description.English);
        }

        // 注：原 Constructor_WhenInvalidTableName_ThrowsInvalidOperationException 测试的是
        // 旧版接收 tableKey 参数的构造函数对非法表名的校验。重构后构造函数不再接收 tableKey，
        // TableKey 改由绑定的文件项提供，表名校验随之移至 ConfigFileEntry.TableKey setter、
        // ConfigTable 构造函数与 ConfigService.CreateTable，相关行为由 ConfigServiceTests 与
        // ConfigTableTests 覆盖，故删除该用例。

        [Fact]
        public void Constructor_WhenUnsupportedValueType_ThrowsInvalidOperationException()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "dummy"
            };
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new ConfigEntry<UnsupportedType>(model, new UnsupportedType(), name, description));
            Assert.Contains("Failed to encode value type", exception.Message);
        }

        [Fact]
        public void Constructor_WhenDefaultValueEncodingFails_ThrowsInvalidOperationException()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "null"
            };
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new ConfigEntry<string>(model, null, name, description));
            Assert.Contains("Failed to encode default value", exception.Message);
        }

        [Fact]
        public void EqualBoxed_WhenBothNull_ReturnsTrue()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(null, null);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenFirstNull_ReturnsFalse()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(null, 42);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenSecondNull_ReturnsFalse()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(42, null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenDifferentTypes_ReturnsFalse()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(42, "42");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenPrimitivesEqual_ReturnsTrue()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(42, 42);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenPrimitivesNotEqual_ReturnsFalse()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(42, 43);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenStringsEqual_ReturnsTrue()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed("test", "test");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenStringsNotEqual_ReturnsFalse()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed("test", "other");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenArraysEqual_ReturnsTrue()
        {
            // Arrange
            var array1 = new[] { 1, 2, 3 };
            var array2 = new[] { 1, 2, 3 };

            // Act
            var result = ConfigEntry<int>.EqualBoxed(array1, array2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenArraysNotEqual_ReturnsFalse()
        {
            // Arrange
            var array1 = new[] { 1, 2, 3 };
            var array2 = new[] { 1, 2, 4 };

            // Act
            var result = ConfigEntry<int>.EqualBoxed(array1, array2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenArraysDifferentLength_ReturnsFalse()
        {
            // Arrange
            var array1 = new[] { 1, 2, 3 };
            var array2 = new[] { 1, 2 };

            // Act
            var result = ConfigEntry<int>.EqualBoxed(array1, array2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenEnumerablesEqual_ReturnsTrue()
        {
            // Arrange
            var list1 = new System.Collections.Generic.List<int> { 1, 2, 3 };
            var list2 = new System.Collections.Generic.List<int> { 1, 2, 3 };

            // Act
            var result = ConfigEntry<int>.EqualBoxed(list1, list2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenEnumerablesNotEqual_ReturnsFalse()
        {
            // Arrange
            var list1 = new System.Collections.Generic.List<int> { 1, 2, 3 };
            var list2 = new System.Collections.Generic.List<int> { 1, 2, 4 };

            // Act
            var result = ConfigEntry<int>.EqualBoxed(list1, list2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenEnumerablesDifferentLength_ReturnsFalse()
        {
            // Arrange
            var list1 = new System.Collections.Generic.List<int> { 1, 2, 3 };
            var list2 = new System.Collections.Generic.List<int> { 1, 2 };

            // Act
            var result = ConfigEntry<int>.EqualBoxed(list1, list2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenNonComparableType_ReturnsFalse()
        {
            // Arrange
            var obj1 = new object();
            var obj2 = new object();

            // Act
            var result = ConfigEntry<int>.EqualBoxed(obj1, obj2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EntryValueChangedEventArgs_Constructor_SetsValue()
        {
            // Act
            var args = new EntryValueChangedEventArgs<int>(42);

            // Assert
            Assert.Equal(42, args.Value);
        }

        [Fact]
        public void Constructor_WhenEnumType_SetsAcceptableValues()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "Value1"
            };
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");

            // Act
            var entry = new ConfigEntry<TestEnum>(model, TestEnum.Value1, name, description);

            // Assert
            Assert.NotNull(entry.Entry.AcceptableValues);
            Assert.Contains("Value1", entry.Entry.AcceptableValues);
        }

        [Fact]
        public void EqualBoxed_WhenEnumsEqual_ReturnsTrue()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(TestEnum.Value1, TestEnum.Value1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenEnumsNotEqual_ReturnsFalse()
        {
            // Act
            var result = ConfigEntry<int>.EqualBoxed(TestEnum.Value1, TestEnum.Value2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenEnumerableWithNullEnumerator_ReturnsFalse()
        {
            // Arrange
            var enumerable = new EnumerableWithNullEnumerator();

            // Act
            var result = ConfigEntry<int>.EqualBoxed(enumerable, enumerable);

            // Assert
            Assert.False(result);
        }

        // IConfigEntryValue 分支：EqualBoxed 把比较委托给实现类型自身的 Equals，
        // 因此两个实现了该接口的对象是否相等由实现决定，而非由 EqualBoxed 的内置规则决定。
        [Fact]
        public void EqualBoxed_WhenConfigEntryValuesEqual_ReturnsTrue()
        {
            // Arrange
            var a = new StubConfigEntryValue("same");
            var b = new StubConfigEntryValue("same");

            // Act
            var result = ConfigEntry<int>.EqualBoxed(a, b);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualBoxed_WhenConfigEntryValuesNotEqual_ReturnsFalse()
        {
            // Arrange
            var a = new StubConfigEntryValue("same");
            var b = new StubConfigEntryValue("different");

            // Act
            var result = ConfigEntry<int>.EqualBoxed(a, b);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenFirstConfigEntryValueIsNull_ReturnsFalse()
        {
            // Arrange
            var b = new StubConfigEntryValue("value");

            // Act
            var result = ConfigEntry<int>.EqualBoxed(null, b);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EqualBoxed_WhenSecondConfigEntryValueIsNull_ReturnsFalse()
        {
            // Arrange
            var a = new StubConfigEntryValue("value");

            // Act
            var result = ConfigEntry<int>.EqualBoxed(a, null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void OnValueChangedBase_WhenValueChanges_RaisesEventWithConvertedEventArgs()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = CreateEntry("General", model, 0);
            IConfigEntry iEntry = entry;
            object actualSender = null;
            EventArgs actualArgs = null;
            var invokeCount = 0;

            // Act
            iEntry.OnValueChangedBase += (sender, args) =>
            {
                invokeCount++;
                actualSender = sender;
                actualArgs = args;
            };
            entry.Value = 100;

            // Assert
            Assert.Equal(1, invokeCount);
            Assert.Same(entry, actualSender);
            var changedArgs = Assert.IsType<EntryValueChangedEventArgs<int>>(actualArgs);
            Assert.Equal(100, changedArgs.Value);
        }

        // ---- PrepareBind / ApplyBind / PublishBind / RollbackBind ----
        // 以下测试直接覆盖重构后 IConfigEntry 上新增的事务协议方法。
        // 旧实现把这些逻辑放在内部的 EntryReloadPlan 与 IConfigEntryReloadParticipant 中，
        // 只能通过 RebindEntry 或 ConfigService.Reload 间接验证；现在它们成为公开接口，应单独测试。

        [Fact]
        public void PrepareBind_WhenCandidateIsNull_ReturnsFalseAndProducesErrorMessage()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);

            var success = entry.PrepareBind(null, out var plan, out var errorMessage);

            Assert.False(success);
            Assert.Null(plan);
            Assert.NotEmpty(errorMessage);
        }

        [Fact]
        public void PrepareBind_WhenDecodeFails_ReturnsFalseWithoutChangingActiveBinding()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);
            var badCandidate = new ConfigFileEntry { Key = "TestKey", Value = "not_an_int" };

            var success = entry.PrepareBind(badCandidate, out var plan, out var errorMessage);

            Assert.False(success);
            Assert.Null(plan);
            Assert.Contains("decode", errorMessage);
            // 预检失败不得修改活动绑定或运行时值。
            Assert.Same(model, entry.Entry);
            Assert.Equal(42, entry.Value);
        }

        [Fact]
        public void PrepareBind_WhenCandidateKeyDiffersFromCurrentEntryKey_ReturnsFalseWithoutChangingBinding()
        {
            // 重新绑定阶段要求候选项键与当前绑定键一致，防止跨配置项错误复用候选项。
            // 该校验只在已存在活动绑定时生效；首次构造阶段 Entry 为 null，候选项即待绑定项，不触发该校验。
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);
            var wrongKeyCandidate = new ConfigFileEntry { Key = "OtherKey", Value = "99" };

            var success = entry.PrepareBind(wrongKeyCandidate, out var plan, out var errorMessage);

            Assert.False(success);
            Assert.Null(plan);
            Assert.Contains("OtherKey", errorMessage);
            Assert.Contains("TestKey", errorMessage);
            // 键不匹配不得修改活动绑定或运行时值。
            Assert.Same(model, entry.Entry);
            Assert.Equal(42, entry.Value);
        }

        [Fact]
        public void PrepareBind_WhenCandidateTableKeyDiffersFromCurrentTableKey_ReturnsFalseWithoutChangingBinding()
        {
            // 候选项表键名必须与当前项一致；该校验先于键名校验，避免候选项被错误跨表复用。
            // 与键名校验不同，这里显式给两边设置不同的 TableKey，以触发该独立分支。
            var model = new ConfigFileEntry { TableKey = "General", Key = "TestKey", Value = "42" };
            var entry = new ConfigEntry<int>(model, 0, new Translator(), new Translator());
            var wrongTableCandidate = new ConfigFileEntry { TableKey = "Other", Key = "TestKey", Value = "99" };

            var success = entry.PrepareBind(wrongTableCandidate, out var plan, out var errorMessage);

            Assert.False(success);
            Assert.Null(plan);
            Assert.Contains("Other", errorMessage);
            Assert.Contains("General", errorMessage);
            // 表键不匹配不得修改活动绑定或运行时值。
            Assert.Same(model, entry.Entry);
            Assert.Equal(42, entry.Value);
        }

        [Fact]
        public void PrepareBind_WhenSuccessful_DoesNotChangeActiveBindingOrRaiseEvents()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var invocationCount = 0;
            entry.OnValueChanged += (_, _) => invocationCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };

            var success = entry.PrepareBind(candidate, out var plan, out _);

            Assert.True(success);
            Assert.NotNull(plan);
            Assert.True(plan.Changed);
            // 准备阶段只验证并构造计划；活动绑定和值必须保持不变，且不发布事件。
            Assert.Same(model, entry.Entry);
            Assert.Equal(1, entry.Value);
            Assert.Equal(0, invocationCount);
        }

        [Fact]
        public void PrepareBind_WhenCandidateValueEqualsCurrent_MarksPlanUnchangedAndKeepsOriginalText()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);
            // 文本内容相等的候选项；等值时计划不应改写候选项文本。
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "42" };

            var success = entry.PrepareBind(candidate, out var plan, out _);

            Assert.True(success);
            Assert.False(plan.Changed);
            Assert.Equal("42", candidate.Value);
        }

        [Fact]
        public void PrepareBind_CopiesRuntimeEntryMetadataToCandidate()
        {
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42", Name = name, Description = description };
            var entry = new ConfigEntry<int>(model, 0, name, description);
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };

            entry.PrepareBind(candidate, out _, out _);

            Assert.Same(name, candidate.Name);
            Assert.Same(description, candidate.Description);
        }

        [Fact]
        public void ApplyBind_WhenValueChanged_SwitchesBindingAndUpdatesValueWithoutPublishing()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var invocationCount = 0;
            entry.OnValueChanged += (_, _) => invocationCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };
            entry.PrepareBind(candidate, out var plan, out _);

            entry.ApplyBind(plan);

            Assert.Same(candidate, entry.Entry);
            Assert.Equal(99, entry.Value);
            Assert.Equal("99", candidate.Value);
            Assert.True(plan.Applied);
            // Apply 不负责发布事件；事件由 PublishBind 触发。
            Assert.Equal(0, invocationCount);
        }

        [Fact]
        public void PublishBind_AfterApply_RaisesTypedAndBaseEventsOnce()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var typedCount = 0;
            var baseCount = 0;
            entry.OnValueChanged += (_, _) => typedCount++;
            entry.OnValueChangedBase += (_, _) => baseCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };
            entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);

            entry.PublishBind(plan);

            Assert.Equal(1, typedCount);
            Assert.Equal(1, baseCount);
        }

        [Fact]
        public void PublishBind_WhenAppliedValueSupersededByLaterAssignment_DoesNotRaiseEvent()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var observedValues = new List<int>();
            entry.OnValueChanged += (_, value) => observedValues.Add(value);
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };
            entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);
            // 在发布前，其他流程（如事件处理器赋值）改写本项，使计划值过期。
            entry.Value = 7;

            entry.PublishBind(plan);

            // 仅普通赋值的 7 被发布；过期的重载候选 99 不应再次发布。
            Assert.Equal(new[] { 7 }, observedValues);
        }

        [Fact]
        public void PublishBind_WhenPlanNotChanged_DoesNotRaiseEvent()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);
            var invocationCount = 0;
            entry.OnValueChanged += (_, _) => invocationCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);

            entry.PublishBind(plan);

            Assert.Equal(0, invocationCount);
        }

        [Fact]
        public void RollbackBind_WhenNotApplied_RestoresNothingAndKeepsCandidateText()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };
            entry.PrepareBind(candidate, out var plan, out _);

            entry.RollbackBind(plan);

            Assert.Same(model, entry.Entry);
            Assert.Equal(1, entry.Value);
        }

        [Fact]
        public void RollbackBind_AfterApply_RestoresBindingValueAndCandidateText()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var invocationCount = 0;
            entry.OnValueChanged += (_, _) => invocationCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = " 99 " };
            entry.PrepareBind(candidate, out var plan, out _);
            // 记录候选文本被 Apply 规范化前的原始值，回滚后应恢复。
            var originalCandidateText = plan.OriginalCandidateValue;
            entry.ApplyBind(plan);

            entry.RollbackBind(plan);

            Assert.Same(model, entry.Entry);
            Assert.Equal(1, entry.Value);
            Assert.Equal(originalCandidateText, candidate.Value);
            Assert.False(plan.Applied);
            // 回滚不发布事件。
            Assert.Equal(0, invocationCount);
        }

        [Fact]
        public void ApplyBind_WhenPlanValueTypeMismatch_ThrowsArgumentException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "\"s\"" };
            var entry = CreateEntry("General", model, "s");
            // 构造一个 NewValue 类型与声明类型不一致的计划，模拟跨配置项错误复用。
            var wrongPlan = new EntryChangePlan(
                entry, model, "s", model, 0, "\"0\"", 0, changed: true);

            Assert.Throws<ArgumentException>(() => entry.ApplyBind(wrongPlan));
        }

        [Fact]
        public void ApplyBind_WithNullPlan_ThrowsArgumentNullException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);

            Assert.Throws<ArgumentNullException>(() => entry.ApplyBind(null));
        }

        [Fact]
        public void PublishBind_WithNullPlan_ThrowsArgumentNullException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);

            Assert.Throws<ArgumentNullException>(() => entry.PublishBind(null));
        }

        [Fact]
        public void RollbackBind_WithNullPlan_ThrowsArgumentNullException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", model, 0);

            Assert.Throws<ArgumentNullException>(() => entry.RollbackBind(null));
        }

        [Fact]
        public void RollbackBind_WhenPlanValueTypeMismatch_ThrowsArgumentException()
        {
            // 与 ApplyBind 的类型校验对称：OldValue 必须能还原为声明类型，否则回滚拒绝执行。
            var model = new ConfigFileEntry { Key = "TestKey", Value = "\"s\"" };
            var entry = CreateEntry("General", model, "s");
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "\"new\"" };
            entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);
            // 篡改计划的 OldValue 为不兼容类型，模拟跨配置项错误复用。
            var tamperedPlan = new EntryChangePlan(
                entry, model, 0, candidate, "new", "\"new\"", plan.OldChangeVersion, changed: true);

            Assert.Throws<ArgumentException>(() => entry.RollbackBind(tamperedPlan));
        }

        // ---- 等值候选项的事务提交：Changed=false 路径 ----
        // 候选项值与当前等价时，PrepareBind 标记 Changed=false；ApplyBind 只替换绑定引用，
        // 不写值、不递增版本；PublishBind 不发布事件。候选项原始文本（含多余空白）被保留。
        // 该路径原由 RebindEntry 的等值短路覆盖，重构后改由公开事务协议方法验证。
        [Fact]
        public void ApplyBind_WhenPlanUnchanged_SwapsBindingWithoutEventOrValueChange()
        {
            var initialModel = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = CreateEntry("General", initialModel, 0);
            var invocationCount = 0;
            entry.OnValueChanged += (_, _) => invocationCount++;
            // 候选项值等价但文本带空格，验证等值路径不会改写候选项文本。
            var newModel = new ConfigFileEntry { Key = "TestKey", Value = "  42  " };

            var prepared = entry.PrepareBind(newModel, out var plan, out _);
            entry.ApplyBind(plan);
            entry.PublishBind(plan);

            Assert.True(prepared);
            Assert.False(plan.Changed);
            Assert.Same(newModel, entry.Entry);
            Assert.Equal(42, entry.Value);
            // 等值时不发布事件。
            Assert.Equal(0, invocationCount);
            // 等值短路沿用候选项原始文本，不被规范化。
            Assert.Equal("  42  ", newModel.Value);
        }

        // ---- Value set 重入：处理器在通知期间再次赋值 ----
        // 这是重构后新增的 _pendingValueChanges 队列逻辑：重入赋值不会递归穿插进当前通知轮次，
        // 而是入队，由最外层赋值在当前轮结束后继续排空。因此每个入队值都会按顺序通知全部订阅者，
        // 订阅者不会在处理同一个值时被嵌套调用打乱。
        [Fact]
        public void Value_WhenHandlerReassigns_QueuesAndPublishesInOrderAfterCurrentRound()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = CreateEntry("General", model, 0);
            var observedByFirst = new List<int>();
            var observedBySecond = new List<int>();
            entry.OnValueChanged += (_, value) =>
            {
                observedByFirst.Add(value);
                // 第一个订阅者收到 10 时发起重入赋值；该赋值会把 20 入队，并在本轮（值 10）的两个订阅者都结束后再发布。
                if (value == 10)
                    entry.Value = 20;
            };
            entry.OnValueChanged += (_, value) => observedBySecond.Add(value);

            entry.Value = 10;

            // 值 10 先完整通知两个订阅者；随后队列中的 20 再完整通知两个订阅者，二者不被重入穿插。
            Assert.Equal(new[] { 10, 20 }, observedByFirst);
            Assert.Equal(new[] { 10, 20 }, observedBySecond);
            // 最终值与文件项同步到重入值。
            Assert.Equal(20, entry.Value);
            Assert.Equal("20", model.Value);
        }

        // 多级重入：每轮赋值都触发下一轮，队列依次排空，最终按顺序发布全部变化。
        [Fact]
        public void Value_WhenMultipleReentrantAssignments_PublishesEachInOrder()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "0" };
            var entry = CreateEntry("General", model, 0);
            var allValues = new List<int>();
            entry.OnValueChanged += (_, value) =>
            {
                allValues.Add(value);
                // 1->2->3，到 3 停止，避免无限递归。
                if (value < 3)
                    entry.Value = value + 1;
            };

            entry.Value = 1;

            Assert.Equal(new[] { 1, 2, 3 }, allValues);
            Assert.Equal(3, entry.Value);
        }

    }

    public class UnsupportedType
    {
    }

    public enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    public class EnumerableWithNullEnumerator : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return null;
        }
    }

    // 最小 IConfigEntryValue 实现，供 EqualBoxed 的适配器分支测试使用。
    // 等值以 Content 为准，其余方法仅提供满足接口的占位实现。
    public class StubConfigEntryValue : IConfigEntryValue
    {
        public string Content { get; }

        public StubConfigEntryValue(string content)
        {
            Content = content;
        }

        public ConfigFileResult<string> Encode() => ConfigFileResult<string>.Ok(Content);
        public ConfigFileResult<object> Decode(string content) => ConfigFileResult<object>.Ok(new StubConfigEntryValue(content));
        public ConfigFileResult<string> EncodeValueType() => ConfigFileResult<string>.Ok(nameof(StubConfigEntryValue));
        public bool Equals(IEntryValue other) => other is StubConfigEntryValue v && v.Content == Content;
    }
}
