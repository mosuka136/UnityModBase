using UnityModBase.HConfigSpace;
using UnityModBase.HTranslatorSpace;
using System.Reflection;
using UnityModBase.HEntrySpace;

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
        public void Constructor_WhenFileDoesNotExist_CreatesEmptyFileSheet()
        {
            var tempPath = CreateTempConfigPath();

            using var manager = new ConfigService(tempPath);

            Assert.Equal(tempPath, manager.FilePath);
            Assert.Empty(manager.FileSheet.Sheet);
            Assert.Empty(manager.Sheet);
        }

        [Fact]
        public void Constructor_WhenFilePathIsNull_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ConfigService(null));

            Assert.Equal("filePath", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("settings.cfg")]
        public void Constructor_WhenFilePathIsInvalid_ThrowsArgumentException(string filePath)
        {
            var exception = Assert.Throws<ArgumentException>(() => new ConfigService(filePath));

            Assert.Equal("value", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("settings.cfg")]
        public void FilePath_WhenInvalidValueAssigned_ThrowsAndKeepsPreviousPath(string invalidPath)
        {
            var originalPath = CreateTempConfigPath();
            using var manager = new ConfigService(originalPath);

            var exception = Assert.Throws<ArgumentException>(() => manager.FilePath = invalidPath);

            Assert.Equal("value", exception.ParamName);
            Assert.Equal(originalPath, manager.FilePath);
        }

        [Fact]
        public void CreateTableAndBind_WhenStructureChanges_RaisesEventsWithCommittedSnapshots()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            using var manager = new ConfigService(tempPath);
            var observedEntryCounts = new List<int>();
            manager.OnConfigChanged += () =>
                observedEntryCounts.Add(manager.Sheet["TestTable"].Entries.Count);

            // Act
            manager.CreateTable("TestTable", new Translator("测试表", "Test Table"));
            manager.Bind(
                "TestTable",
                "TestKey",
                42,
                new Translator("测试项", "Test Entry"),
                new Translator("描述", "Description"));

            // Assert
            Assert.Equal(new[] { 0, 1 }, observedEntryCounts);
        }

        [Fact]
        public void CreateTable_WhenConfigChangedHandlerThrows_InvokesRemainingHandlers()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            using var manager = new ConfigService(tempPath);
            var remainingHandlerCalled = false;
            manager.OnConfigChanged += () => throw new InvalidOperationException("handler failure");
            manager.OnConfigChanged += () =>
                remainingHandlerCalled = manager.Sheet.Contains("TestTable");

            // Act
            var exception = Record.Exception(() =>
                manager.CreateTable("TestTable", new Translator("测试表", "Test Table")));

            // Assert
            Assert.Null(exception);
            Assert.True(remainingHandlerCalled);
        }

        [Fact]
        public void Reload_WhenSuccessful_RaisesEventAfterUpdatedSnapshotIsCommitted()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 1\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "Test Table"));
            var entry = manager.Bind(
                "TestTable",
                "TestKey",
                0,
                new Translator("测试项", "Test Entry"),
                new Translator("描述", "Description"));
            var observedValues = new List<int>();
            manager.OnConfigChanged += () => observedValues.Add(entry.Value);
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 10\n");

            // Act
            var result = manager.Reload();

            // Assert
            Assert.True(result);
            Assert.Equal(new[] { 10 }, observedValues);
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
        public void Write_WhenSaveWriterIsInjected_WaitsForBackgroundWriteAndPersistsContent()
        {
            // Arrange：写服务以不会到期的长周期构造，磁盘写入只可能由 Write 的显式触发完成。
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            using (var writerScope = new SaveWriterScope())
            {
                // Act
                var result = manager.Write();

                // Assert：显式保存阻塞等待后台版本落盘后才返回，返回即代表内容已在磁盘上。
                Assert.True(result);
                Assert.Contains("TestKey = \"DefaultValue\"", File.ReadAllText(tempPath));
            }
        }

        [Fact]
        public void Write_WhenSaveWriterIsInjectedAndWriteFails_ReturnsFalseAndReportsThroughFailureQueue()
        {
            // Arrange：注入写服务后写入失败不再直接记日志，而是进入失败队列由主线程取走上报。
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            using var writerScope = new SaveWriterScope();
            using (File.Open(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                var result = manager.Write();

                // Assert
                Assert.False(result);
            }

            var failure = Assert.Single(DequeueAllFailures(writerScope.Worker, tempPath));
            Assert.Equal(tempPath, failure.filePath);
            Assert.NotNull(failure.exception);
        }

        [Fact]
        public void BoundEntry_WhenSaveWriterIsInjectedAndValueChanges_WritesDeferredUntilFlush()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"OriginalValue\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            using var writerScope = new SaveWriterScope();

            // Act：自动保存只编码入队，不等待落盘。
            entry.Value = "UpdatedValue";

            // Assert：入队后磁盘仍是原内容，调用线程不被磁盘 IO 阻塞。
            Assert.Equal("\"UpdatedValue\"", entry.Entry.Value);
            Assert.DoesNotContain("UpdatedValue", File.ReadAllText(tempPath));

            // 排队内容经一次写盘周期落盘后，磁盘与内存一致。
            Assert.True(writerScope.Worker.FlushAll(TimeSpan.FromSeconds(5)));
            Assert.Contains("TestKey = \"UpdatedValue\"", File.ReadAllText(tempPath));
        }

        [Fact]
        public void Reload_WhenSaveWriterHasPendingWrite_FlushesItBeforeReadingDisk()
        {
            // Arrange：自动保存内容仍在后台队列中，磁盘上还是旧值。
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 1\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<int>("TestTable", "TestKey", 0, new Translator("测试键", "TestKey"), new Translator("描述", "Description"));

            using var writerScope = new SaveWriterScope();
            entry.Value = 2;

            // Act
            var result = manager.Reload();

            // Assert：重载必须先排空该文件的待写内容再读盘，否则内存中的 2 会被磁盘旧值 1 覆盖。
            Assert.True(result);
            Assert.Equal(2, entry.Value);
            Assert.Contains("TestKey = 2", File.ReadAllText(tempPath));
        }

        [Fact]
        public void CreateTable_WhenTableExistsInFileSheetButNotInSheet_AddsTableToSheet()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[ExistingTable]\n");
            using var manager = new ConfigService(tempPath);
            var tableName = new Translator("表名", "TableName");
            var description = new Translator("描述", "Description");

            manager.CreateTable("ExistingTable", tableName, description);

            var runtimeTable = Assert.Single(manager.Sheet.Values);
            var fileTable = manager.FileSheet.GetTable("ExistingTable").Value;
            Assert.Equal("ExistingTable", runtimeTable.Key);
            Assert.Same(tableName, runtimeTable.Name);
            Assert.Same(description, runtimeTable.Description);
            Assert.Same(tableName, fileTable.Name);
            Assert.Same(description, fileTable.Description);
        }

        [Fact]
        public void CreateTable_WhenTableKeyInvalid_ThrowsArgumentException()
        {
            var tempPath = CreateTempConfigPath();
            var manager = new ConfigService(tempPath);
            var tableName = new Translator("表名", "TableName");
            var invalidTableKey = "Invalid-Table!";

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.CreateTable(invalidTableKey, tableName));

            Assert.Contains("Invalid table key", exception.Message);
            Assert.Contains(invalidTableKey, exception.Message);
            Assert.Equal("key", exception.ParamName);
            Assert.False(manager.FileSheet.GetTable(invalidTableKey).Success);
            Assert.False(manager.Sheet.Contains(invalidTableKey));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateTable_WhenTableKeyMissing_ThrowsArgumentException(string tableKey)
        {
            using var manager = new ConfigService(CreateTempConfigPath());

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.CreateTable(tableKey, new Translator("表名", "TableName")));

            Assert.Equal("key", exception.ParamName);
            Assert.Empty(manager.Sheet);
            Assert.Empty(manager.FileSheet.Sheet.Keys);
        }

        [Fact]
        public void CreateTable_WhenNameIsNull_UsesKeyDerivedName()
        {
            using var manager = new ConfigService(CreateTempConfigPath());

            manager.CreateTable("TestTable", null);

            Assert.True(manager.Sheet.Contains("TestTable"));
            Assert.True(manager.FileSheet.GetTable("TestTable").Success);
            Assert.Equal("TestTable", manager.Sheet["TestTable"].Name.Chinese);
            Assert.Equal("TestTable", manager.Sheet["TestTable"].Name.English);
        }

        [Fact]
        public void Bind_WhenEntryExists_UsesStoredValueAndDeclaredMetadata()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 123\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var name = new Translator("测试键", "TestKey");
            var description = new Translator("描述", "Description");

            var result = manager.Bind<int>("TestTable", "TestKey", 456, name, description);

            Assert.Equal("TestTable", result.TableKey);
            Assert.Equal("TestKey", result.Key);
            Assert.Equal(123, result.Value);
            Assert.Same(name, result.Name);
            Assert.Same(description, result.Description);
            Assert.Same(manager.FileSheet.GetEntry("TestTable", "TestKey").Value, result.Entry);
            Assert.Contains(result, manager.Sheet["TestTable"]);
        }

        [Fact]
        public void Bind_WhenEntryDoesNotExist_CreatesNewEntryFromDefault()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var result = manager.Bind<string>("TestTable", "NewKey", "DefaultValue", new Translator("新键", "NewKey"), new Translator("描述", "Description"));

            Assert.Equal("TestTable", result.TableKey);
            Assert.Equal("NewKey", result.Key);
            Assert.Equal("DefaultValue", result.Value);
            Assert.Equal("\"DefaultValue\"", result.Entry.Value);
            Assert.Same(manager.FileSheet.GetEntry("TestTable", "NewKey").Value, result.Entry);
            Assert.Contains(result, manager.Sheet["TestTable"]);
        }

        [Fact]
        public void BindTuple_WhenEntryExists_UsesStoredValuesAndRegistersFacade()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nRange = 10,20\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var name = new Translator("范围", "Range");
            var snapshots = new List<IConfigEntry>();
            manager.OnConfigChanged += () =>
                snapshots.Add(Assert.Single(manager.Sheet["TestTable"]));

            // Act
            var result = manager.Bind<int, int>(
                "TestTable",
                "Range",
                1,
                2,
                name,
                new Translator("取值范围", "Value range"),
                new Translator("下限", "Minimum"),
                new Translator("上限", "Maximum"));

            // Assert
            Assert.Equal(10, result.Value1);
            Assert.Equal(20, result.Value2);
            Assert.Same(name, result.Name);
            Assert.Same(manager.FileSheet.GetEntry("TestTable", "Range").Value, result.Entry);
            Assert.Contains(result, manager.Sheet["TestTable"]);
            Assert.Same(result, Assert.Single(snapshots));
        }

        [Fact]
        public void BindTuple_WhenEntryDoesNotExist_CreatesEncodedEntryFromDefaults()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            // Act
            var result = manager.Bind<int, string>(
                "TestTable",
                "Pair",
                7,
                "fallback",
                new Translator("组合", "Pair"),
                new Translator("组合值", "Pair value"),
                new Translator("编号", "Number"),
                new Translator("文本", "Text"));

            // Assert
            Assert.Equal(7, result.Value1);
            Assert.Equal("fallback", result.Value2);
            Assert.Equal("7,\"fallback\"", result.Entry.Value);
            Assert.Same(manager.FileSheet.GetEntry("TestTable", "Pair").Value, result.Entry);
            Assert.Contains(result, manager.Sheet["TestTable"]);
        }

        [Fact]
        public void Reload_WhenTupleEntryIsBound_UpdatesBothValues()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nPair = 1,\"old\"\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<int, string>(
                "TestTable",
                "Pair",
                0,
                "default",
                new Translator("组合", "Pair"),
                new Translator("组合值", "Pair value"),
                new Translator("编号", "Number"),
                new Translator("文本", "Text"));
            File.WriteAllText(tempPath, "[TestTable]\nPair = 9,\"new\"\n");

            // Act
            var result = manager.Reload();

            // Assert
            Assert.True(result);
            Assert.Equal(9, entry.Value1);
            Assert.Equal("new", entry.Value2);
            Assert.Same(manager.FileSheet.GetEntry("TestTable", "Pair").Value, entry.Entry);
        }

        [Fact]
        public void BindTuple_WhenValueChangesAndSaveOnConfigSetIsTrue_WritesBothValues()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nRange = 1,2\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<int, int>(
                "TestTable",
                "Range",
                0,
                0,
                new Translator("范围", "Range"),
                new Translator("取值范围", "Value range"),
                new Translator("下限", "Minimum"),
                new Translator("上限", "Maximum"));

            // Act
            entry.Value2 = 9;

            // Assert
            Assert.Equal(1, entry.Value1);
            Assert.Equal(9, entry.Value2);
            Assert.Contains("Range = 1,9", File.ReadAllText(tempPath));
        }

        [Fact]
        public void BindTuple_WhenDefaultElementTypeUnsupported_DoesNotAddEntry()
        {
            // Arrange
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() =>
                manager.Bind<object, int>(
                    "TestTable",
                    "UnsupportedPair",
                    new object(),
                    1,
                    new Translator("组合", "Pair"),
                    new Translator("组合值", "Pair value"),
                    new Translator("对象", "Object"),
                    new Translator("编号", "Number")));

            // Assert
            Assert.Contains("Failed to encode default value", exception.Message);
            Assert.False(manager.FileSheet.GetEntry("TestTable", "UnsupportedPair").Success);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BindTuple_WhenElementTypeIsNestedTuple_ThrowsWithoutAddingEntry(bool nestedTupleIsFirst)
        {
            using var manager = new ConfigService(CreateTempConfigPath());
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var key = nestedTupleIsFirst ? "NestedFirst" : "NestedSecond";

            var exception = nestedTupleIsFirst
                ? Assert.Throws<ArgumentException>(() =>
                    manager.Bind<EntryValue<int, int>, string>(
                        "TestTable",
                        key,
                        new EntryValue<int, int>(1, 2),
                        "value",
                        new Translator("组合", "Pair"),
                        new Translator("组合值", "Pair value"),
                        new Translator("嵌套", "Nested"),
                        new Translator("文本", "Text")))
                : Assert.Throws<ArgumentException>(() =>
                    manager.Bind<string, EntryValue<int, int>>(
                        "TestTable",
                        key,
                        "value",
                        new EntryValue<int, int>(1, 2),
                        new Translator("组合", "Pair"),
                        new Translator("组合值", "Pair value"),
                        new Translator("文本", "Text"),
                        new Translator("嵌套", "Nested")));

            Assert.Contains(typeof(EntryValue<int, int>).FullName, exception.Message);
            Assert.False(manager.FileSheet.GetEntry("TestTable", key).Success);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Fact]
        public void BindTuple_WhenStoredValueCannotDecode_DoesNotRegisterFacade()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nPair = invalid\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                manager.Bind<int, string>(
                    "TestTable",
                    "Pair",
                    1,
                    "default",
                    new Translator("组合", "Pair"),
                    new Translator("组合值", "Pair value"),
                    new Translator("编号", "Number"),
                    new Translator("文本", "Text")));

            Assert.Contains("decode", exception.Message);
            Assert.Equal("invalid", manager.FileSheet.GetEntry("TestTable", "Pair").Value.Value);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Fact]
        public void Bind_WhenTableDoesNotExist_ThrowsArgumentException()
        {
            var tempPath = CreateTempConfigPath();
            using var manager = new ConfigService(tempPath);

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.Bind<string>("NonExistentTable", "Key", "Value", new Translator("键", "Key"), new Translator("描述", "Description")));

            Assert.Contains("Config table not found", exception.Message);
            Assert.Contains("NonExistentTable", exception.Message);
            Assert.Equal("tableKey", exception.ParamName);
            Assert.Empty(manager.FileSheet.Sheet);
            Assert.Empty(manager.Sheet);
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

            Assert.Contains("Invalid entry key", exception.Message);
            Assert.Contains("TestTable.Invalid-Key!", exception.Message);
            Assert.Equal("key", exception.ParamName);
            Assert.False(manager.FileSheet.GetEntry("TestTable", "Invalid-Key!").Success);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Fact]
        public void Bind_WhenKeyNameInvalidOnMissingTable_RejectsKeyBeforeTableLookup()
        {
            // Arrange：键名格式校验先于任何文件查询执行，非法键应先于“表不存在”诊断被拒绝，
            // 用于锁定 GetOrCreateConfigFileEntry 中校验顺序不被回退到“先查表后验键”。
            using var manager = new ConfigService(CreateTempConfigPath());

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.Bind<string>("NonExistentTable", "Invalid-Key!", "Value", new Translator("键", "Key"), new Translator("描述", "Description")));

            Assert.Contains("Invalid entry key", exception.Message);
            Assert.Contains("NonExistentTable.Invalid-Key!", exception.Message);
            Assert.Equal("key", exception.ParamName);
        }

        [Theory]
        [InlineData("tableKey")]
        [InlineData("key")]
        public void Bind_WhenKeyViolatesSharedKeyRules_ThrowsWithoutAddingEntry(string argumentName)
        {
            using var manager = new ConfigService(CreateTempConfigPath());
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var tableKey = argumentName == "tableKey" ? " " : "TestTable";
            var key = argumentName == "key" ? " " : "Candidate";

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.Bind(tableKey, key, 1, new Translator("候选项", "Candidate"), new Translator("描述", "Description")));

            Assert.Equal(argumentName, exception.ParamName);
            Assert.False(manager.FileSheet.GetEntry("TestTable", "Candidate").Success);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Fact]
        public void Bind_WhenMetadataOmitted_UsesKeyDerivedNameAndEmptyDescription()
        {
            using var manager = new ConfigService(CreateTempConfigPath());
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var entry = manager.Bind<int>("TestTable", "Candidate", 1, null, null);

            Assert.Equal("Candidate", entry.Name.Chinese);
            Assert.Equal("Candidate", entry.Name.English);
            Assert.Equal(string.Empty, entry.Description.Chinese);
            Assert.Equal(string.Empty, entry.Description.English);
        }

        [Theory]
        [InlineData("tableKey")]
        [InlineData("key")]
        public void BindTuple_WhenKeyViolatesSharedKeyRules_ThrowsWithoutAddingEntry(string argumentName)
        {
            using var manager = new ConfigService(CreateTempConfigPath());
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var tableKey = argumentName == "tableKey" ? " " : "TestTable";
            var key = argumentName == "key" ? " " : "Candidate";

            var exception = Assert.Throws<ArgumentException>(() =>
                manager.Bind(
                    tableKey,
                    key,
                    1,
                    "default",
                    new Translator("组合", "Pair"),
                    new Translator("组合值", "Pair value"),
                    new Translator("编号", "Number"),
                    new Translator("文本", "Text")));

            Assert.Equal(argumentName, exception.ParamName);
            Assert.False(manager.FileSheet.GetEntry("TestTable", "Candidate").Success);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Fact]
        public void BindTuple_WhenMetadataOmitted_UsesKeyDerivedNameAndBlankDescriptions()
        {
            using var manager = new ConfigService(CreateTempConfigPath());
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));

            var entry = manager.Bind<int, string>("TestTable", "Candidate", 1, "default", null);

            Assert.Equal("Candidate", entry.Name.Chinese);
            Assert.Equal("Candidate", entry.Name.English);
            // 省略元数据时总体说明与分元素说明均为空；分元素行连同“值1/值2”标签一起按语言省略，
            // 组合说明只剩空的总说明行与两个空行，文件注释不出现无文本的结构标签。
            Assert.Equal($"{Environment.NewLine}{Environment.NewLine}", entry.Description.Chinese);
            Assert.Equal($"{Environment.NewLine}{Environment.NewLine}", entry.Description.English);
        }

        [Fact]
        public void Constructor_WhenFileExists_LoadsFileEntriesWithoutRuntimeBindings()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey=TestValue\n");

            using var manager = new ConfigService(tempPath);

            var entryResult = manager.FileSheet.GetEntry("TestTable", "TestKey");
            Assert.True(entryResult.Success);
            Assert.Equal("TestValue", entryResult.Value.Value);
            Assert.Empty(manager.Sheet);
        }

        [Fact]
        public void Reload_WhenSuccessful_PreservesEntryMetadataAndReplacesFileSheet()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 1\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "Test Table"));
            var entryName = new Translator("测试键", "TestKey");
            var entryDescription = new Translator("键说明", "Key Description");
            var entry = manager.Bind<int>("TestTable", "TestKey", 0, entryName, entryDescription);
            var originalFileSheet = manager.FileSheet;
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 2\n");

            var result = manager.Reload();

            Assert.True(result);
            Assert.Equal(2, entry.Value);
            Assert.NotSame(originalFileSheet, manager.FileSheet);
            // 重载后候选项的条目级元数据由 PrepareBind 的 CopyTo 从运行时声明同步，应保留。
            Assert.Same(entryName, entry.Entry.Name);
            Assert.Same(entryDescription, entry.Entry.Description);
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

            var result = manager.Reload();

            Assert.True(result);
            Assert.True(manager.SaveOnConfigSet);
        }

        [Fact]
        public void Reload_WhenEntryDoesNotExistInFile_ReturnsFalseAndKeepsPreviousBindingAndValue()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = \"Value\"\n");
            var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<string>("TestTable", "TestKey", "DefaultValue", new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            var originalEntry = entry.Entry;

            File.WriteAllText(tempPath, "[TestTable]\n");
            var result = manager.Reload();

            Assert.False(result);
            Assert.Equal("Value", entry.Value);
            Assert.Same(originalEntry, entry.Entry);
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
            bool? reloadResult = null;
            var exception = Record.Exception(() =>
            {
                reloadResult = manager.Reload();
            });

            // Assert
            Assert.Null(exception);
            Assert.False(reloadResult);
            Assert.False(manager.SaveOnConfigSet);
            Assert.Equal(42, entry.Value);
            Assert.Same(originalEntry, entry.Entry);
            Assert.Equal("42", entry.Entry.Value);
        }

        [Fact]
        public void Reload_WhenReadFails_ReturnsFalseAndKeepsPreviousBindingAndValue()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 42\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<int>("TestTable", "TestKey", 0, new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            var originalEntry = entry.Entry;

            bool result;
            using (File.Open(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                result = manager.Reload();

            Assert.False(result);
            Assert.Equal(42, entry.Value);
            Assert.Same(originalEntry, entry.Entry);
        }

        [Fact]
        public void Reload_WhenMultipleEntriesChange_PublishesEventsAfterAllEntriesAreCommitted()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nFirstKey = 1\nSecondKey = 2\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var firstEntry = manager.Bind<int>("TestTable", "FirstKey", 0, new Translator("第一项", "First"), new Translator("描述", "Description"));
            var secondEntry = manager.Bind<int>("TestTable", "SecondKey", 0, new Translator("第二项", "Second"), new Translator("描述", "Description"));
            var invocationCount = 0;
            var secondValueObservedByFirstHandler = 0;
            firstEntry.OnValueChanged += (_, _) =>
            {
                invocationCount++;
                secondValueObservedByFirstHandler = secondEntry.Value;
            };
            File.WriteAllText(tempPath, "[TestTable]\nFirstKey = 10\nSecondKey = 20\n");

            var result = manager.Reload();

            Assert.True(result);
            Assert.Equal(10, firstEntry.Value);
            Assert.Equal(20, secondEntry.Value);
            Assert.Equal(1, invocationCount);
            Assert.Equal(20, secondValueObservedByFirstHandler);
        }

        [Fact]
        public void Reload_WhenEarlierHandlerChangesLaterEntry_DoesNotPublishSupersededReloadEvent()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nFirstKey = 1\nSecondKey = 2\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var firstEntry = manager.Bind<int>("TestTable", "FirstKey", 0, new Translator("第一项", "First"), new Translator("描述", "Description"));
            var secondEntry = manager.Bind<int>("TestTable", "SecondKey", 0, new Translator("第二项", "Second"), new Translator("描述", "Description"));
            var secondEventValues = new List<int>();
            firstEntry.OnValueChanged += (_, _) => secondEntry.Value = 30;
            secondEntry.OnValueChanged += (_, value) => secondEventValues.Add(value);
            File.WriteAllText(tempPath, "[TestTable]\nFirstKey = 10\nSecondKey = 20\n");

            var result = manager.Reload();

            Assert.True(result);
            Assert.Equal(10, firstEntry.Value);
            Assert.Equal(30, secondEntry.Value);
            Assert.Equal(new[] { 30 }, secondEventValues);
            Assert.Contains("SecondKey = 30", File.ReadAllText(tempPath));
        }

        [Fact]
        public void Reload_WhenHandlerChangesSameEntry_PublishesOrderedValueSnapshots()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 1\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind<int>("TestTable", "TestKey", 0, new Translator("测试键", "TestKey"), new Translator("描述", "Description"));
            var eventValues = new List<int>();
            entry.OnValueChanged += (_, value) =>
            {
                if (value == 10)
                    entry.Value = 11;
            };
            entry.OnValueChanged += (_, value) => eventValues.Add(value);
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = 10\n");

            var result = manager.Reload();

            Assert.True(result);
            Assert.Equal(11, entry.Value);
            Assert.Equal(new[] { 10, 11 }, eventValues);
            Assert.Contains("TestKey = 11", File.ReadAllText(tempPath));
        }

        [Fact]
        public void Reload_WithAdapterValue_DecodesAndEncodesCandidateOnlyOnce()
        {
            ReloadProbeAdapter.Reset();
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = original\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind(
                "TestTable",
                "TestKey",
                new ReloadProbeAdapter("default"),
                new Translator("测试键", "TestKey"),
                new Translator("描述", "Description"));
            ReloadProbeAdapter.Reset();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = updated\n");

            var result = manager.Reload();

            Assert.True(result);
            Assert.Equal("updated", entry.Value.Content);
            Assert.Equal(1, ReloadProbeAdapter.DecodeCount);
            Assert.Equal(1, ReloadProbeAdapter.EncodeCount);
        }

        [Fact]
        public void Reload_WhenPreparedAdapterValueCannotEncode_KeepsPreviousFileModelBindingAndValue()
        {
            ReloadProbeAdapter.Reset();
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = original\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var entry = manager.Bind(
                "TestTable",
                "TestKey",
                new ReloadProbeAdapter("default"),
                new Translator("测试键", "TestKey"),
                new Translator("描述", "Description"));
            var originalFileSheet = manager.FileSheet;
            var originalEntry = entry.Entry;
            var originalValue = entry.Value;
            var invocationCount = 0;
            entry.OnValueChanged += (_, _) => invocationCount++;
            ReloadProbeAdapter.Reset();
            ReloadProbeAdapter.FailEncode = true;
            File.WriteAllText(tempPath, "[TestTable]\nTestKey = updated\n");

            try
            {
                var result = manager.Reload();

                Assert.False(result);
                Assert.Same(originalFileSheet, manager.FileSheet);
                Assert.Same(originalEntry, entry.Entry);
                Assert.Same(originalValue, entry.Value);
                Assert.Equal("original", entry.Value.Content);
                Assert.Equal(1, ReloadProbeAdapter.DecodeCount);
                Assert.Equal(1, ReloadProbeAdapter.EncodeCount);
                Assert.Equal(0, invocationCount);
            }
            finally
            {
                ReloadProbeAdapter.Reset();
            }
        }

        [Fact]
        public void Reload_WhenLaterCommitThrows_RollsBackEarlierEntriesWithoutPublishingEvents()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\nFirstKey = 1\nSecondKey = 2\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var firstEntry = manager.Bind<int>("TestTable", "FirstKey", 0, new Translator("第一项", "First"), new Translator("描述", "Description"));
            manager.Sheet["TestTable"].Add(new ThrowingReloadEntry("TestTable", "SecondKey"));
            var originalFileSheet = manager.FileSheet;
            var originalEntry = firstEntry.Entry;
            var invocationCount = 0;
            firstEntry.OnValueChanged += (_, _) => invocationCount++;
            File.WriteAllText(tempPath, "[TestTable]\nFirstKey = 10\nSecondKey = 20\n");

            var result = manager.Reload();

            Assert.False(result);
            Assert.Same(originalFileSheet, manager.FileSheet);
            Assert.Same(originalEntry, firstEntry.Entry);
            Assert.Equal(1, firstEntry.Value);
            Assert.Equal("1", firstEntry.Entry.Value);
            Assert.Equal(0, invocationCount);
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
            Assert.False(manager.FileSheet.GetEntry("TestTable", "UnsupportedKey").Success);
            Assert.Empty(manager.Sheet["TestTable"]);
        }

        [Fact]
        public void CreateTable_WhenTableAlreadyExistsInSheet_ThrowsInvalidOperationExceptionAndKeepsOriginalTable()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[ExistingTable]\n");
            using var manager = new ConfigService(tempPath);
            var originalName = new Translator("原表名", "Original Name");
            var originalDescription = new Translator("原描述", "Original Description");
            manager.CreateTable("ExistingTable", originalName, originalDescription);
            var originalTable = manager.Sheet["ExistingTable"];

            var exception = Assert.Throws<InvalidOperationException>(() =>
                manager.CreateTable("ExistingTable", new Translator("新表名", "Replacement Name"), new Translator("新描述", "Replacement Description")));

            Assert.Contains("ExistingTable", exception.Message);
            Assert.Same(originalTable, Assert.Single(manager.Sheet.Values));
            Assert.Same(originalName, originalTable.Name);
            Assert.Same(originalDescription, originalTable.Description);
        }

        [Fact]
        public void Bind_WhenEntryAlreadyBoundToSameTableAndKey_ThrowsInvalidOperationException()
        {
            var tempPath = CreateTempConfigPath();
            File.WriteAllText(tempPath, "[TestTable]\n");
            using var manager = new ConfigService(tempPath);
            manager.CreateTable("TestTable", new Translator("测试表", "TestTable"));
            var first = manager.Bind<string>("TestTable", "TestKey", "First", new Translator("键", "Key"), new Translator("描述", "Description"));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                manager.Bind<string>("TestTable", "TestKey", "Second", new Translator("键", "Key"), new Translator("描述", "Description")));

            Assert.Contains("TestTable.TestKey", exception.Message);
            Assert.Same(first, Assert.Single(manager.Sheet["TestTable"].Entries));
            Assert.Equal("First", first.Value);
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

        // 注入共享后台写服务的隔离夹具：以不会到期的长周期构造写服务并挂到静态入口，
        // 使配置写入进入后台队列；释放时先摘除静态引用再释放写服务，不影响其余测试的同步写入路径。
        private sealed class SaveWriterScope : IDisposable
        {
            public ConfigSaveWorker Worker { get; }

            public SaveWriterScope()
            {
                Worker = new ConfigSaveWorker(TimeSpan.FromSeconds(100));
                ConfigService.SaveWriter = Worker;
            }

            public void Dispose()
            {
                ConfigService.SaveWriter = null;
                Worker.Dispose();
            }
        }

        // 取出失败队列中目标路径的全部失败报告，供断言失败不重复、不遗漏。
        private static List<(string filePath, Exception exception)> DequeueAllFailures(ConfigSaveWorker worker, string filePath)
        {
            var failures = new List<(string, Exception)>();
            while (worker.TryDequeueFailure(out var failurePath, out var failureException))
            {
                if (failurePath == filePath)
                    failures.Add((failurePath, failureException));
            }

            return failures;
        }

        // 记录重载期间的编解码次数，并可注入编码失败，用于验证预检不会重复转换或泄漏部分状态。
        private sealed class ReloadProbeAdapter : IConfigEntryValue
        {
            public static int DecodeCount { get; private set; }
            public static int EncodeCount { get; private set; }
            public static bool FailEncode { get; set; }

            public string Content { get; private set; }

            public ReloadProbeAdapter()
                : this(string.Empty)
            {
            }

            public ReloadProbeAdapter(string content)
            {
                Content = content;
            }

            public ConfigFileResult<string> Encode()
            {
                EncodeCount++;
                if (FailEncode)
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "encode failed"));

                return ConfigFileResult<string>.Ok(Content);
            }

            public ConfigFileResult<object> Decode(string content)
            {
                DecodeCount++;
                return ConfigFileResult<object>.Ok(new ReloadProbeAdapter(content));
            }

            public ConfigFileResult<string> EncodeValueType()
            {
                return ConfigFileResult<string>.Ok(nameof(ReloadProbeAdapter));
            }

            // 重载探测适配器的等值以 Content 为准：PrepareBind 通过 EqualBoxed 判断候选值是否变化，
            // 因此本实现必须把相同内容视为相等、不同内容视为不等，验证计数才成立。
            public bool Equals(IEntryValue other)
            {
                return other is ReloadProbeAdapter adapter && adapter.Content == Content;
            }

            public static void Reset()
            {
                DecodeCount = 0;
                EncodeCount = 0;
                FailEncode = false;
            }
        }

        // 在提交阶段抛出异常的配置项替身，用于覆盖“前序计划已应用、后序计划失败”的逆序回滚路径。
        // 新协议下配置项直接实现 PrepareBind/ApplyBind/RollbackBind/PublishBind，
        // 因此替身在 PrepareBind 返回一个指向自身、在 ApplyBind 抛出异常的计划。
        private sealed class ThrowingReloadEntry : IConfigEntry
        {
            public Translator Name { get; } = new Translator("抛出异常的配置项", "Throwing Entry");
            public Translator Description { get; } = new Translator("测试提交回滚", "Tests commit rollback");
            public string TableKey { get; }
            public string Key { get; }
            public ConfigFileEntry Entry { get; }
            public Type ValueType => typeof(int);
            public object BoxedValue { get; set; }
            public object BoxedDefaultValue => 0;

            public event EventHandler OnValueChangedBase
            {
                add { }
                remove { }
            }

            public ThrowingReloadEntry(string tableKey, string key)
            {
                TableKey = tableKey;
                Key = key;
                Entry = new ConfigFileEntry
                {
                    Key = key,
                    Value = "2"
                };
                BoxedValue = 2;
            }

            public bool PrepareBind(ConfigFileEntry candidate, out EntryChangePlan plan, out string errorMessage)
            {
                // changed 传 false：本替身的 ApplyBind 必定抛出，不需要构造可提交的值变化。
                plan = new EntryChangePlan(this, Entry, BoxedValue, candidate, BoxedValue, candidate.Value, 0, changed: false);
                errorMessage = string.Empty;
                return true;
            }

            public void ApplyBind(EntryChangePlan plan)
            {
                throw new InvalidOperationException("commit failed");
            }

            public void RollbackBind(EntryChangePlan plan)
            {
                // Apply 在写入任何测试状态前即抛出，因此没有需要恢复的局部状态。
            }

            public void PublishBind(EntryChangePlan plan)
            {
                // 提交阶段必定失败，协调器不应到达该计划的发布阶段。
            }
        }

    }
}
