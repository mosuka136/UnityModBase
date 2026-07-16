using System;
using System.IO;
using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 管理单个配置文件与运行时配置表之间的绑定关系。
    /// 该类型负责读取/写入文件、创建表项、把磁盘上的 <see cref="ConfigFileEntry"/> 重新绑定到运行时 <see cref="ConfigEntry{T}"/>。
    /// 它不负责 UI 展示和具体配置项声明；这些职责分别由配置 GUI 与 <c>ConfigManager</c> 承担。
    /// 文件模型、运行时模型和磁盘 IO 均不提供并发保护；创建、绑定、重载、保存及释放必须由调用方串行化。
    /// </summary>
    public class ConfigService : IDisposable
    {
        /// <summary>
        /// 控制配置项赋值后是否立即写回文件。
        /// 启用后，绑定项的值变化会在赋值线程上同步执行完整文件写入；批量初始化或重载期间应临时关闭，避免重复 IO。
        /// 关闭只抑制磁盘写入，运行时值和文件内存模型仍会更新。
        /// </summary>
        public bool SaveOnConfigSet { get; set; } = true;

        /// <summary>
        /// 当前文件层模型。读取后保存磁盘恢复的表项，绑定过程还会在此模型中补齐缺失项和最新元数据。
        /// </summary>
        public ConfigFileSheet FileSheet { get; private set; }

        /// <summary>
        /// 运行时绑定后的配置模型，供业务代码直接访问强类型配置项。
        /// </summary>
        public ConfigSheet Sheet { get; private set; }

        /// <summary>
        /// 配置文件路径。修改该路径不会自动迁移旧文件，下一次读写会使用新路径。
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// <see cref="FilePath"/> 的文件名部分；路径为空时也为空。
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// 创建配置文件管理器并立即尝试读取指定路径的配置。
        /// </summary>
        /// <param name="filePath">配置文件路径；文件不存在时会创建空的内存模型，实际文件在写入时生成。</param>
        /// <remarks>首次读取异常会被记录而不会从构造函数抛出，此时 <see cref="FileSheet"/> 可能仍为 <c>null</c>。</remarks>
        public ConfigService(string filePath)
        {
            FilePath = filePath;
            Sheet = new ConfigSheet();
            Read();
        }

        /// <summary>
        /// 从 <see cref="FilePath"/> 读取并解析配置文件。
        /// 读取失败时记录错误并返回 <c>false</c>，不会向调用方抛出文件系统异常；已有 <see cref="FileSheet"/> 不会被清空。
        /// </summary>
        /// <returns>读取流程是否完成。解析错误会记录日志，但当前实现仍返回 <c>true</c> 并保存可解析出的模型。</returns>
        public bool Read()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    FileSheet = new ConfigFileSheet();
                    return true;
                }

                var content = File.ReadAllText(FilePath).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                int index = 0;
                var decodeResult = ConfigFileSheet.DecodeSheet(content, ref index);
                if (decodeResult.HasErrors)
                {
                    foreach (var error in decodeResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, index);
                }
                FileSheet = decodeResult.Value;
                return true;
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to read config file: {FilePath}.", ex);
                return false;
            }
        }

        /// <summary>
        /// 将当前文件模型编码并写入 <see cref="FilePath"/>。
        /// 写入先落到同目录的唯一临时文件：目标存在时通过替换生成同名 <c>.bak</c> 备份，目标不存在时再移动到最终路径，
        /// 以降低写入中断留下半截配置的风险。临时文件会在失败路径尽力清理。
        /// </summary>
        /// <returns>编码与写入是否成功；失败时记录日志并返回 <c>false</c>。</returns>
        public bool Write()
        {
            var tmpFilePath = string.Empty;

            try
            {
                var directoryPath = string.Empty;

                var encodeResult = FileSheet.EncodeSheet();
                if (encodeResult.HasErrors)
                {
                    foreach (var error in encodeResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                    return false;
                }

                directoryPath = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                tmpFilePath = Path.Combine(directoryPath ?? string.Empty, $"{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
                var backupFilePath = FilePath + ".bak";

                File.WriteAllText(tmpFilePath, encodeResult.Value);

                if (File.Exists(FilePath))
                    File.Replace(tmpFilePath, FilePath, backupFilePath, true);
                else
                    File.Move(tmpFilePath, FilePath);

                return true;
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to write config file: {FilePath}.", ex);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpFilePath))
                        File.Delete(tmpFilePath);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 重新读取磁盘配置，并把已声明配置项重新绑定到新的文件项。
        /// 元数据会从当前运行时配置同步回文件项，但不会覆盖用户在文件中的配置值。
        /// 仅磁盘中仍存在且能成功解码的项会替换绑定；缺失或非法项保留原运行时值和旧绑定。
        /// 期间会暂停逐项自动保存；全部重绑定正常完成后保存一次当前文件模型。
        /// 任一步骤异常都会记录日志并跳过剩余重绑定及本次保存，但所有路径都会恢复调用前的自动保存设置。
        /// </summary>
        public void Reload()
        {
            var oldSaveOnConfigSet = SaveOnConfigSet;
            SaveOnConfigSet = false;

            try
            {
                Read();
                foreach (var table in Sheet.Values)
                {
                    foreach (var entry in table)
                    {
                        var entryResult = FileSheet.GetEntry(entry.TableName, entry.Key);
                        if (entryResult.Success)
                        {
                            entry.Entry.CopyTo(entryResult.Value, false);
                            entry.RebindEntry(entryResult.Value);
                        }
                        if (entryResult.HasErrors)
                        {
                            foreach (var error in entryResult.Errors)
                                BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                        }
                    }
                }
                Save();
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to reload config file: {FilePath}.", ex);
            }
            finally
            {
                SaveOnConfigSet = oldSaveOnConfigSet;
            }
        }

        /// <summary>
        /// 绑定一个强类型配置项；如果文件中不存在该项，则使用默认值创建。
        /// 每次调用都会在运行时表中追加一个新绑定并订阅自动保存，调用方应确保同一表键和配置项键只绑定一次。
        /// 调用前必须先通过 <see cref="CreateTable"/> 建立对应的运行时表。
        /// </summary>
        /// <typeparam name="T">配置值类型，必须能被 <see cref="ConfigFileEntry"/> 编码和解码。</typeparam>
        /// <param name="tableKey">已有配置表键名。</param>
        /// <param name="key">配置项键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="defaultValue">文件中缺失该项时写入的默认值。</param>
        /// <param name="entryName">用于生成配置文件注释和 GUI 标签的名称。</param>
        /// <param name="description">用于生成配置文件注释和 GUI 提示的描述。</param>
        /// <returns>可在运行时读写的强类型配置项。</returns>
        /// <exception cref="ArgumentException">表不存在或键名非法时抛出。</exception>
        /// <exception cref="InvalidOperationException">现有值无法解码、类型或默认值无法编码，或者文件模型无法接受新项。</exception>
        /// <exception cref="NullReferenceException">文件表存在，但尚未通过 <see cref="CreateTable"/> 建立对应运行时表。</exception>
        public ConfigEntry<T> Bind<T>(string tableKey, string key, T defaultValue, Translator entryName, Translator description)
        {
            ConfigEntry<T> result = null;
            var entryResult = FileSheet.GetEntry(tableKey, key);
            if (entryResult.Success)
            {
                result = new ConfigEntry<T>(tableKey, entryResult.Value, defaultValue, entryName, description);
            }
            else
            {
                var tableResult = FileSheet.GetTable(tableKey);
                if (!tableResult.Success)
                {
                    foreach (var error in tableResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                    throw new ArgumentException($"Config table not found: {tableKey}.", nameof(tableKey));
                }
                var newEntry = new ConfigFileEntry();

                if (!ConfigFileEntry.IsValidKeyName(key))
                    throw new ArgumentException($"Invalid key name for config entry: {tableKey}.{key}.", nameof(key));
                newEntry.Key = key;

                var valueResult = ConfigFileEntry.EncodeValue(defaultValue);
                if (!valueResult.Success)
                {
                    foreach (var error in valueResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                    throw new InvalidOperationException($"Failed to encode default value for config entry: {tableKey}.{key}. Errors: {string.Join(", ", valueResult.Errors)}");
                }
                newEntry.Value = valueResult.Value;

                var addEntryResult = tableResult.Value.AddEntry(newEntry);
                if (!addEntryResult.Success)
                {
                    foreach (var error in addEntryResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                    throw new InvalidOperationException($"Failed to add config entry to table: {tableKey}.{key}.");
                }

                result = new ConfigEntry<T>(tableKey, newEntry, defaultValue, entryName, description);
            }

            result.OnValueChangedBase += OnConfigEntryChanged;

            Sheet[tableKey].Add(result);
            return result;
        }

        /// <summary>
        /// 在现有文件表中创建只包含键和当前值的文件项。
        /// 该辅助方法不创建运行时绑定，也不订阅自动保存事件。
        /// </summary>
        /// <typeparam name="T">默认值的声明类型。</typeparam>
        /// <param name="tableKey">已有文件表键名。</param>
        /// <param name="key">配置项键名。</param>
        /// <param name="defaultValue">编码后作为文件项当前值的默认值。</param>
        /// <returns>已加入文件表的文件项。</returns>
        /// <exception cref="ArgumentException">表不存在或键名非法。</exception>
        /// <exception cref="InvalidOperationException">默认值无法编码或文件表拒绝新项。</exception>
        internal ConfigFileEntry CreateFileEntry<T>(string tableKey, string key, T defaultValue)
        {
            var tableResult = FileSheet.GetTable(tableKey);
            if (!tableResult.Success)
            {
                foreach (var error in tableResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new ArgumentException($"Config table not found: {tableKey}.", nameof(tableKey));
            }

            var result = new ConfigFileEntry();

            if (!ConfigFileEntry.IsValidKeyName(key))
                throw new ArgumentException($"Invalid key name for config entry: {tableKey}.{key}.", nameof(key));
            result.Key = key;

            var valueResult = ConfigFileEntry.EncodeValue(defaultValue);
            if (!valueResult.Success)
            {
                foreach (var error in valueResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to encode default value for config entry: {tableKey}.{key}. Errors: {string.Join(", ", valueResult.Errors)}");
            }
            result.Value = valueResult.Value;

            var addEntryResult = tableResult.Value.AddEntry(result);
            if (!addEntryResult.Success)
            {
                foreach (var error in addEntryResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to add config entry to table: {tableKey}.{key}.");
            }

            return result;
        }

        /// <summary>
        /// 声明一个配置表；文件中不存在时会创建对应表结构。
        /// 已存在于运行时模型的表会直接保留原元数据；只存在于文件模型的表会在首次声明时建立运行时绑定并同步本次元数据。
        /// </summary>
        /// <param name="tableKey">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="tableName">运行时展示名称。</param>
        /// <param name="description">写入配置文件的表说明，可为空。</param>
        /// <exception cref="InvalidOperationException">表名非法，或新表无法加入文件模型。</exception>
        public void CreateTable(string tableKey, Translator tableName, Translator description = null)
        {
            var tableResult = FileSheet.GetTable(tableKey);
            if (tableResult.Success)
            {
                if (!Sheet.Contains(tableKey))
                    Sheet.Add(tableKey, new ConfigTable(tableKey, tableResult.Value, tableName, description));
                return;
            }

            var newTableResult = ConfigFileSheet.CreateTable(tableKey, description);
            if (!newTableResult.Success)
            {
                foreach (var error in newTableResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to create config table: {tableKey}.");
            }

            var addTableResult = FileSheet.AddTable(newTableResult.Value);
            if (!addTableResult.Success)
            {
                foreach (var error in addTableResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to add config table: {tableKey}.");
            }

            Sheet.Add(tableKey, new ConfigTable(tableKey, newTableResult.Value, tableName, description));
        }

        /// <summary>
        /// 保存当前配置文件模型。该便捷方法不返回写入状态；需要处理失败时应直接调用 <see cref="Write"/>。
        /// </summary>
        public void Save()
        {
            Write();
        }

        /// <summary>
        /// 取消所有自动保存事件订阅并释放对文件模型和运行时模型的引用。
        /// 该操作不会隐式保存；可重复调用，但释放后的服务不应继续用于读取、绑定或写入。
        /// </summary>
        public void Dispose()
        {
            foreach (var table in Sheet?.Values ?? Array.Empty<ConfigTable>())
            {
                foreach (var entry in table)
                    entry.OnValueChangedBase -= OnConfigEntryChanged;
            }

            FileSheet = null;
            Sheet = null;
        }

        private void OnConfigEntryChanged(object sender, EventArgs args)
        {
            if (SaveOnConfigSet)
                Write();
        }
    }
}
