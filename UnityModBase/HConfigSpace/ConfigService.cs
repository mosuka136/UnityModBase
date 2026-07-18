using System;
using System.Collections.Generic;
using System.IO;
using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 管理单个配置文件与运行时配置表之间的绑定关系。
    /// 该类型负责读取/写入文件、创建表项、把磁盘上的 <see cref="ConfigFileEntry"/> 重新绑定到运行时 <see cref="ConfigEntry{T}"/>。
    /// 它不负责 UI 展示和具体配置项声明；这些职责分别由配置 GUI 与 <c>ConfigManager</c> 承担。
    /// 重载的回滚边界止于事件发布前；事件副作用和最终磁盘写入不属于可回滚范围。
    /// 文件模型、运行时模型和磁盘 IO 均不提供并发保护；创建、绑定、重载、保存及释放必须由调用方串行化。
    /// </summary>
    public class ConfigService : IDisposable
    {
        /// <summary>
        /// 控制配置项赋值后是否立即写回文件。
        /// 启用后，绑定项的值变化会在赋值线程上同步执行完整文件写入；批量初始化或重载期间应临时关闭，避免重复 IO。
        /// 关闭只抑制磁盘写入，运行时值和文件内存模型仍会更新。
        /// <see cref="Reload"/> 会在执行期间强制关闭该开关，并在所有成功或失败路径恢复调用前的值。
        /// </summary>
        public bool SaveOnConfigSet { get; set; } = true;

        /// <summary>
        /// 当前活动文件层模型。读取后保存磁盘恢复的表项，绑定过程还会在此模型中补齐缺失项和最新元数据。
        /// <see cref="Read"/> 会直接用读取结果替换该引用；<see cref="Reload"/> 仅在全部计划提交后替换。
        /// 重载预检或提交失败时保留旧实例；提交完成后即使事件发布或最终写入失败，也保留新实例。
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
        /// 创建配置文件管理器并立即尝试读取指定路径的配置。
        /// </summary>
        /// <param name="filePath">配置文件路径；文件不存在时会创建空的内存模型，实际文件在写入时生成。</param>
        /// <remarks>首次读取异常会转换为失败结果并记录，不会从构造函数抛出；此时 <see cref="FileSheet"/> 为 <c>null</c>。</remarks>
        public ConfigService(string filePath)
        {
            FilePath = filePath;
            Sheet = new ConfigSheet();
            Read();
        }

        /// <summary>
        /// 从 <see cref="FilePath"/> 读取并解析配置文件；只有结果携带有效模型时才替换 <see cref="FileSheet"/>。
        /// 文件不存在时使用空模型；可恢复的解析诊断会记录，解析器返回的部分模型仍会生效。
        /// </summary>
        /// <returns>
        /// 获得有效文件模型时返回 <c>true</c>；文件 IO、解析入口或结果处理失败时记录诊断并返回 <c>false</c>。
        /// 失败不会清空调用前已有的 <see cref="FileSheet"/>，也不会向调用方传播捕获到的异常。
        /// </returns>
        public bool Read()
        {
            try
            {
                var sheetResult = GetSheetFromFile();
                if (sheetResult.HasErrors)
                {
                    foreach (var error in sheetResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                }

                if (sheetResult.Success)
                {
                    FileSheet = sheetResult.Value;
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to read config file: {FilePath}.", ex);
                return false;
            }
        }

        /// <summary>
        /// 把文件读取和整表解析统一包装为结果对象，供首次读取与事务重载采用不同的失败策略。
        /// 文件不存在视为空配置成功；文件系统或解析入口抛出的异常转换为 <see cref="ConfigFileErrorCode.Exception"/>，不向上传播。
        /// </summary>
        /// <returns>
        /// 文件模型及解析诊断。解析器允许成功结果同时携带可恢复诊断；捕获到异常时返回不含有效模型的失败结果。
        /// </returns>
        private ConfigFileResult<ConfigFileSheet> GetSheetFromFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new ConfigFileSheet();

                var content = File.ReadAllText(FilePath).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                int index = 0;
                return ConfigFileSheet.DecodeSheet(content, ref index);
            }
            catch (Exception ex)
            {
                return ConfigFileResult<ConfigFileSheet>.Fail(new ConfigFileError(ConfigFileErrorCode.Exception, ex.ToString()));
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
        /// 重载先为所有已声明项准备只包含已验证值的重绑定计划；任一准备失败都会恢复旧文件模型，
        /// 并保留全部运行时值和旧绑定。准备成功后静默提交全部计划，提交异常时按逆序回滚，
        /// 全部提交完成后才发布值变化事件并保存规范化后的文件。
        /// </summary>
        /// <returns>
        /// 读取结果不含诊断、全部重绑定计划成功准备并应用、计划发布未抛出异常且最终写入成功时返回 <c>true</c>；
        /// 任一条件不满足时记录诊断并返回 <c>false</c>。
        /// </returns>
        /// <remarks>
        /// 所有路径都会恢复调用前的 <see cref="SaveOnConfigSet"/>。事件发布时全部运行时配置项均已切换到新状态。
        /// 已绑定的表或配置项在新文件中缺失会使整批预检失败；未建立运行时绑定的额外文件内容不参与计划，
        /// 但会保留在新文件模型中并随最终写入输出。
        /// 准备失败不会改变活动绑定；提交失败会按逆序尝试回滚，若回滚本身抛出异常则记录后继续处理其余计划。
        /// 因此回滚是尽力而为：发生回滚异常时，<see cref="FileSheet"/> 仍保留旧实例，但个别运行时表或配置项可能停留在部分提交状态。
        /// 事件发布或最终写入失败时，已经完整提交的内存状态不会回滚。
        /// 事件处理器在发布阶段产生的配置赋值会更新新文件模型，并包含在本次最终写入中。
        /// 与允许采用可恢复部分模型的 <see cref="Read"/> 不同，整表解析返回任何诊断都会在候选模型生效前拒绝本次重载，
        /// 包括“成功且含诊断”的结果。
        /// <see cref="ConfigEntry{T}"/> 会隔离并记录单个订阅者异常，因此这类异常不影响本方法的返回值。
        /// </remarks>
        public bool Reload()
        {
            var oldSaveOnConfigSet = SaveOnConfigSet;
            SaveOnConfigSet = false;

            try
            {
                var sheetResult = GetSheetFromFile();
                if (sheetResult.HasErrors)
                {
                    foreach (var error in sheetResult.Errors)
                        BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                    return false;
                }
                var fileSheet = sheetResult.Value;

                var plans = new List<ConfigReloadPlan>();
                var preparationSucceeded = true;

                // 扫描全部绑定以尽可能收集完整诊断，但任何一项失败都会阻止整批提交。
                // 候选文件模型尚未生效，准备阶段可以安全补充其中的运行时元数据。
                foreach (var table in Sheet.Values)
                {
                    var tableResult = fileSheet.GetTable(table.Key);
                    if (tableResult.Success)
                    {
                        tableResult.Value.Name = table.Name;
                        tableResult.Value.Description = table.Description;
                        plans.Add(new TableReloadPlan(table, tableResult.Value));
                    }
                    else
                    {
                        foreach (var error in tableResult.Errors)
                            BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                        preparationSucceeded = false;
                    }

                    foreach (var entry in table)
                    {
                        var entryResult = fileSheet.GetEntry(entry.TableName, entry.Key);
                        if (entryResult.Success)
                        {
                            if (!(entry is IConfigEntryReloadParticipant participant))
                            {
                                BLog.Error($"Config entry {entry.TableName}.{entry.Key} does not support transactional reload.");
                                preparationSucceeded = false;
                            }
                            else if (participant.TryPrepareRebind(entryResult.Value, out var plan, out var errorMessage))
                            {
                                plans.Add(plan);
                            }
                            else
                            {
                                BLog.Error($"Config entry {entry.TableName}.{entry.Key} in file is invalid. Errors: {errorMessage}");
                                preparationSucceeded = false;
                            }
                        }
                        else
                        {
                            preparationSucceeded = false;
                        }

                        if (entryResult.HasErrors)
                        {
                            foreach (var error in entryResult.Errors)
                                BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                            preparationSucceeded = false;
                        }
                    }
                }

                if (!preparationSucceeded)
                {
                    BLog.Error($"Failed to reload config from file: {FilePath}. See previous errors for details.");
                    return false;
                }

                var appliedPlans = new Stack<ConfigReloadPlan>();
                try
                {
                    foreach (var plan in plans)
                    {
                        // 先入栈，使 Apply 自身发生意外异常时也能参与后续的逆序回滚。
                        appliedPlans.Push(plan);
                        plan.Apply();
                    }
                }
                catch (Exception ex)
                {
                    while (appliedPlans.Count > 0)
                    {
                        try
                        {
                            appliedPlans.Pop().Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            BLog.Error("Failed to roll back a config entry reload plan.", rollbackException);
                        }
                    }

                    BLog.Error($"Failed to commit config reload: {FilePath}.", ex);
                    return false;
                }

                FileSheet = fileSheet;

                // 此时所有订阅者都能观察到完整的新配置快照，不会看到逐项切换的中间状态。
                var publishSucceeded = true;
                foreach (var plan in plans)
                {
                    try
                    {
                        plan.Publish();
                    }
                    catch (Exception ex)
                    {
                        BLog.Error("Failed to publish a config entry reload event.", ex);
                        publishSucceeded = false;
                    }
                }

                return Save() && publishSucceeded;
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to reload config file: {FilePath}.", ex);
                return false;
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
        /// 保存当前配置文件模型；行为及失败处理与 <see cref="Write"/> 相同。
        /// </summary>
        /// <returns>编码与写入成功时返回 <c>true</c>；失败时记录诊断并返回 <c>false</c>。</returns>
        public bool Save()
        {
            return Write();
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
            // 赋值和事件在到达此处前已经完成；自动保存失败只由 Write 记录，不回滚内存值，也不向事件调用方抛出。
            if (SaveOnConfigSet)
                Write();
        }
    }
}
