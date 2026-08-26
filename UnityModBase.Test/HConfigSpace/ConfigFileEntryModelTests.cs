using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigFileEntryModelTests
    {
        [Fact]
        public void EncodeValueType_WithSByte_ReturnsInt8()
        {
            // Arrange
            var type = typeof(sbyte);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int8", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithShort_ReturnsInt16()
        {
            // Arrange
            var type = typeof(short);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int16", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithByte_ReturnsUInt8()
        {
            // Arrange
            var type = typeof(byte);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("UInt8", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithUShort_ReturnsUInt16()
        {
            // Arrange
            var type = typeof(ushort);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("UInt16", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithUInt_ReturnsUInt32()
        {
            // Arrange
            var type = typeof(uint);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("UInt32", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithULong_ReturnsUInt64()
        {
            // Arrange
            var type = typeof(ulong);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("UInt64", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithDouble_ReturnsDouble()
        {
            // Arrange
            var type = typeof(double);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Double", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithIEnumerable_ReturnsArrayType()
        {
            // Arrange
            var type = typeof(List<int>);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int32[]", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithAdapterThatFails_ReturnsFailure()
        {
            // Arrange
            var type = typeof(TestAdapterWithFailure);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void EncodeValueType_WithAdapterThatThrows_ReturnsFailure()
        {
            // Arrange
            var type = typeof(TestAdapterThatThrows);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidType);
        }

        [Fact]
        public void DecodeKeyValuePair_WithInvalidFormat_ReturnsFailure()
        {
            // Arrange
            var content = "InvalidContentWithoutEquals";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidKeyValuePair);
        }

        [Fact]
        public void DecodeKeyValuePair_WithInvalidKeyName_ReturnsFailure()
        {
            // Arrange
            var content = "Invalid Key! = value";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidKeyName);
        }

        [Fact]
        public void DecodeKeyValuePair_WithEmptyValue_ReturnsFailure()
        {
            // Arrange
            var content = "validKey =   ";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidValue);
        }

        [Fact]
        public void EncodeAcceptableValues_WithEnumType_ReturnsEnumValues()
        {
            // Arrange & Act
            var result = ConfigFileEntry.EncodeAcceptableValues<TestEnum>();

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Value1", result.Value);
            Assert.Contains("Value2", result.Value);
            Assert.Contains("Value3", result.Value);
        }

        [Fact]
        public void DecodeValue_WithValidString_ReturnsDecodedValue()
        {
            // Arrange
            var value = "42";

            // Act
            var result = ConfigFileEntry.DecodeValue<int>(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void DecodeKeyValuePair_WithComment_ReturnsFailure()
        {
            // Arrange
            var content = "# key = value";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidKeyValuePair);
        }

        [Fact]
        public void DecodeKeyValuePair_WithValidKeyValuePair_ReturnsSuccess()
        {
            // Arrange
            var content = "key = value";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key", result.Value.Item1);
            Assert.Equal("value", result.Value.Item2);
        }

        [Fact]
        public void EncodeValueType_WithString_ReturnsString()
        {
            // Arrange
            var type = typeof(string);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("String", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithInt_ReturnsInt32()
        {
            // Arrange
            var type = typeof(int);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int32", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithLong_ReturnsInt64()
        {
            // Arrange
            var type = typeof(long);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int64", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithFloat_ReturnsFloat()
        {
            // Arrange
            var type = typeof(float);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Float", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithBool_ReturnsBoolean()
        {
            // Arrange
            var type = typeof(bool);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Boolean", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithEnum_ReturnsEnumType()
        {
            // Arrange
            var type = typeof(TestEnum);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Enum TestEnum", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithUnsupportedType_ReturnsFailure()
        {
            // Arrange
            var type = typeof(ConfigFileEntryModelTests);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.UnsupportedType);
        }

        [Fact]
        public void EncodeValueType_WithSuccessfulAdapter_ReturnsAdapterResult()
        {
            // Arrange
            var type = typeof(TestAdapterWithSuccess);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("CustomType", result.Value);
        }

        [Fact]
        public void DecodeKeyValuePair_WithWhitespaceAroundKeyValue_TrimsProperly()
        {
            // Arrange
            var content = "  key  =  value  ";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key", result.Value.Item1);
            Assert.Equal("value", result.Value.Item2);
        }

        [Fact]
        public void DecodeKeyValuePair_WithMultipleEquals_SplitsOnFirst()
        {
            // Arrange
            var content = "key = value = extra";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key", result.Value.Item1);
            Assert.Equal("value = extra", result.Value.Item2);
        }

        [Fact]
        public void EncodeValueType_WithArray_ReturnsArrayType()
        {
            // Arrange
            var type = typeof(int[]);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int32[]", result.Value);
        }

        [Fact]
        public void EncodeAcceptableValues_WithNonEnumType_ReturnsFailure()
        {
            // Arrange & Act
            var result = ConfigFileEntry.EncodeAcceptableValues<int>();

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidType);
        }

        [Fact]
        public void DecodeValue_WithInvalidValue_ReturnsFailure()
        {
            // Arrange
            var value = "not_a_number";

            // Act
            var result = ConfigFileEntry.DecodeValue<int>(value);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void DecodeValue_WithNullValue_ReturnsFailure()
        {
            // Arrange
            string value = null;

            // Act
            var result = ConfigFileEntry.DecodeValue<int>(value);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void DecodeValue_WithWhitespaceValue_ReturnsFailure()
        {
            // Arrange
            var value = "   ";

            // Act
            var result = ConfigFileEntry.DecodeValue<int>(value);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void DecodeKeyValuePair_WithEmptyString_ReturnsFailure()
        {
            // Arrange
            var content = "";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidKeyValuePair);
        }

        [Fact]
        public void DecodeKeyValuePair_WithOnlyEquals_ReturnsFailure()
        {
            // Arrange
            var content = "=";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void EncodeAcceptableValues_WithEnumInArray_ReturnsEnumValues()
        {
            // Arrange & Act
            var result = ConfigFileEntry.EncodeAcceptableValues<List<TestEnum>>();

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Value1", result.Value);
            Assert.Contains("Value2", result.Value);
            Assert.Contains("Value3", result.Value);
        }

        [Fact]
        public void EncodeValueType_WithEnumArray_ReturnsEnumArrayType()
        {
            // Arrange
            var type = typeof(TestEnum[]);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Enum TestEnum[]", result.Value);
        }

        [Fact]
        public void DecodeValue_WithValidBool_ReturnsDecodedValue()
        {
            // Arrange
            var value = "true";

            // Act
            var result = ConfigFileEntry.DecodeValue<bool>(value);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.Value);
        }

        [Fact]
        public void DecodeValue_WithValidString_ReturnsStringValue()
        {
            // Arrange
            var value = "\"test string\"";

            // Act
            var result = ConfigFileEntry.DecodeValue<string>(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("test string", result.Value);
        }

        [Fact]
        public void DecodeKeyValuePair_WithKeyContainingNumbers_ReturnsSuccess()
        {
            // Arrange
            var content = "key123 = value";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key123", result.Value.Item1);
            Assert.Equal("value", result.Value.Item2);
        }

        [Fact]
        public void DecodeKeyValuePair_WithKeyContainingUnderscore_ReturnsSuccess()
        {
            // Arrange
            var content = "key_test = value";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key_test", result.Value.Item1);
            Assert.Equal("value", result.Value.Item2);
        }

        [Fact]
        public void EncodeValueType_GenericMethod_WithInt_ReturnsInt32()
        {
            // Arrange & Act
            var result = ConfigFileModel.EncodeValueType<int>();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Int32", result.Value);
        }

        [Fact]
        public void DecodeValue_WithZero_ReturnsZero()
        {
            // Arrange
            var value = "0";

            // Act
            var result = ConfigFileEntry.DecodeValue<int>(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.Value);
        }

        [Fact]
        public void DecodeValue_WithNegativeNumber_ReturnsNegative()
        {
            // Arrange
            var value = "-42";

            // Act
            var result = ConfigFileEntry.DecodeValue<int>(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(-42, result.Value);
        }

        [Fact]
        public void EncodeValueType_WithStringList_ReturnsStringArrayType()
        {
            // Arrange
            var type = typeof(List<string>);

            // Act
            var result = ConfigFileModel.EncodeValueType(type);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("String[]", result.Value);
        }

        [Fact]
        public void DecodeKeyValuePair_WithSpacesInValue_PreservesSpaces()
        {
            // Arrange
            var content = "key = value with spaces";

            // Act
            var result = ConfigFileEntry.DecodeKeyValuePair(content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key", result.Value.Item1);
            Assert.Equal("value with spaces", result.Value.Item2);
        }

        [Fact]
        public void DecodeEntry_WithValidKeyValuePair_ReturnsEntry()
        {
            // Arrange
            var content = new[] { "key = value" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key", result.Value.Key);
            Assert.Equal("value", result.Value.Value);
        }

        [Fact]
        public void DecodeEntry_WithInvalidKeyInKeyValuePair_ReturnsFailure()
        {
            // Arrange
            var content = new[] { "Invalid Key! = value" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidKeyName);
        }

        [Fact]
        public void DecodeEntry_WithLineNotKeyValuePair_ReturnsFailure()
        {
            // Arrange
            var content = new[] { "SomeInvalidLineWithoutEquals" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidKeyValuePair);
        }

        [Fact]
        public void DecodeEntry_WithEmptyContent_ReturnsEndOfContent()
        {
            // Arrange
            var content = new string[0];
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.EndOfContent);
        }

        [Fact]
        public void DecodeEntry_WithOnlyComments_ReturnsEndOfContent()
        {
            // Arrange
            var content = new[] { "# Comment 1", "## Comment 2" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.EndOfContent);
        }

        [Fact]
        public void DecodeEntry_WithOnlyWhitespace_ReturnsEndOfContent()
        {
            // Arrange
            var content = new[] { "   ", "\t", "" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.EndOfContent);
        }

        [Fact]
        public void DecodeEntry_WithCommentsBeforeValidEntry_SkipsCommentsAndReturnsEntry()
        {
            // Arrange
            var content = new[] { "# Comment", "", "key = value" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key", result.Value.Key);
            Assert.Equal("value", result.Value.Value);
        }

        [Fact]
        public void DecodeEntry_WithStartingIndexBeyondContent_ReturnsEndOfContent()
        {
            // Arrange
            var content = new[] { "key = value" };
            var index = 5;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.EndOfContent);
        }

        [Fact]
        public void DecodeEntry_WithEmptyValueInKeyValuePair_ReturnsFailure()
        {
            // Arrange
            var content = new[] { "key = " };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Code == ConfigFileErrorCode.InvalidValue);
        }

        [Fact]
        public void DecodeEntry_UpdatesIndexAfterValidEntry()
        {
            // Arrange
            var content = new[] { "key = value", "next line" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, index);
        }

        [Fact]
        public void DecodeEntry_UpdatesIndexAfterInvalidLine()
        {
            // Arrange
            var content = new[] { "InvalidLine", "next line" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(1, index);
        }

        [Fact]
        public void DecodeEntry_WithMultipleValidEntries_ReturnsFirstEntry()
        {
            // Arrange
            var content = new[] { "key1 = value1", "key2 = value2" };
            var index = 0;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key1", result.Value.Key);
            Assert.Equal("value1", result.Value.Value);
        }

        [Fact]
        public void DecodeEntry_WithNonZeroStartIndex_ReturnsCorrectEntry()
        {
            // Arrange
            var content = new[] { "key1 = value1", "key2 = value2" };
            var index = 1;

            // Act
            var result = ConfigFileEntry.DecodeEntry(content, ref index);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("key2", result.Value.Key);
            Assert.Equal("value2", result.Value.Value);
        }


        [Fact]
        public void Key_WithInvalidValue_ThrowsArgumentException()
        {
            // Arrange
            var model = new ConfigFileEntry();

            // Act
            var exception = Assert.Throws<ArgumentException>(() => model.Key = "Invalid Key!");

            // Assert
            Assert.Contains("Invalid key name: Invalid Key!", exception.Message);
        }


        [Fact]
        public void Key_WithValidValue_SetsProperty()
        {
            // Arrange
            var model = new ConfigFileEntry();

            // Act
            model.Key = "Valid_Key_1";

            // Assert
            Assert.Equal("Valid_Key_1", model.Key);
        }

        // TableKey 是本次重构新增的文件项属性：解析阶段由 ConfigFileTable.DecodeTable 按所属表回填，
        // 运行时供 ConfigEntry.PrepareBind 校验候选项归属。其 setter 复用 ConfigFileModel.IsValidTableKey 校验，
        // 与 Key setter 的校验模式对称，这里补齐等价的赋值/拒绝用例。

        [Fact]
        public void TableKey_WithInvalidValue_ThrowsArgumentException()
        {
            // Arrange
            var model = new ConfigFileEntry();

            // Act
            var exception = Assert.Throws<ArgumentException>(() => model.TableKey = "Invalid Table!");

            // Assert
            Assert.Contains("Invalid table name: Invalid Table!", exception.Message);
        }

        [Fact]
        public void TableKey_WithValidValue_SetsProperty()
        {
            // Arrange
            var model = new ConfigFileEntry();

            // Act
            model.TableKey = "Valid_Table_1";

            // Assert
            Assert.Equal("Valid_Table_1", model.TableKey);
        }

        [Fact]
        public void EncodeName_WithEmptyTranslation_SkipsEmptyValue()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Name = new Translator(chinese: string.Empty, english: "DisplayName")
            };

            // Act
            var result = model.EncodeName();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("# Name: DisplayName", result.Value);
        }

        [Fact]
        public void EncodeKeyValuePair_WithMissingKey_ReturnsInvalidKeyNameFailure()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Value = "42"
            };

            // Act
            var result = model.EncodeKeyValuePair();

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Code == ConfigFileErrorCode.InvalidKeyName);
            Assert.Contains("Invalid key name", result.Errors[0].Message);
        }

        [Fact]
        public void EncodeEntry_WithValidMetadata_ReturnsCombinedContent()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Name = new Translator(chinese: "名称", english: "Name"),
                Description = new Translator(chinese: "描述", english: "Description"),
                Key = "TestKey",
                DefaultValue = "10",
                ValueType = "Int32",
                AcceptableValues = "1, 2, 3",
                Value = "42"
            };

            // Act
            var result = model.EncodeEntry();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("# Name: 名称, Name\n## 描述\n## Description\n# Value Type: Int32\n# Acceptable Values: 1, 2, 3\n# Default Value: 10\nTestKey = 42", result.Value.Replace("\r\n", "\n"));
        }

        [Fact]
        public void EncodeEntry_WithMissingKey_ReturnsInvalidKeyNameFailure()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Value = "42"
            };

            // Act
            var result = model.EncodeEntry();

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Code == ConfigFileErrorCode.InvalidKeyName);
        }

        [Fact]
        public void CopyTo_WithNullTarget_ReturnsFalse()
        {
            // Arrange
            var model = new ConfigFileEntry
            {
                Key = "TestKey",
                Value = "42"
            };

            // Act
            var result = model.CopyTo(null, overrideValue: true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CopyTo_WithOverrideValueTrue_CopiesAllPropertiesIncludingValue()
        {
            // Arrange
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");
            var source = new ConfigFileEntry
            {
                Name = name,
                Description = description,
                Key = "TestKey",
                DefaultValue = "10",
                ValueType = "Int32",
                AcceptableValues = "1, 2, 3",
                Value = "42"
            };
            var target = new ConfigFileEntry
            {
                Key = "OtherKey",
                Value = "100"
            };

            // Act
            var result = source.CopyTo(target, overrideValue: true);

            // Assert
            Assert.True(result);
            Assert.Same(name, target.Name);
            Assert.Same(description, target.Description);
            Assert.Equal("TestKey", target.Key);
            Assert.Equal("10", target.DefaultValue);
            Assert.Equal("Int32", target.ValueType);
            Assert.Equal("1, 2, 3", target.AcceptableValues);
            Assert.Equal("42", target.Value);
        }

        [Fact]
        public void CopyTo_WithOverrideValueFalse_CopiesMetadataButPreservesTargetValue()
        {
            // Arrange
            var name = new Translator(chinese: "名称", english: "Name");
            var description = new Translator(chinese: "描述", english: "Description");
            var source = new ConfigFileEntry
            {
                Name = name,
                Description = description,
                Key = "TestKey",
                DefaultValue = "10",
                ValueType = "Int32",
                AcceptableValues = "1, 2, 3",
                Value = "42"
            };
            var target = new ConfigFileEntry
            {
                Key = "OtherKey",
                Value = "100"
            };

            // Act
            var result = source.CopyTo(target, overrideValue: false);

            // Assert
            Assert.True(result);
            Assert.Same(name, target.Name);
            Assert.Same(description, target.Description);
            Assert.Equal("TestKey", target.Key);
            Assert.Equal("10", target.DefaultValue);
            Assert.Equal("Int32", target.ValueType);
            Assert.Equal("1, 2, 3", target.AcceptableValues);
            Assert.Equal("100", target.Value);
        }

        [Fact]
        public void EncodeValue_WithStringValue_ReturnsEncodedString()
        {
            // Arrange
            var value = "hello world";

            // Act
            var result = ConfigFileEntry.EncodeValue(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("\"hello world\"", result.Value);
        }


        [Fact]
        public void EncodeValue_WithNullString_ReturnsFailure()
        {
            // Arrange
            string value = null;

            // Act
            var result = ConfigFileEntry.EncodeValue(value);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Code == ConfigFileErrorCode.InvalidValue);
        }

        [Fact]
        public void EncodeValueType_GenericMethod_WithUnsupportedType_ReturnsFailure()
        {
            // Arrange & Act
            var result = ConfigFileModel.EncodeValueType<ConfigFileEntryModelTests>();

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Code == ConfigFileErrorCode.UnsupportedType);
        }

        [Fact]
        public void DecodeValue_WithEnumValue_ReturnsDecodedEnum()
        {
            // Arrange
            var value = "Value2";

            // Act
            var result = ConfigFileEntry.DecodeValue<TestEnum>(value);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(TestEnum.Value2, result.Value);
        }


        private class TestAdapterWithSuccess : IConfigEntryValue
        {
            public ConfigFileResult<string> Encode()
            {
                return "test";
            }

            public ConfigFileResult<object> Decode(string content)
            {
                return new object();
            }

            public ConfigFileResult<string> EncodeValueType()
            {
                return "CustomType";
            }

            public bool Equals(IConfigEntryValue other) => ReferenceEquals(this, other);
        }

        private enum TestEnum
        {
            Value1,
            Value2,
            Value3
        }

        private class TestAdapterWithFailure : IConfigEntryValue
        {
            public ConfigFileResult<string> Encode()
            {
                return "test";
            }

            public ConfigFileResult<object> Decode(string content)
            {
                return new object();
            }

            public ConfigFileResult<string> EncodeValueType()
            {
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Test failure"));
            }

            public bool Equals(IConfigEntryValue other) => ReferenceEquals(this, other);
        }


        private class TestAdapterThatThrows : IConfigEntryValue
        {
            public ConfigFileResult<string> Encode()
            {
                throw new NotImplementedException();
            }

            public ConfigFileResult<object> Decode(string content)
            {
                throw new NotImplementedException();
            }

            public ConfigFileResult<string> EncodeValueType()
            {
                throw new InvalidOperationException("Test exception");
            }

            public bool Equals(IConfigEntryValue other) => ReferenceEquals(this, other);
        }
    }
}
