using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;
using System.Collections;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigEntryTests
    {
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
            var entry = new ConfigEntry<string>("General", model, "default");

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
            var entry = new ConfigEntry<string>("General", model, "default");

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
            var entry = new ConfigEntry<int>("General", model, 0);

            // Act
            var valueType = entry.ValueType;

            // Assert
            Assert.Equal(typeof(int), valueType);
        }

        [Fact]
        public void RebindEntry_WhenEntryIsNull_ReturnsWithoutException()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = new ConfigEntry<int>("General", model, 0);

            // Act
            entry.RebindEntry(null);

            // Assert
            Assert.Same(model, entry.Entry);
            Assert.Equal(42, entry.Value);
            Assert.Equal("42", entry.Entry.Value);
        }

        [Fact]
        public void RebindEntry_WhenValidEntry_UpdatesEntryAndValue()
        {
            // Arrange
            var initialModel = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = new ConfigEntry<int>("General", initialModel, 0);
            
            var newModel = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "99"
            };

            // Act
            entry.RebindEntry(newModel);

            // Assert
            Assert.Same(newModel, entry.Entry);
            Assert.Equal(99, entry.Value);
        }

        [Fact]
        public void RebindEntry_WhenDecodeValueFails_ThrowsInvalidOperationException()
        {
            // Arrange
            var initialModel = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = new ConfigEntry<int>("General", initialModel, 0);
            
            var newModel = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "invalid_integer"
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => entry.RebindEntry(newModel));
        }

        [Fact]
        public void Value_WhenSetToNull_ThrowsInvalidOperationException()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "\"test\""
            };
            var entry = new ConfigEntry<string>("General", model, "default");

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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<string>("General", model, "defaultValue");
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
        public void Constructor_WhenParameterless_CreatesInstance()
        {
            // Arrange & Act
            var entry = new ConfigEntry<int>();

            // Assert
            Assert.Equal(typeof(int), entry.ValueType);
            Assert.Equal(default, entry.Value);
            Assert.Equal(default, entry.DefaultValue);
            Assert.Null(entry.TableKey);
            Assert.Null(entry.Entry);
        }

        [Fact]
        public void Constructor_WithThreeParameters_InitializesProperties()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42",
                Name = new Translator(chinese: "名称", english: "Name"),
                Description = new Translator(chinese: "描述", english: "Description")
            };

            // Act
            var entry = new ConfigEntry<int>("General", model, 10);

            // Assert
            Assert.Equal("General", entry.TableKey);
            Assert.Equal(10, entry.DefaultValue);
            Assert.Same(model, entry.Entry);
            Assert.Equal(42, entry.Value);
        }

        [Fact]
        public void Constructor_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ConfigEntry<int>("General", null, 0, name, description));
        }

        [Fact]
        public void Constructor_WhenInvalidTableName_ThrowsInvalidOperationException()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new ConfigEntry<int>("Invalid-Table-Name", model, 0, name, description));
            Assert.Contains("Invalid table name", exception.Message);
        }

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
            var exception = Assert.Throws<InvalidOperationException>(() => new ConfigEntry<UnsupportedType>("General", model, new UnsupportedType(), name, description));
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
            var exception = Assert.Throws<InvalidOperationException>(() => new ConfigEntry<string>("General", model, null, name, description));
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
            var entry = new ConfigEntry<TestEnum>("General", model, TestEnum.Value1, name, description);

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

        [Fact]
        public void OnValueChangedBase_WhenValueChanges_RaisesEventWithConvertedEventArgs()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);

            var success = entry.PrepareBind(null, out var plan, out var errorMessage);

            Assert.False(success);
            Assert.Null(plan);
            Assert.NotEmpty(errorMessage);
        }

        [Fact]
        public void PrepareBind_WhenDecodeFails_ReturnsFalseWithoutChangingActiveBinding()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = new ConfigEntry<int>("General", model, 0);
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
        public void PrepareBind_WhenSuccessful_DoesNotChangeActiveBindingOrRaiseEvents()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "99" };

            entry.PrepareBind(candidate, out _, out _);

            Assert.Same(name, candidate.Name);
            Assert.Same(description, candidate.Description);
        }

        [Fact]
        public void ApplyBind_WhenValueChanged_SwitchesBindingAndUpdatesValueWithoutPublishing()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1" };
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<int>("General", model, 0);
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
            var entry = new ConfigEntry<string>("General", model, "s");
            // 构造一个 NewValue 类型与声明类型不一致的计划，模拟跨配置项错误复用。
            var wrongPlan = new EntryChangePlan(
                entry, model, "s", model, 0, "\"0\"", 0, changed: true);

            Assert.Throws<ArgumentException>(() => entry.ApplyBind(wrongPlan));
        }

        [Fact]
        public void ApplyBind_WithNullPlan_ThrowsArgumentNullException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = new ConfigEntry<int>("General", model, 0);

            Assert.Throws<ArgumentNullException>(() => entry.ApplyBind(null));
        }

        [Fact]
        public void PublishBind_WithNullPlan_ThrowsArgumentNullException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = new ConfigEntry<int>("General", model, 0);

            Assert.Throws<ArgumentNullException>(() => entry.PublishBind(null));
        }

        [Fact]
        public void RollbackBind_WithNullPlan_ThrowsArgumentNullException()
        {
            var model = new ConfigFileEntry { Key = "TestKey", Value = "42" };
            var entry = new ConfigEntry<int>("General", model, 0);

            Assert.Throws<ArgumentNullException>(() => entry.RollbackBind(null));
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
}
