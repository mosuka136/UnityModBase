using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigSpace
{
    // ConfigEntry<T1,T2> 是双元素元组配置项的门面，内部委托给值类型为 EntryValue<T1,T2> 的 ConfigEntry<T>。
    // 以下测试覆盖：构造与文件值解码、说明拼接、单元素读写（整体替换语义）、装箱视图、
    // 值变化事件转发，以及事务协议方法（PrepareBind/ApplyBind/PublishBind/RollbackBind）的转发正确性。
    public class ConfigEntryTupleTests
    {
        // 门面测试多数不关心展示文本，统一通过该工厂构造空翻译，避免每个用例重复。
        private static ConfigEntry<T1, T2> CreateEntry<T1, T2>(
            ConfigFileEntry entry, T1 defaultValue1, T2 defaultValue2)
        {
            return new ConfigEntry<T1, T2>(
                entry,
                defaultValue1,
                defaultValue2,
                new Translator(),
                new Translator(),
                new Translator(),
                new Translator());
        }

        // ---- 构造与绑定 ----

        [Fact]
        public void Constructor_DecodesFileValueOverDefaults()
        {
            // Arrange
            var model = new ConfigFileEntry { TableKey = "General", Key = "TestKey", Value = "1,2" };

            // Act
            var entry = new ConfigEntry<int, int>(
                model, 10, 20,
                new Translator("名称", "Name"),
                new Translator(),
                new Translator(),
                new Translator());

            // Assert
            Assert.Equal("General", entry.TableKey);
            Assert.Equal("TestKey", entry.Key);
            Assert.Equal("Name", entry.Name.English);
            // 文件中的 1,2 优先于声明的默认值 10,20。
            Assert.Equal(1, entry.Value1);
            Assert.Equal(2, entry.Value2);
            Assert.Equal(typeof(EntryValue<int, int>), entry.ValueType);
        }

        [Fact]
        public void Constructor_BoxedDefaultValueReturnsDeclaredDefaults()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 10, 20);
            IConfigEntry iEntry = entry;

            // Act
            var boxedDefault = iEntry.BoxedDefaultValue;

            // Assert
            var tupleDefault = Assert.IsType<EntryValue<int, int>>(boxedDefault);
            Assert.Equal(10, tupleDefault.Value1);
            Assert.Equal(20, tupleDefault.Value2);
        }

        [Fact]
        public void Constructor_ConcatenatesDescriptionAndValueDescriptions()
        {
            // 整体说明与两个分元素说明按语言拼接，并给分元素附上“值1/值2”与“Value1/Value2”标签，
            // 使文件注释与 UI 提示在未读取逐槽说明的场景下仍能区分各元素含义。
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };

            // Act
            var entry = new ConfigEntry<int, int>(
                model, 0, 0,
                new Translator(),
                new Translator("整体说明", "Overall"),
                new Translator("元素一", "First"),
                new Translator("元素二", "Second"));

            // Assert
            Assert.Equal(
                $"整体说明{Environment.NewLine}值1：元素一{Environment.NewLine}值2：元素二",
                entry.Description.Chinese);
            Assert.Equal(
                $"Overall{Environment.NewLine}Value1: First{Environment.NewLine}Value2: Second",
                entry.Description.English);
        }

        [Fact]
        public void Constructor_WhenValueDescriptionBlank_OmitsLabeledLinePerLanguage()
        {
            // 空白的分元素说明连同行标签一起按语言独立省略：第一个元素只有中文说明时，
            // 中文行带“值1”标签而英文行省略；完全空白的第二个元素两种语言都省略整行，
            // 文件注释不出现无文本的“值1：/Value1:”标签。
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };

            // Act
            var entry = new ConfigEntry<int, int>(
                model, 0, 0,
                new Translator(),
                new Translator("整体说明", "Overall"),
                new Translator("元素一", ""),
                new Translator());

            // Assert
            Assert.Equal(
                $"整体说明{Environment.NewLine}值1：元素一{Environment.NewLine}",
                entry.Description.Chinese);
            Assert.Equal(
                $"Overall{Environment.NewLine}{Environment.NewLine}",
                entry.Description.English);
            // 原始说明不经拼接原样保留在多元素视图中，槽位提示不受省略逻辑影响。
            Assert.Equal("元素一", ((IEntryMultiple)entry).ValueDescription[0].Chinese);
        }

        [Fact]
        public void MultipleView_ExposesCountAndPerValueDescriptions()
        {
            // IEntryMultiple 视图按原实例暴露基础说明与分元素说明，供 GUI 绑定按下标解析槽位提示。
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var baseDescription = new Translator("整体说明", "Overall");
            var valueDescription1 = new Translator("元素一", "First");
            var valueDescription2 = new Translator("元素二", "Second");

            // Act
            var entry = new ConfigEntry<int, int>(
                model, 0, 0,
                new Translator(),
                baseDescription,
                valueDescription1,
                valueDescription2);

            // Assert
            var multiple = Assert.IsAssignableFrom<IEntryMultiple>(entry);
            Assert.Equal(2, multiple.Count);
            Assert.Same(baseDescription, multiple.BaseDescription);
            Assert.Equal(2, multiple.ValueDescription.Length);
            Assert.Same(valueDescription1, multiple.ValueDescription[0]);
            Assert.Same(valueDescription2, multiple.ValueDescription[1]);
        }

        [Fact]
        public void Constructor_WhenEntryIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ConfigEntry<int, int>(
                null, 0, 0, new Translator(), new Translator(), new Translator(), new Translator()));
        }

        [Fact]
        public void Constructor_WhenNameIsNull_UsesKeyDerivedTranslator()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };

            // Act
            var entry = new ConfigEntry<int, int>(
                model, 0, 0, null, new Translator(), new Translator(), new Translator());

            // Assert
            Assert.Equal("TestKey", entry.Name.Chinese);
            Assert.Equal("TestKey", entry.Name.English);
        }

        [Fact]
        public void Constructor_WhenFileValueCannotDecode_ThrowsInvalidOperationException()
        {
            // 文件值无法按双元素解码时，内部 ConfigEntry 在首次绑定的 PrepareBind 阶段失败并抛异常。
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "abc" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => CreateEntry(model, 0, 0));
        }

        // ---- Value / Value1 / Value2 读写 ----

        [Fact]
        public void Value1_Get_ReturnsFirstElement()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);

            // Assert
            Assert.Equal(1, entry.Value1);
        }

        [Fact]
        public void Value2_Get_ReturnsSecondElement()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);

            // Assert
            Assert.Equal(2, entry.Value2);
        }

        [Fact]
        public void Value1_Set_WhenChanged_UpdatesValueAndFileEntryAndRaisesEvent()
        {
            // 单元素赋值在检测到变化时构造一个新实例整体替换 Value，保证编码与变化事件按整体值触发。
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var invokeCount = 0;
            EntryValue<int, int> observed = null;
            entry.OnValueChangedBase += (sender, args) =>
            {
                invokeCount++;
                observed = Assert.IsType<EntryValueChangedEventArgs<EntryValue<int, int>>>(args).Value;
            };

            // Act
            entry.Value1 = 5;

            // Assert
            Assert.Equal(1, invokeCount);
            // 第二个元素保持不变。
            Assert.Equal(5, entry.Value1);
            Assert.Equal(2, entry.Value2);
            Assert.Equal(5, observed.Value1);
            Assert.Equal(2, observed.Value2);
            // 文件项文本同步为新整体编码。
            Assert.Equal("5,2", model.Value);
        }

        [Fact]
        public void Value2_Set_WhenChanged_UpdatesValueAndFileEntryAndRaisesEvent()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var invokeCount = 0;
            entry.OnValueChangedBase += (sender, args) =>
            {
                invokeCount++;
                Assert.IsType<EntryValueChangedEventArgs<EntryValue<int, int>>>(args);
            };

            // Act
            entry.Value2 = 9;

            // Assert
            Assert.Equal(1, invokeCount);
            Assert.Equal(1, entry.Value1);
            Assert.Equal(9, entry.Value2);
            Assert.Equal("1,9", model.Value);
        }

        [Fact]
        public void Value1_Set_WhenUnchanged_DoesNotRaiseEvent()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var invokeCount = 0;
            entry.OnValueChangedBase += (sender, args) => invokeCount++;

            // Act
            entry.Value1 = 1;

            // Assert
            Assert.Equal(0, invokeCount);
            Assert.Equal(1, entry.Value1);
            Assert.Equal(2, entry.Value2);
        }

        [Fact]
        public void Value2_Set_WhenUnchanged_DoesNotRaiseEvent()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var invokeCount = 0;
            entry.OnValueChangedBase += (sender, args) => invokeCount++;

            // Act
            entry.Value2 = 2;

            // Assert
            Assert.Equal(0, invokeCount);
        }

        [Fact]
        public void Value_Set_UpdatesWholeTuple()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);

            // Act
            entry.Value = new EntryValue<int, int>(7, 8);

            // Assert
            Assert.Equal(7, entry.Value1);
            Assert.Equal(8, entry.Value2);
            Assert.Equal("7,8", model.Value);
        }

        // ---- 装箱视图 ----

        [Fact]
        public void BoxedValue_Get_ReturnsEntryValue()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            IConfigEntry iEntry = entry;

            // Act
            var boxed = iEntry.BoxedValue;

            // Assert
            var tuple = Assert.IsType<EntryValue<int, int>>(boxed);
            Assert.Equal(1, tuple.Value1);
            Assert.Equal(2, tuple.Value2);
        }

        [Fact]
        public void BoxedValue_Set_UpdatesValue()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            IConfigEntry iEntry = entry;

            // Act
            iEntry.BoxedValue = new EntryValue<int, int>(3, 4);

            // Assert
            Assert.Equal(3, entry.Value1);
            Assert.Equal(4, entry.Value2);
        }

        // ---- 事件订阅 ----

        [Fact]
        public void OnValueChangedBase_AddRemove_SubscribesAndUnsubscribes()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            IConfigEntry iEntry = entry;
            var invokeCount = 0;
            EventHandler handler = (sender, args) => invokeCount++;

            // Act & Assert
            iEntry.OnValueChangedBase += handler;
            entry.Value1 = 5;
            Assert.Equal(1, invokeCount);

            iEntry.OnValueChangedBase -= handler;
            entry.Value1 = 6;
            Assert.Equal(1, invokeCount);
        }

        // ---- 事务协议转发 ----

        [Fact]
        public void PrepareBind_WhenCandidateIsNull_ReturnsFalse()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);

            // Act
            var success = entry.PrepareBind(null, out var plan, out var errorMessage);

            // Assert
            Assert.False(success);
            Assert.Null(plan);
            Assert.NotEmpty(errorMessage);
        }

        [Fact]
        public void PrepareBind_ThenApply_SwitchesBindingWithoutPublishing()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var invokeCount = 0;
            entry.OnValueChangedBase += (sender, args) => invokeCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "5,6" };

            // Act
            var prepared = entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);

            // Assert
            Assert.True(prepared);
            Assert.True(plan.Applied);
            Assert.Same(candidate, entry.Entry);
            Assert.Equal(5, entry.Value1);
            Assert.Equal(6, entry.Value2);
            Assert.Equal("5,6", candidate.Value);
            // Apply 不发布事件；事件由 PublishBind 触发。
            Assert.Equal(0, invokeCount);
        }

        [Fact]
        public void PublishBind_AfterApply_RaisesEventOnce()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var invokeCount = 0;
            entry.OnValueChangedBase += (sender, args) => invokeCount++;
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "5,6" };
            entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);

            // Act
            entry.PublishBind(plan);

            // Assert
            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public void RollbackBind_AfterApply_RestoresValueAndBinding()
        {
            // Arrange
            var model = new ConfigFileEntry { Key = "TestKey", Value = "1,2" };
            var entry = CreateEntry(model, 0, 0);
            var candidate = new ConfigFileEntry { Key = "TestKey", Value = "5,6" };
            entry.PrepareBind(candidate, out var plan, out _);
            entry.ApplyBind(plan);

            // Act
            entry.RollbackBind(plan);

            // Assert
            Assert.Same(model, entry.Entry);
            Assert.Equal(1, entry.Value1);
            Assert.Equal(2, entry.Value2);
        }
    }
}
