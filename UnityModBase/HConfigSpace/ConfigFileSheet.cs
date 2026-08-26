using System.Collections.Specialized;
using System.Linq;
using System.Text;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 表示整个配置文件的内存模型。
    /// 该类型按文件顺序保存多个配置表，负责整文件级别的编码/解码协调，不负责磁盘 IO。
    /// 解析器只恢复表头和键值行，文件中的说明注释会被忽略；运行时声明会在绑定阶段重新补充元数据。
    /// 模型可变且不提供并发保护，读写和枚举必须由调用方串行化。
    /// </summary>
    public class ConfigFileSheet
    {
        /// <summary>
        /// 配置表集合，使用有序字典保持写出顺序稳定。
        /// 返回的是可变字典；直接修改会绕过表名和重复键校验。
        /// </summary>
        public OrderedDictionary Sheet { get; private set; } = new OrderedDictionary();

        /// <summary>
        /// 使用表自身的键名追加文件表。
        /// </summary>
        /// <param name="table">键名有效且尚未加入集合的文件表。</param>
        /// <returns>添加成功时携带原表实例，否则携带结构化诊断。</returns>
        /// <exception cref="System.NullReferenceException"><paramref name="table"/> 为 <c>null</c>。</exception>
        public ConfigFileResult<ConfigFileTable> AddTable(ConfigFileTable table)
        {
            return AddTable(table.Key, table);
        }

        /// <summary>
        /// 以指定键追加文件表，不会改写 <see cref="ConfigFileTable.Key"/>；调用方应保证两者一致。
        /// </summary>
        /// <param name="tableKey">字典键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="table">待加入的文件表。</param>
        /// <returns>添加成功时携带原表实例；键非法、表为空或键重复时返回失败结果。</returns>
        public ConfigFileResult<ConfigFileTable> AddTable(string tableKey, ConfigFileTable table)
        {
            if (!ConfigFileModel.IsValidTableKey(tableKey))
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidTableName, $"Invalid table name: {tableKey}"));
            if (table == null)
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.TableNotFound, "Table cannot be null"));
            if (Sheet.Contains(tableKey))
                return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidTableName, $"Table name already exists: {tableKey}"));
            Sheet.Add(tableKey, table);
            return table;
        }

        /// <summary>
        /// 按表键名查询文件表。
        /// </summary>
        /// <param name="tableKey">表键名。</param>
        /// <returns>找到的文件表，或带 <see cref="ConfigFileErrorCode.TableNotFound"/> 的失败结果。</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="tableKey"/> 为 <c>null</c>。</exception>
        public ConfigFileResult<ConfigFileTable> GetTable(string tableKey)
        {
            if (Sheet.Contains(tableKey))
                return (ConfigFileTable)Sheet[tableKey];
            return ConfigFileResult<ConfigFileTable>.Fail(new ConfigFileError(ConfigFileErrorCode.TableNotFound, $"Table not found: {tableKey}"));
        }

        /// <summary>
        /// 按表键名和配置项键名查询文件项。
        /// </summary>
        /// <param name="tableKey">表键名。</param>
        /// <param name="key">配置项键名。</param>
        /// <returns>找到的文件项；表或配置项不存在时返回对应失败结果。</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="tableKey"/> 为 <c>null</c>。</exception>
        public ConfigFileResult<ConfigFileEntry> GetEntry(string tableKey, string key)
        {
            if (Sheet.Contains(tableKey))
            {
                var table = (ConfigFileTable)Sheet[tableKey];
                return table.GetEntry(key);
            }
            return ConfigFileResult<ConfigFileEntry>.Fail(new ConfigFileError(ConfigFileErrorCode.TableNotFound, $"Table not found: {tableKey}"));
        }

        /// <summary>
        /// 创建尚未加入工作表的文件表。
        /// </summary>
        /// <param name="tableKey">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="description">写出时使用的多语言说明。</param>
        /// <returns>新文件表，或表名非法诊断。</returns>
        public static ConfigFileResult<ConfigFileTable> CreateTable(string tableKey, Translator description)
        {
            var table = ConfigFileTable.Create(tableKey, description);
            if (!table.Success)
                return ConfigFileResult<ConfigFileTable>.Fail(table.Errors);
            return table.Value;
        }

        /// <summary>
        /// 按插入顺序编码所有文件表。
        /// 任一表包含编码错误时不会返回部分文本，而是汇总全部表诊断并返回失败结果。
        /// </summary>
        /// <returns>去除首尾空白后的整文件文本；空工作表编码为空字符串。</returns>
        public ConfigFileResult<string> EncodeSheet()
        {
            var sb = new StringBuilder();
            var result = new ConfigFileResult<string>();
            foreach (ConfigFileTable table in Sheet.Values)
            {
                var tableResult = table.EncodeTable();
                if (tableResult.Success)
                    sb.AppendLine(tableResult.Value);
                if (tableResult.HasErrors)
                    result.AddError(tableResult.Errors);
                sb.AppendLine();
            }

            if (result.HasErrors)
                return ConfigFileResult<string>.Fail(result.Errors);

            result.SetValue(sb.ToString().Trim());
            return result;
        }

        /// <summary>
        /// 从按行拆分后的配置内容解析整个配置模型。
        /// </summary>
        /// <param name="content">配置文件内容行。</param>
        /// <param name="index">读取起点；返回时推进到已消费位置。</param>
        /// <returns>
        /// 已恢复的配置文件模型。单个表解析失败时会收集错误并继续尝试后续内容，
        /// 因而结果可能同时满足 <see cref="ConfigFileResult{T}.Success"/> 和 <see cref="ConfigFileResult{T}.HasErrors"/>。
        /// </returns>
        public static ConfigFileResult<ConfigFileSheet> DecodeSheet(string[] content, ref int index)
        {
            var model = new ConfigFileSheet();
            var result = new ConfigFileResult<ConfigFileSheet>(model);

            while (index < content.Length)
            {
                var tableResult = ConfigFileTable.DecodeTable(content, ref index);
                if (tableResult.Success)
                {
                    var addTableResult = model.AddTable(tableResult.Value);
                    if (!addTableResult.Success)
                        result.AddError(addTableResult.Errors);
                }
                if (tableResult.HasErrors)
                {
                    if (tableResult.Errors.Any(e => e.Code == ConfigFileErrorCode.EndOfContent))
                        break;
                    result.AddError(tableResult.Errors);
                }
            }

            result.SetValue(model);
            return result;
        }
    }
}
