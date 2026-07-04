using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigFileSheetModelTests
    {
        [Fact]
        public void AddTable_WithInvalidTableName_ReturnsFailureWithInvalidTableNameError()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            var invalidTableName = "";
            var table = new ConfigFileTable("validName", new Translator("中文", "English"));

            // Act
            var result = sheet.AddTable(invalidTableName, table);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Errors);
            Assert.Single(result.Errors);
            Assert.Equal(ConfigFileErrorCode.InvalidTableName, result.Errors[0].Code);
        }

        [Fact]
        public void AddTable_WithNullTable_ReturnsFailureWithTableNotFoundError()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            var tableKey = "validTable";
            ConfigFileTable table = null;

            // Act
            var result = sheet.AddTable(tableKey, table);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Errors);
            Assert.Single(result.Errors);
            Assert.Equal(ConfigFileErrorCode.TableNotFound, result.Errors[0].Code);
            Assert.Contains("Table cannot be null", result.Errors[0].Message);
        }

        [Fact]
        public void AddTable_WithValidTableAndKey_AddsTableSuccessfully()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            var tableKey = "validTable";
            var table = new ConfigFileTable(tableKey, new Translator("中文", "English"));

            // Act
            var result = sheet.AddTable(tableKey, table);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(table, result.Value);
            Assert.True(sheet.Sheet.Contains(tableKey));
        }

        [Fact]
        public void AddTable_WithDuplicateTableName_ReturnsFailureWithInvalidTableNameError()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            var tableKey = "duplicateTable";
            var table1 = new ConfigFileTable(tableKey, new Translator("中文1", "English1"));
            var table2 = new ConfigFileTable(tableKey, new Translator("中文2", "English2"));
            sheet.AddTable(tableKey, table1);

            // Act
            var result = sheet.AddTable(tableKey, table2);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Errors);
            Assert.Single(result.Errors);
            Assert.Equal(ConfigFileErrorCode.InvalidTableName, result.Errors[0].Code);
            Assert.Contains("Table name already exists", result.Errors[0].Message);
        }

        [Fact]
        public void GetEntry_WithNonExistentTable_ReturnsFailureWithTableNotFoundError()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            var tableKey = "nonExistentTable";
            var entryKey = "someKey";

            // Act
            var result = sheet.GetEntry(tableKey, entryKey);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Errors);
            Assert.Single(result.Errors);
            Assert.Equal(ConfigFileErrorCode.TableNotFound, result.Errors[0].Code);
            Assert.Contains($"Table not found: {tableKey}", result.Errors[0].Message);
        }

        [Fact]
        public void CreateTable_WithInvalidTableName_ReturnsFailure()
        {
            // Arrange
            var invalidTableName = "";
            var description = new Translator("中文", "English");

            // Act
            var result = ConfigFileSheet.CreateTable(invalidTableName, description);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Errors);
            Assert.True(result.Errors.Count > 0);
            Assert.Equal(ConfigFileErrorCode.InvalidTableName, result.Errors[0].Code);
        }

        [Fact]
        public void CreateTable_WithValidTableName_ReturnsSuccessWithTable()
        {
            // Arrange
            var tableName = "validTable";
            var description = new Translator("中文", "English");

            // Act
            var result = ConfigFileSheet.CreateTable(tableName, description);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Equal(tableName, result.Value.Key);
        }

        [Fact]
        public void EncodeSheet_WithEmptySheet_ReturnsEmptyString()
        {
            // Arrange
            var sheet = new ConfigFileSheet();

            // Act
            var result = sheet.EncodeSheet();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("", result.Value);
        }

        [Fact]
        public void EncodeSheet_WithSuccessfulTables_ReturnsEncodedContent()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            var table1 = new ConfigFileTable("table1", new Translator("表1", "Table1"));
            var table2 = new ConfigFileTable("table2", new Translator("表2", "Table2"));
            sheet.AddTable(table1);
            sheet.AddTable(table2);

            // Act
            var result = sheet.EncodeSheet();

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.Value);
        }

        [Fact]
        public void EncodeSheet_WithTableEncodingError_AddsErrorsToResult()
        {
            // Arrange
            var sheet = new ConfigFileSheet();
            
            // Create a table and then corrupt its Key to make EncodeTable fail
            var table = new ConfigFileTable("validTable", new Translator("表1", "Table1"));
            table.Key = "";  // Invalid key will cause EncodeTableHeader to fail

            sheet.Sheet.Add("validTable", table);

            // Act
            var result = sheet.EncodeSheet();

            // Assert
            // Note: Success will still be true because SetValue is called at the end
            // But errors should be present
            Assert.NotNull(result.Errors);
            Assert.True(result.Errors.Count > 0);
            Assert.Equal(ConfigFileErrorCode.InvalidTableName, result.Errors[0].Code);
        }

        [Fact]
        public void DecodeSheet_WithEmptyContent_ReturnsEmptySheet()
        {
            // Arrange
            var content = new string[0];
            var index = 0;

            // Act
            var result = ConfigFileSheet.DecodeSheet(content, ref index);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Sheet);
        }

    }
}
