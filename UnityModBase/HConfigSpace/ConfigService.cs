using System;
using System.Collections.Generic;
using System.IO;
using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 管理单个配置文件与运行时配置表之间的绑定关系。
    /// 该类型负责读取/写入文件、创建表项，以及把磁盘上的 <see cref="ConfigFileEntry"/> 绑定到单值或双元素运行时配置项。
    /// 它不负责 UI 展示和具体配置项声明；这些职责分别由配置 GUI 与上层配置管理器承担。
    /// 重载的回滚边界止于事件发布前；配置项变化事件、配置模型变化事件及最终磁盘写入不属于可回滚范围。
    /// 文件模型、运行时模型和磁盘 IO 均不提供并发保护；创建、绑定、重载、保存及释放必须由调用方串行化。
    /// </summary>
    public class ConfigService : IDisposable
    {
        private string _filePath;

        /// <summary>
        /// 文件模型成功读取、事务重载完成内存提交，以及运行时表或配置项声明成功后同步触发。
        /// 该事件表示配置模型可能需要重新投影，不表示每次 <see cref="ConfigEntry{T}.Value"/> 赋值；
        /// 需要监听单项值变化时应订阅对应配置项的变化事件。
        /// </summary>
        /// <remarks>
        /// <see cref="Read"/> 在替换 <see cref="FileSheet"/> 后触发，但不会自动重绑定现有 <see cref="Sheet"/>；
        /// <see cref="Reload"/> 在全部计划提交和单项事件发布后、最终写盘前触发。
        /// <see cref="CreateTable"/> 每个成功路径都会触发；运行时表已存在时不再静默复用原表，而是抛出异常。
        /// 构造函数中的首次 <see cref="Read"/> 在实例可供外部订阅前完成，不能用作服务创建通知。
        /// 订阅者在发起操作的线程上按登记顺序同步执行；单个订阅者异常只记录日志，不影响调用操作的成功状态。
        /// </remarks>
        public event Action OnConfigChanged;

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
        /// 配置文件路径，必须非空且包含目录部分，以便写入时在目标目录内创建临时文件和备份。
        /// 修改该路径不会自动迁移旧文件，也不会重新绑定已声明的运行时配置项；后续读写使用新路径。
        /// </summary>
        /// <exception cref="ArgumentException">赋值为 <c>null</c>、空白字符串或不包含目录部分。</exception>
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("FilePath cannot be null or whitespace.", nameof(value));

                if (string.IsNullOrEmpty(Path.GetDirectoryName(value)))
                    throw new ArgumentException("FilePath must contain a directory.", nameof(value));

                _filePath = value;
            }
        }

        /// <summary>
        /// 创建配置文件管理器并立即尝试读取指定路径的配置。
        /// </summary>
        /// <param name="filePath">包含目录部分的配置文件路径；文件不存在时会创建空的内存模型，实际文件在写入时生成。</param>
        /// <exception cref="ArgumentNullException"><paramref name="filePath"/> 为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> 为空白字符串或不包含目录部分。</exception>
        /// <remarks>路径校验在首次读取前执行。通过校验后，首次读取的 IO 或解析异常会转换为失败结果并记录，不会从构造函数传播；此时 <see cref="FileSheet"/> 为 <c>null</c>。</remarks>
        public ConfigService(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Sheet = new ConfigSheet();
            Read();
        }

        /// <summary>
        /// 从 <see cref="FilePath"/> 读取并解析配置文件；只有结果携带有效模型时才替换 <see cref="FileSheet"/>。
        /// 文件不存在时使用空模型；可恢复的解析诊断会记录，解析器返回的部分模型仍会生效。
        /// 模型替换完成后会同步触发 <see cref="OnConfigChanged"/>。
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
                    InvokeOnConfigChanged();
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
        /// 单项事件发布或最终写入失败时，已经完整提交的内存状态不会回滚。
        /// 事件处理器在发布阶段产生的配置赋值会更新新文件模型，并包含在本次最终写入中。
        /// <see cref="OnConfigChanged"/> 在单项事件发布完成后、最终写入前触发；其订阅者异常会被隔离，
        /// 但订阅者对配置的修改会包含在本次最终写入中。
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

                var plans = new List<EntryChangePlan>();
                var preparationSucceeded = true;

                // 扫描全部绑定以尽可能收集完整诊断，但任何一项失败都会阻止整批提交。
                // 候选文件模型尚未生效，准备阶段可以安全补充其中的运行时元数据。
                foreach (var table in Sheet.Values)
                {
                    var tableResult = fileSheet.GetTable(table.Key);
                    if (!tableResult.Success)
                    {
                        foreach (var error in tableResult.Errors)
                            BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                        preparationSucceeded = false;
                    }

                    foreach (var entry in table)
                    {
                        var entryResult = fileSheet.GetEntry(entry.TableKey, entry.Key);
                        if (entryResult.Success)
                        {
                            if (entry.PrepareBind(entryResult.Value, out var plan, out var errorMessage))
                            {
                                plans.Add(plan);
                            }
                            else
                            {
                                BLog.Error($"Config entry {entry.TableKey}.{entry.Key} in file is invalid. Errors: {errorMessage}");
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
                    BLog.Error($"Config reload preparation failed. Path='{FilePath}', PreparedEntries={plans.Count}. See earlier diagnostics for rejected tables or entries.");
                    return false;
                }

                var appliedPlans = new Stack<EntryChangePlan>();
                try
                {
                    foreach (var plan in plans)
                    {
                        // 先入栈，使 Apply 自身发生意外异常时也能参与后续的逆序回滚。
                        appliedPlans.Push(plan);
                        plan.ConfigEntry.ApplyBind(plan);
                    }
                }
                catch (Exception ex)
                {
                    while (appliedPlans.Count > 0)
                    {
                        var plan = appliedPlans.Pop();
                        try
                        {
                            plan.ConfigEntry.RollbackBind(plan);
                        }
                        catch (Exception rollbackException)
                        {
                            BLog.Error($"Failed to roll back config entry during reload. Entry='{plan.NewEntry.TableKey}.{plan.NewEntry.Key}', Path='{FilePath}'.", rollbackException);
                        }
                    }

                    BLog.Error($"Config reload commit failed; applied entries were rolled back where possible. Path='{FilePath}', PlannedEntries={plans.Count}.", ex);
                    return false;
                }

                FileSheet = fileSheet;

                // 此时所有订阅者都能观察到完整的新配置快照，不会看到逐项切换的中间状态。
                var publishSucceeded = true;
                foreach (var plan in plans)
                {
                    try
                    {
                        plan.ConfigEntry.PublishBind(plan);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Failed to publish config reload event for entry '{plan.NewEntry.TableKey}.{plan.NewEntry.Key}'. Path='{FilePath}'.", ex);
                        publishSucceeded = false;
                    }
                }

                InvokeOnConfigChanged();

                return Save() && publishSucceeded;
            }
            catch (Exception ex)
            {
                BLog.Error($"Unexpected failure during config reload. Path='{FilePath}'.", ex);
                return false;
            }
            finally
            {
                SaveOnConfigSet = oldSaveOnConfigSet;
            }
        }

        /// <summary>
        /// 绑定一个强类型配置项；如果文件中不存在该项，则使用默认值创建。
        /// 文件中已存在的有效值优先于声明默认值；默认值只用于补齐缺失项和生成元数据。
        /// 每次调用都会在运行时表中追加一个新绑定并订阅自动保存；同一表键和配置项键重复绑定会在登记阶段抛出异常，由服务保证唯一性。
        /// 调用前必须先通过 <see cref="CreateTable"/> 建立对应的运行时表。
        /// 新绑定加入运行时表后会同步触发 <see cref="OnConfigChanged"/>。
        /// </summary>
        /// <typeparam name="T">配置值类型，必须能被 <see cref="ConfigFileEntry"/> 编码和解码。</typeparam>
        /// <param name="tableKey">已有配置表键名。</param>
        /// <param name="key">配置项键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="defaultValue">文件模型中缺失该项时使用的初始值。</param>
        /// <param name="name">用于生成配置文件注释和 GUI 标签的名称。</param>
        /// <param name="description">用于生成配置文件注释和 GUI 提示的描述。</param>
        /// <returns>可在运行时读写的强类型配置项。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tableKey"/> 或 <paramref name="key"/> 为空白字符串，或 <paramref name="name"/> 或 <paramref name="description"/> 为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException">表不存在或键名非法时抛出。</exception>
        /// <exception cref="InvalidOperationException">现有值无法解码、类型或默认值无法编码，文件模型无法接受新项，或同一表键和配置项键已存在运行时绑定。</exception>
        /// <exception cref="NullReferenceException">服务已释放、<see cref="FileSheet"/> 首次读取失败，或尚未通过 <see cref="CreateTable"/> 建立对应运行时表。</exception>
        /// <remarks>文件模型的补项和元数据更新早于运行时登记；若后续的类型校验或登记失败，这些文件模型变更不会自动回滚。</remarks>
        public ConfigEntry<T> Bind<T>(string tableKey, string key, T defaultValue, Translator name, Translator description)
        {
            if (string.IsNullOrWhiteSpace(tableKey))
                throw new ArgumentNullException(nameof(tableKey));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));

            var configFileEntry = GetOrCreateConfigFileEntry(tableKey, key, defaultValue);
            var result = new ConfigEntry<T>(configFileEntry, defaultValue, name, description);
            Bind(tableKey, result);
            return result;
        }

        /// <summary>
        /// 绑定一个以顶层逗号分隔文本存储的双元素配置项；文件中不存在该项时，使用两个声明默认值创建。
        /// 文件中已有的有效值优先于默认值，两个元素说明会与整体说明按语言拼接后写入配置项元数据。
        /// 返回的门面允许分别读写两个元素，但单元素写入仍会整体替换双元素值，从而复用统一的编码、事件和自动保存流程。
        /// 每次调用都会追加新运行时绑定；调用前必须先通过 <see cref="CreateTable"/> 建立运行时表，同一配置键重复绑定会在登记阶段抛出异常。
        /// </summary>
        /// <typeparam name="T1">第一个元素的值类型，必须受配置编解码器支持，且不能是另一个双元素配置值适配器。</typeparam>
        /// <typeparam name="T2">第二个元素的值类型，必须受配置编解码器支持，且不能是另一个双元素配置值适配器。</typeparam>
        /// <param name="tableKey">已有配置表键名。</param>
        /// <param name="key">配置项键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="defaultValue1">文件中缺失该项时使用的第一个元素默认值。</param>
        /// <param name="defaultValue2">文件中缺失该项时使用的第二个元素默认值。</param>
        /// <param name="name">用于生成配置文件注释和 GUI 标签的名称。</param>
        /// <param name="description">双元素配置项的整体说明。</param>
        /// <param name="valueDescription1">第一个元素的说明；仅用于文件注释和 GUI 提示，不参与取值校验。</param>
        /// <param name="valueDescription2">第二个元素的说明；仅用于文件注释和 GUI 提示，不参与取值校验。</param>
        /// <returns>可整体或按元素读写的双元素配置项门面。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tableKey"/> 或 <paramref name="key"/> 为空白字符串，或 <paramref name="name"/> 或任一说明参数为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException">表不存在、键名非法，或任一元素类型会使双元素平铺格式产生嵌套分隔歧义。</exception>
        /// <exception cref="InvalidOperationException">现有双元素文本无法解码、元素类型或默认值无法编码，文件模型无法接受新项，或同一表键和配置项键已存在运行时绑定。</exception>
        /// <exception cref="NullReferenceException">服务已释放、<see cref="FileSheet"/> 首次读取失败，或尚未通过 <see cref="CreateTable"/> 建立对应运行时表。</exception>
        /// <remarks>文件模型的补项和元数据更新早于运行时登记；若后续的类型校验或登记失败，这些文件模型变更不会自动回滚。</remarks>
        public ConfigEntry<T1, T2> Bind<T1, T2>(
            string tableKey,
            string key,
            T1 defaultValue1,
            T2 defaultValue2,
            Translator name,
            Translator description,
            Translator valueDescription1,
            Translator valueDescription2)
        {
            if (string.IsNullOrWhiteSpace(tableKey))
                throw new ArgumentNullException(nameof(tableKey));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));
            if (valueDescription1 == null)
                throw new ArgumentNullException(nameof(valueDescription1));
            if (valueDescription2 == null)
                throw new ArgumentNullException(nameof(valueDescription2));

            // 双元素文本没有外层定界符；元素若再次使用相同平铺格式，其逗号会被外层误判为额外元素，导致无法逆向解码。
            var genericType = typeof(IGenericConfigEntryValue);
            if (genericType.IsAssignableFrom(typeof(T1)))
                throw new ArgumentException($"Type {typeof(T1).FullName} is a generic ConfigEntryValue type and cannot be used as a direct element type in a dual-value config entry.");
            if (genericType.IsAssignableFrom(typeof(T2)))
                throw new ArgumentException($"Type {typeof(T2).FullName} is a generic ConfigEntryValue type and cannot be used as a direct element type in a dual-value config entry.");

            var defaultValue = new ConfigEntryValue<T1, T2>(defaultValue1, defaultValue2);
            var configFileEntry = GetOrCreateConfigFileEntry(tableKey, key, defaultValue);
            var result = new ConfigEntry<T1, T2>(configFileEntry, defaultValue1, defaultValue2, name, description, valueDescription1, valueDescription2);
            Bind(tableKey, result);
            return result;
        }

        /// <summary>
        /// 从文件模型取得指定项；仅在配置表存在且配置项缺失时，编码默认值并把新项追加到该表。
        /// 已有项会原样返回，其值解码和名称、说明、类型等元数据同步由随后构造的运行时配置项负责。
        /// 本方法只维护文件层模型，不会注册运行时项、订阅自动保存事件或触发 <see cref="OnConfigChanged"/>。
        /// </summary>
        /// <typeparam name="T">用于创建缺失项的默认值类型。</typeparam>
        /// <param name="tableKey">目标文件表键名。</param>
        /// <param name="key">目标配置项键名。</param>
        /// <param name="defaultValue">缺失项的初始值。</param>
        /// <returns>文件模型中已有或本次新建的配置项。</returns>
        /// <exception cref="ArgumentException"><paramref name="tableKey"/> 或 <paramref name="key"/> 不符合键名语法（含 <c>null</c> 或空白），或目标表不存在。</exception>
        /// <exception cref="InvalidOperationException">默认值无法编码，或新项无法加入目标表。</exception>
        /// <exception cref="NullReferenceException"><see cref="FileSheet"/> 尚未初始化或服务已释放。</exception>
        /// <remarks>键名格式校验先于任何文件查询执行，非法键在读取已有项之前即被拒绝。</remarks>
        private ConfigFileEntry GetOrCreateConfigFileEntry<T>(string tableKey, string key, T defaultValue)
        {
            if (!ConfigFileModel.IsValidTableKey(tableKey))
                throw new ArgumentException($"Invalid table key: {tableKey}.", nameof(tableKey));
            if (!ConfigFileModel.IsValidEntryKey(key))
                throw new ArgumentException($"Invalid entry key: {tableKey}.{key}.", nameof(key));

            var entryResult = FileSheet.GetEntry(tableKey, key);
            if (entryResult.Success)
                return entryResult.Value;

            var tableResult = FileSheet.GetTable(tableKey);
            if (!tableResult.Success)
            {
                foreach (var error in tableResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new ArgumentException($"Config table not found: {tableKey}.", nameof(tableKey));
            }

            var valueResult = ConfigFileEntry.EncodeValue(defaultValue);
            if (!valueResult.Success)
            {
                foreach (var error in valueResult.Errors)
                    BLog.Error(error.GetFullMessage(), null, string.Empty, string.Empty, 0);
                throw new InvalidOperationException($"Failed to encode default value for config entry: {tableKey}.{key}. Errors: {string.Join(", ", valueResult.Errors)}");
            }

            var result = new ConfigFileEntry
            {
                TableKey = tableKey,
                Key = key,
                Value = valueResult.Value,
            };

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
        /// 将已完成文件绑定和初始值校验的运行时配置项登记到现有运行时表，并接入自动保存事件。
        /// 该低级步骤会通过 <see cref="ConfigTable.Contains"/> 拒绝向运行时表重复登记同一配置键；登记成功后会同步触发一次 <see cref="OnConfigChanged"/>。
        /// </summary>
        /// <param name="tableKey">已通过 <see cref="CreateTable"/> 建立的运行时表键名。</param>
        /// <param name="entry">待登记的运行时配置项。</param>
        /// <exception cref="ArgumentException"><paramref name="tableKey"/> 不符合表键名语法（含 <c>null</c> 或空白）。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">同一表键和配置项键已存在运行时绑定。</exception>
        /// <exception cref="NullReferenceException"><see cref="Sheet"/> 已释放，或目标运行时表不存在。</exception>
        private void Bind(string tableKey, IConfigEntry entry)
        {
            if (!ConfigFileModel.IsValidTableKey(tableKey))
                throw new ArgumentException($"Invalid table key: {tableKey}.", nameof(tableKey));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var table = Sheet[tableKey] ?? throw new NullReferenceException($"Config table not found in runtime sheet: {tableKey}.");

            if (table.Contains(entry))
                throw new InvalidOperationException($"Config entry already exists in runtime table: {tableKey}.{entry.Key}.");

            entry.OnValueChangedBase += OnConfigEntryChanged;
            table.Add(entry);
            InvokeOnConfigChanged();
        }

        /// <summary>
        /// 声明一个配置表；文件中不存在时会创建对应表结构，只存在于文件模型的表会在首次声明时建立运行时绑定并同步本次元数据。
        /// 运行时模型中已存在同键表时不再静默复用原表，而是抛出异常；重复声明无法用于刷新表元数据。
        /// 成功路径均会同步触发 <see cref="OnConfigChanged"/>，因此调用方不能把该事件次数等同于新增表数量。
        /// </summary>
        /// <param name="tableKey">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="tableName">运行时展示名称。</param>
        /// <param name="description">写入配置文件的表说明，可为空。</param>
        /// <exception cref="ArgumentException"><paramref name="tableKey"/> 不符合表键名语法（含 <c>null</c> 或空白）。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="tableName"/> 为 <c>null</c>。</exception>
        /// <exception cref="InvalidOperationException">运行时表中已存在同键表，新表无法创建，或新表无法加入文件模型。</exception>
        /// <exception cref="NullReferenceException">服务已释放，或 <see cref="FileSheet"/> 首次读取失败。</exception>
        public void CreateTable(string tableKey, Translator tableName, Translator description = null)
        {
            if (!ConfigFileModel.IsValidTableKey(tableKey))
                throw new ArgumentException($"Invalid table key: {tableKey}.", nameof(tableKey));
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));

            var tableResult = FileSheet.GetTable(tableKey);
            if (tableResult.Success)
            {
                if (Sheet.Contains(tableKey))
                    throw new InvalidOperationException($"Config table already exists in runtime sheet: {tableKey}.");

                Sheet.Add(tableKey, new ConfigTable(tableKey, tableResult.Value, tableName, description));
                InvokeOnConfigChanged();
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
            InvokeOnConfigChanged();
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
        /// 取消所有自动保存和配置模型变化事件订阅，并释放对文件模型和运行时模型的引用。
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
            OnConfigChanged = null;
        }

        private void OnConfigEntryChanged(object sender, EventArgs args)
        {
            // 赋值和事件在到达此处前已经完成；自动保存失败只由 Write 记录，不回滚内存值，也不向事件调用方抛出。
            if (SaveOnConfigSet)
                Write();
        }

        private void InvokeOnConfigChanged()
        {
            // 配置维护操作不能因界面投影等单个观察者失败而中断，因此逐个隔离订阅者异常。
            foreach (var handler in OnConfigChanged.GetInvocationListOrEmpty())
            {
                try
                {
                    handler.Invoke();
                }
                catch (Exception ex)
                {
                    BLog.Error($"Config changed handler '{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}' failed. Path='{FilePath}'; remaining handlers will continue.", ex);
                }
            }
        }
    }
}
