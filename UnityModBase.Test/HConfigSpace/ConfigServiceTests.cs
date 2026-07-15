using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;
using System.Reflection;

namespace UnityModBase.Test.HConfigSpace
{
    public class ConfigServiceTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();
        private readonly List<string> _tempDirectories = new List<string>();

        private string CreateTempConfigPath()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cfg");
            _tempFiles.Add(path);
            return path;
        }

        private string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _tempDirectories.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var path in _tempFiles)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    var tmpPath = path + ".tmp";
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                }
                catch { }
            }

            foreach (var dir in _tempDirectories)
            {
                try
                {
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }
                catch { }
            }
        }

        [Fact]
        public void FileName_WhenFilePathSet_ReturnsFileName()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);

            var result = manager.FileName;

            Assert.Equal(Path.GetFileName(tempPath), result);
        }

        [Fact]
        public void Constructor_WhenFileDoesNotExist_CreatesEmptyFileSheet()
        {
            var tempPath = CreateTempConfigPath();

            var manager = new ConfigService(tempPath);

            Assert.NotNull(manager.FileSheet);
            Assert.NotNull(manager.Sheet);
            Assert.Equal(tempPath, manager.FilePath);
        }

        [Fact]
        public void Read_WhenFileHasDecodeErrors_ReturnsTrueAndKeepsDecodedSheet()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[InvalidTable\nKey = Value");

            var manager = new ConfigService(tempPath);
            var result = manager.Read();

            Assert.True(result);
            Assert.NotNull(manager.FileSheet);
        }

        [Fact]
        public void Read_WhenIOExceptionOccurs_LogsErrorAndReturnsFalse()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[Table]\nKey=Value");
            var manager = new ConfigService(tempPath);
            
            using (var stream = File.Open(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = manager.Read();
                Assert.False(result);
            }
        }

        [Fact]
        public void Write_WhenEncodeSucceeds_WritesFileAndReturnsTrue()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);

            var result = manager.Write();

            Assert.True(result);
            Assert.True(File.Exists(tempPath));
        }

        [Fact]
        public void Write_WhenEncodeHasErrors_LogsErrorsAndReturnsFalse()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);
            var table = new ConfigFileTable("Test", new Translator());
            manager.FileSheet.AddTable(table);
            table.Key = "Invalid-Table-Name!";

            var result = manager.Write();

            Assert.False(result);
        }

        [Fact]
        public void Write_WhenDirectoryDoesNotExist_CreatesDirectory()
        {
            var tempDir = CreateTempDirectory();
            var tempPath = Path.Combine(tempDir, "subdir", "test.cfg");
            _tempFiles.Add(tempPath);
            var manager = new ConfigService(CreateTempConfigPath());
            manager.FilePath = tempPath;

            var result = manager.Write();

            Assert.True(result);
            Assert.True(Directory.Exists(Path.GetDirectoryName(tempPath)));
            Assert.True(File.Exists(tempPath));
        }

        [Fact]
        public void Write_WhenIOExceptionOccurs_LogsErrorAndReturnsFalse()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);
            
            using (var stream = File.Open(tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var result = manager.Write();
                Assert.False(result);
            }
        }

        [Fact]
        public void Write_WhenReplacingExistingFile_CreatesBackupAndLeavesNoTemporaryFile()
        {
            // Arrange
            var tempDirectory = CreateTempDirectory();
            Directory.CreateDirectory(tempDirectory);
            var tempPath = Path.Combine(tempDirectory, "settings.cfg");
            var originalContent = "[Table]\nKey = \"old\"";
            File.WriteAllText(tempPath, originalContent);
            using var manager = new ConfigService(tempPath);
            var entryResult = manager.FileSheet.GetEntry("Table", "Key");
            Assert.True(entryResult.Success);
            entryResult.Value.Value = "\"new\"";

            // Act
            var result = manager.Write();

            // Assert
            Assert.True(result);
            Assert.Contains("Key = \"new\"", File.ReadAllText(tempPath));
            Assert.True(File.Exists(tempPath + ".bak"));
            Assert.Equal(originalContent, File.ReadAllText(tempPath + ".bak"));
            Assert.Empty(Directory.GetFiles(tempDirectory, "*.tmp"));
        }

        [Fact]
        public void CreateTable_WhenTableExistsInFileSheetButNotInSheet_AddsTableToSheet()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[ExistingTable]\n");
            var manager = new ConfigService(tempPath);
            var tableName = new Translator("表名", "TableName");
            var description = new Translator("描述", "Description");

            manager.CreateTable("ExistingTable", tableName, description);

            Assert.True(manager.Sheet.Contains("ExistingTable"));
            Assert.Equal(1, manager.Sheet.Count);
        }

        [Fact]
        public void CreateTable_WhenTableNameInvalid_ThrowsInvalidOperationException()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);
            var tableName = new Translator("表名", "TableName");
            var invalidTableKey = "Invalid-Table!";

            var exception = Assert.Throws<InvalidOperationException>(() => 
                manager.CreateTable(invalidTableKey, tableName));

            Assert.Contains("Failed to create config table", exception.Message);
            Assert.Contains(invalidTableKey, exception.Message);
        }

        [Fact]
        public void Bind_WhenEntryExists_ReturnsBoundConfigEntry()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 123\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var result = manager.Bind<int>("TestTable", "TestKey", 456, new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            Assert.NotNull(result);
            Assert.Equal("TestTable", result.TableName);
            Assert.Equal("TestKey", result.Key);
        }

        [Fact]
        public void Bind_WhenEntryDoesNotExist_CreatesNewEntry()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var result = manager.Bind<string>("TestTable", "NewKey", "DefaultValue", new Translator("新键", "NewKey"), new Translator("描述", "Description"));

            Assert.NotNull(result);
            Assert.Equal("TestTable", result.TableName);
            Assert.Equal("NewKey", result.Key);
        }

        [Fact]
        public void Bind_WhenTableDoesNotExist_ThrowsArgumentException()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.Bind<string>("NonExistentTable", "Key", "Value", new Translator("键", "Key"), new Translator("描述", "Description")));

            Assert.Contains("Config table not found", exception.Message);
            Assert.Contains("NonExistentTable", exception.Message);
            Assert.Equal("tableKey", exception.ParamName);
        }

        [Fact]
        public void Bind_WhenKeyNameInvalid_ThrowsArgumentException()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.Bind<string>("TestTable", "Invalid-Key!", "Value", new Translator("键", "Key"), new Translator("描述", "Description")));

            Assert.Contains("Invalid key name", exception.Message);
            Assert.Contains("TestTable.Invalid-Key!", exception.Message);
            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void Constructor_CallsReadMethod()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey=TestValue\n");

            var manager = new ConfigService(tempPath);

            Assert.NotNull(manager.FileSheet);
            Assert.NotNull(manager.Sheet);
            var tableResult = manager.FileSheet.GetTable("TestTable");
            Assert.True(tableResult.Success);
        }

        [Fact]
        public void Bind_WhenCreatingNewEntryWithComplexType_EncodesValue()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var result = manager.Bind<bool>("TestTable", "BoolKey", true, new Translator("布尔键", "BoolKey"), new Translator("描述", "Description"));

            Assert.NotNull(result);
            Assert.Equal("TestTable", result.TableName);
            Assert.Equal("BoolKey", result.Key);
        }

        [Fact]
        public void Bind_AddsValueChangedHandler()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var result = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            Assert.NotNull(result);
            Assert.True(manager.Sheet.Contains("TestTable"));
            Assert.Contains(result, manager.Sheet["TestTable"]);
        }

        [Fact]
        public void Reload_WhenCalledWithBoundEntries_UpdatesEntriesFromFile()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"OriginalValue\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"UpdatedValue\"\n");
            manager.Reload();

            Assert.Equal("UpdatedValue", entry.Value);
        }

        [Fact]
        public void Reload_WhenSaveOnConfigSetIsTrue_PreservesAndRestoresIt()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"Value\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            manager.SaveOnConfigSet = true;

            manager.Reload();

            Assert.True(manager.SaveOnConfigSet);
        }

        [Fact]
        public void Reload_WhenEntryDoesNotExistInFile_SkipsRebinding()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"Value\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            File.WriteAllText(tempPath, "[TestTable]\n");
            manager.Reload();

            Assert.Equal("Value", entry.Value);
        }

        [Fact]
        public void Reload_WhenStoredValueIsInvalid_RestoresFlagAndKeepsPreviousBindingAndValue()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 42\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<int>("TestTable", "TestKey", 0, new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            var originalEntry = entry.Entry;
            manager.SaveOnConfigSet = false;
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = invalid\n");

            // Act
            var exception = Record.Exception(manager.Reload);

            // Assert
            Assert.Null(exception);
            Assert.False(manager.SaveOnConfigSet);
            Assert.Equal(42, entry.Value);
            Assert.Same(originalEntry, entry.Entry);
            Assert.Equal("42", entry.Entry.Value);
        }

        [Fact]
        public void Bind_WhenDefaultValueTypeUnsupported_ThrowsInvalidOperationException()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                manager.Bind<object>("TestTable", "UnsupportedKey", new object(), new Translator("键", "Key"), new Translator("描述", "Description")));

            Assert.Contains("Failed to encode default value", exception.Message);
            Assert.Contains("TestTable.UnsupportedKey", exception.Message);
        }

        [Fact]
        public void CreateTable_WhenTableAlreadyExistsInSheet_DoesNotAddDuplicateTable()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[ExistingTable]\n");
            var manager = new ConfigService(tempPath);
            var tableName = new Translator("表名", "TableName");

            manager.CreateTable("ExistingTable", tableName);
            manager.CreateTable("ExistingTable", tableName);

            Assert.True(manager.Sheet.Contains("ExistingTable"));
            Assert.Equal(1, manager.Sheet.Count);
        }

        [Fact]
        public void Bind_WhenBoundEntryValueChangesAndSaveOnConfigSetIsTrue_WritesUpdatedFile()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"OriginalValue\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            entry.Value = "UpdatedValue";
            var content = File.ReadAllText(tempPath);

            Assert.Contains("TestKey = \"UpdatedValue\"", content);
        }

        [Fact]
        public void BoundEntry_WhenSaveOnConfigSetIsFalse_UpdatesMemoryWithoutWritingFile()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"OriginalValue\"\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            manager.SaveOnConfigSet = false;
            var originalFileContent = File.ReadAllText(tempPath);

            // Act
            entry.Value = "UpdatedValue";

            // Assert
            Assert.Equal("UpdatedValue", entry.Value);
            Assert.Equal("\"UpdatedValue\"", entry.Entry.Value);
            Assert.Equal(originalFileContent, File.ReadAllText(tempPath));
        }

        [Fact]
        public void Dispose_CalledTwice_UnsubscribesBoundEntriesAndClearsModels()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"Value\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            var handlerField = typeof(ConfigEntry<string>).GetField(
                nameof(ConfigEntry<string>.OnValueChangedBase),
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(handlerField);
            Assert.NotNull(handlerField.GetValue(entry));

            // Act
            manager.Dispose();
            var exception = Record.Exception(manager.Dispose);

            // Assert
            Assert.Null(exception);
            Assert.Null(manager.FileSheet);
            Assert.Null(manager.Sheet);
            Assert.Null(handlerField.GetValue(entry));
        }


    }
}
