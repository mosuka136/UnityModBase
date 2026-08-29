using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 表示配置文件中的一个表段。
    /// 表段按插入顺序保存配置项，负责编码/解码 <c>[Table]</c> 头和表内键值项，不处理跨表级别的文件结构。
    /// 解码只恢复表键名和配置项，不恢复名称、说明等注释元数据；模型可变且不提供并发保护。
    /// </summary>
    public class ConfigFileTable
    {
        /// <summary>
        /// 写入文件的表名称注释；当前解析流程不会从已有注释中恢复该值。
        /// </summary>
        public Translator Name { get; set; }
        /// <summary>
        /// 写入文件的表说明注释；当前解析流程不会从已有注释中恢复该值。
        /// </summary>
        public Translator Description { get; set; }
        /// <summary>
        /// 表键名，对应配置文件中的 <c>[Key]</c>。
        /// 构造时会校验，但属性本身允许后续写入任意值；编码表头时会再次校验。
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 表内配置项，使用有序字典以保持写出顺序稳定。
        /// 返回的是可变字典；直接修改会绕过键重复检查以及字典键与 <see cref="ConfigFileEntry.Key"/> 的一致性约束。
        /// </summary>
        public OrderedDictionary Table { get; private set; } = new OrderedDictionary();

        /// <summary>
        /// 创建空文件表。
        /// </summary>
        /// <param name="tableKey">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="description">写出时使用的多语言说明，允许为 <c>null</c>。</param>
        /// <exception cref="ArgumentException"><paramref name="tableKey"/> 不符合表名语法。</exception>
        public ConfigFileTable(string tableKey, Translator description)
        {
            if (EntryModel.IsValidTableKey(tableKey))
                Key = tableKey;
            else
                throw new ArgumentException($"Invalid table name: {tableKey}", nameof(tableKey));
            Description = description;
        }

        /// <summary>
        /// 按配置项键名追加文件项，并拒绝重复键。
        /// </summary>
        /// <param name="entry">待加入的文件项。</param>
        /// <returns>成功时携带当前表；输入为空或键重复时返回失败结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 非空但尚未设置键名。</exception>
        public ConfigFileResult<ConfigFileTable> AddEntry(ConfigFileEntry entry)
        {
            if (entry == null)
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.EntryNotFound, "Entry cannot be null"));

            if (Table.Contains(entry.Key))
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidKeyName, $"Duplicate key: {entry.Key}"));

            Table.Add(entry.Key, entry);
            return this;
        }

        /// <summary>
        /// 按键名查询文件项。
        /// </summary>
        /// <param name="key">配置项键名。</param>
        /// <returns>找到的文件项，或带 <see cref="ConfigFileErrorCode.EntryNotFound"/> 的失败结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> 为 <c>null</c>。</exception>
        public ConfigFileResult<ConfigFileEntry> GetEntry(string key)
        {
            if (Table.Contains(key))
                return (ConfigFileEntry)Table[key];
            return ConfigFileResult<ConfigFileEntry>.Fail(new ConfigFileError(ConfigFileErrorCode.EntryNotFound, $"Entry not found: {key}"));
        }

        /// <summary>
        /// 将全部非空名称翻译编码到单行 <c># Name:</c> 注释中。
        /// </summary>
        /// <returns>逗号分隔的名称注释；没有可用名称时为空字符串。</returns>
        public ConfigFileResult<string> EncodeName()
        {
            if (Name == null)
                return string.Empty;
            var list = new List<string>();
            foreach (var name in Name)
            {
                if (string.IsNullOrEmpty(name))
                    continue;
                list.Add(name);
            }
            return $"# Name: {string.Join(", ", list)}";
        }

        /// <summary>
        /// 将全部非空说明翻译编码为 <c>##</c> 注释；多行说明会逐行添加前缀。
        /// </summary>
        /// <returns>说明注释；没有可用说明时为空字符串。</returns>
        public ConfigFileResult<string> EncodeDescription()
        {
            if (Description == null)
                return string.Empty;
            var sb = new StringBuilder();
            foreach (var description in Description)
            {
                if (string.IsNullOrEmpty(description))
                    continue;
                var lines = description.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                foreach (var line in lines)
                    sb.AppendLine($"## {line}");
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 编码 <c>[Table]</c> 表头，并再次验证当前键名。
        /// </summary>
        /// <returns>表头文本，或表名非法诊断。</returns>
        public ConfigFileResult<string> EncodeTableHeader()
        {
            if (!EntryModel.IsValidTableKey(Key))
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidTableName, $"Invalid table name: {Key}"));
            return $"[{Key}]";
        }

        /// <summary>
        /// 编码表注释、表头和全部可编码配置项。
        /// 单个配置项失败时会跳过该项并继续编码其余内容，返回值因此可能成功且同时携带错误；整文件编码器会把这类诊断视为写出失败。
        /// </summary>
        /// <returns>去除首尾空白后的表段文本，或无法生成表头时的失败结果。</returns>
        public ConfigFileResult<string> EncodeTable()
        {
            var nameResult = EncodeName();
            if (!nameResult.Success)
                return ConfigFileResult<string>.Fail(nameResult.Errors);

            var descriptionResult = EncodeDescription();
            if (!descriptionResult.Success)
                return ConfigFileResult<string>.Fail(descriptionResult.Errors);

            var tableHeaderResult = EncodeTableHeader();
            if (!tableHeaderResult.Success)
                return ConfigFileResult<string>.Fail(tableHeaderResult.Errors);

            var sb = new StringBuilder();
            var result = new ConfigFileResult<string>();

            if (nameResult.Value != string.Empty)
                sb.AppendLine(nameResult.Value);
            if (descriptionResult.Value != string.Empty)
                sb.AppendLine(descriptionResult.Value);
            sb.AppendLine(tableHeaderResult.Value);
            sb.AppendLine();

            foreach (var entry in Table.Values)
            {
                var entryResult = ((ConfigFileEntry)entry).EncodeEntry();
                if (entryResult.Success)
                    sb.AppendLine(entryResult.Value);
                if (entryResult.HasErrors)
                    result.AddError(entryResult.Errors);
                sb.AppendLine();
            }

            result.SetValue(sb.ToString().Trim());
            return result;
        }

        /// <summary>
        /// 验证表名并创建空文件表，不会自动加入任何 <see cref="ConfigFileSheet"/>。
        /// </summary>
        /// <param name="tableName">表键名。</param>
        /// <param name="description">写出时使用的多语言说明。</param>
        /// <returns>新文件表，或表名非法诊断。</returns>
        public static ConfigFileResult<ConfigFileTable> Create(string tableName, Translator description)
        {
            if (!EntryModel.IsValidTableKey(tableName))
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidTableName, $"Invalid table name: {tableName}"));
            var table = new ConfigFileTable(tableName, description);
            return table;
        }

        /// <summary>
        /// 从当前位置解析一个完整表段。
        /// </summary>
        /// <param name="content">按行拆分后的配置文件内容。</param>
        /// <param name="index">读取起点；返回时停在当前表已消费内容之后，下一表前的空行或注释可能仍留给后续解析。</param>
        /// <returns>
        /// 解析出的表段。表内部分配置项失败时会保留已成功解析的项并收集错误，
        /// 因而结果可能同时为成功状态并携带诊断。
        /// </returns>
        public static ConfigFileResult<ConfigFileTable> DecodeTable(string[] content, ref int index)
        {
            var headerResult = DecodeTableHeader(content, ref index);
            if (!headerResult.Success)
                return ConfigFileResult<ConfigFileTable>.Fail(headerResult.Errors);

            var table = new ConfigFileTable(headerResult.Value.Key, headerResult.Value.Description);
            var result = new ConfigFileResult<ConfigFileTable>(table);

            // 使用独立索引前瞻后续有效内容是否为表头，避免结束当前表时消费下一表及其前置注释。
            for (var i = index; index < content.Length && !DecodeTableHeader(content, ref i).Success; i = index)
            {
                var entryResult = ConfigFileEntry.DecodeEntry(content, ref index);
                if (entryResult.Success)
                {
                    // 表键名不出现在配置文件文本中，解析得到键值后按所属表回填，供运行时重绑定校验候选项归属。
                    entryResult.Value.TableKey = table.Key;
                    var entryAddResult = table.AddEntry(entryResult.Value);
                    if (!entryAddResult.Success)
                        result.AddError(entryAddResult.Errors);
                }
                if (entryResult.HasErrors)
                {
                    if (entryResult.Errors.Any(e => e.Code == ConfigFileErrorCode.EndOfContent))
                        break;
                    result.AddError(entryResult.Errors);
                }
            }

            result.SetValue(table);
            return result;
        }

        /// <summary>
        /// 跳过空行和注释后解析第一个有效内容行；该行不是合法表头时会被消费并返回失败。
        /// </summary>
        /// <param name="content">按行拆分后的配置文件内容。</param>
        /// <param name="index">读取起点；成功或遇到非法内容时推进到该行之后。</param>
        /// <returns>只包含表键名的表模型；没有更多内容时返回 <see cref="ConfigFileErrorCode.EndOfContent"/>。</returns>
        public static ConfigFileResult<ConfigFileTable> DecodeTableHeader(string[] content, ref int index)
        {
            for (; index < content.Length; index++)
            {
                var line = content[index].Trim();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    index++;
                    var tableName = line.Substring(1, line.Length - 2);
                    var tableResult = Create(tableName, new Translator());
                    if (!tableResult.Success)
                        return ConfigFileResult<ConfigFileTable>.Fail(tableResult.Errors);
                    return tableResult;
                }

                index++;
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidTableHeader, $"Invalid table header: {line}"));
            }

            return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.EndOfContent, "No more content to process"));
        }
    }
}
