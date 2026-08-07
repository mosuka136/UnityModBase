using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityModBase.HTranslatorSpace;
using static UnityModBase.HConfigSpace.ConfigFileModel;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 表示配置文件中的一个键值项及其写出时附带的说明元数据。
    /// 该类型只维护文件层面的字符串表示，强类型值的校验和运行时事件由 <see cref="ConfigEntry{T}"/> 负责。
    /// 解码现有文件时只恢复键和值，名称、说明、默认值和类型提示会在运行时绑定阶段重新生成。
    /// 实例可变且不提供并发保护。
    /// </summary>
    public class ConfigFileEntry
    {
        private string _key;

        /// <summary>
        /// 写入配置文件的多语言名称注释；解析已有文件时当前不会从注释中恢复该值。
        /// </summary>
        public Translator Name { get; set; }
        /// <summary>
        /// 写入配置文件的多语言说明注释；解析已有文件时当前不会从注释中恢复该值。
        /// </summary>
        public Translator Description { get; set; }
        /// <summary>
        /// 配置项键名，只允许 Unicode 字母、数字和下划线，以避免与配置文件语法冲突。
        /// </summary>
        /// <exception cref="ArgumentException">赋值不符合键名语法。</exception>
        public string Key
        {
            get => _key;

            set
            {
                if (IsValidKeyName(value))
                    _key = value;
                else
                    throw new ArgumentException($"Invalid key name: {value}. Key names must be non-empty and can only contain letters, digits, and underscores.");
            }
        }
        /// <summary>
        /// 配置文件中的值文本，约定使用 <see cref="ConfigFileModel"/> 规则编码。
        /// 属性赋值本身不验证强类型格式；<see cref="ConfigEntry{T}"/> 在建立或替换运行时绑定时才按声明类型解码。
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// 默认值的编码文本，用作配置文件注释，不参与运行时回退逻辑。
        /// </summary>
        public string DefaultValue { get; set; }
        /// <summary>
        /// 配置值类型的说明文本，用于帮助人工编辑配置文件。
        /// </summary>
        public string ValueType { get; set; }
        /// <summary>
        /// 可接受值的说明文本，通常由枚举成员名生成，仅用于人工编辑提示。
        /// </summary>
        public string AcceptableValues { get; set; }

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
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 将类型提示编码为 <c># Value Type:</c> 注释。
        /// </summary>
        /// <returns>类型提示注释；未设置提示时为空字符串。</returns>
        public ConfigFileResult<string> EncodeValueType()
        {
            if (string.IsNullOrEmpty(ValueType))
                return string.Empty;
            return $"# Value Type: {ValueType}";
        }

        /// <summary>
        /// 将可接受值提示编码为 <c># Acceptable Values:</c> 注释。
        /// </summary>
        /// <returns>可接受值注释；未设置提示时为空字符串。</returns>
        public ConfigFileResult<string> EncodeAcceptableValues()
        {
            if (string.IsNullOrEmpty(AcceptableValues))
                return string.Empty;
            return $"# Acceptable Values: {AcceptableValues}";
        }

        /// <summary>
        /// 将声明默认值编码为 <c># Default Value:</c> 注释，不影响当前值。
        /// </summary>
        /// <returns>默认值注释；未设置默认值文本时为空字符串。</returns>
        public ConfigFileResult<string> EncodeDefaultValue()
        {
            if (string.IsNullOrEmpty(DefaultValue))
                return string.Empty;
            return $"# Default Value: {DefaultValue}";
        }

        /// <summary>
        /// 编码必需的 <c>Key = Value</c> 行，并校验键名和值非空白。
        /// </summary>
        /// <returns>键值行，或键名/值非法诊断。</returns>
        public ConfigFileResult<string> EncodeKeyValuePair()
        {
            if (!IsValidKeyName(_key))
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidKeyName, $"Invalid key name: {Key}"));
            if (string.IsNullOrWhiteSpace(Value))
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Value cannot be empty for key: {Key}"));
            return $"{Key} = {Value}";
        }

        /// <summary>
        /// 将配置项编码为完整文件片段，包含可选注释和必需的键值行。
        /// </summary>
        /// <returns>成功时为可直接写入配置文件的文本片段；键名或值非法时返回失败结果。</returns>
        public ConfigFileResult<string> EncodeEntry()
        {
            var nameResult = EncodeName();
            if (!nameResult.Success)
                return ConfigFileResult<string>.Fail(nameResult.Errors);

            var descriptionResult = EncodeDescription();
            if (!descriptionResult.Success)
                return ConfigFileResult<string>.Fail(descriptionResult.Errors);

            var valueTypeResult = EncodeValueType();
            if (!valueTypeResult.Success)
                return ConfigFileResult<string>.Fail(valueTypeResult.Errors);

            var acceptableValuesResult = EncodeAcceptableValues();
            if (!acceptableValuesResult.Success)
                return ConfigFileResult<string>.Fail(acceptableValuesResult.Errors);

            var defaultValueResult = EncodeDefaultValue();
            if (!defaultValueResult.Success)
                return ConfigFileResult<string>.Fail(defaultValueResult.Errors);

            var keyValuePairResult = EncodeKeyValuePair();
            if (!keyValuePairResult.Success)
                return ConfigFileResult<string>.Fail(keyValuePairResult.Errors);

            var sb = new StringBuilder();
            if (nameResult.Value != string.Empty)
                sb.AppendLine(nameResult.Value);
            if (descriptionResult.Value != string.Empty)
                sb.AppendLine(descriptionResult.Value);
            if (valueTypeResult.Value != string.Empty)
                sb.AppendLine(valueTypeResult.Value);
            if (acceptableValuesResult.Value != string.Empty)
                sb.AppendLine(acceptableValuesResult.Value);
            if (defaultValueResult.Value != string.Empty)
                sb.AppendLine(defaultValueResult.Value);
            sb.AppendLine(keyValuePairResult.Value);

            return sb.ToString().Trim();
        }

        /// <summary>
        /// 将当前项的元数据复制到另一个文件项。
        /// 名称和说明按引用浅复制；<paramref name="overrideValue"/> 为 <c>false</c> 时目标当前值是唯一保留字段。
        /// </summary>
        /// <param name="target">目标配置项。</param>
        /// <param name="overrideValue">是否连同当前值一起覆盖目标值；重载配置时通常应为 <c>false</c>，以保留用户编辑。</param>
        /// <returns>目标存在且复制完成时返回 <c>true</c>。</returns>
        /// <exception cref="ArgumentException">当前项尚未设置合法键名，导致目标键赋值失败。</exception>
        public bool CopyTo(ConfigFileEntry target, bool overrideValue)
        {
            if (target == null)
                return false;

            target.Name = Name;
            target.Description = Description;
            target.Key = Key;
            target.DefaultValue = DefaultValue;
            target.ValueType = ValueType;
            target.AcceptableValues = AcceptableValues;
            if (overrideValue)
                target.Value = Value;
            return true;
        }

        /// <summary>
        /// 判断键名是否符合配置文件语法约束：非空且仅包含 Unicode 字母、数字或下划线。
        /// </summary>
        /// <param name="key">待检查的键名。</param>
        /// <returns>键名是否可以安全写入键值行。</returns>
        public static bool IsValidKeyName(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && key.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        /// <summary>
        /// 粗略判断一行是否可能是键值对。
        /// 该检查只要求存在等号且不是注释，不校验键名和值；需要可靠结果时应调用 <see cref="DecodeKeyValuePair"/>。
        /// </summary>
        /// <param name="content">待分类的单行文本。</param>
        /// <returns>该行是否应进入键值解析流程。</returns>
        /// <exception cref="NullReferenceException"><paramref name="content"/> 为 <c>null</c>。</exception>
        public static bool IsKeyValuePair(string content)
        {
            content = content.Trim();
            return content.Contains('=') && !content.StartsWith("#");
        }

        /// <summary>
        /// 判断忽略行首空白后是否以井号开头。
        /// </summary>
        /// <param name="content">待分类的单行文本。</param>
        /// <returns>该行是否为配置注释。</returns>
        /// <exception cref="NullReferenceException"><paramref name="content"/> 为 <c>null</c>。</exception>
        public static bool IsComment(string content)
        {
            return content.TrimStart().StartsWith("#");
        }

        /// <summary>
        /// 使用 <see cref="ConfigFileModel"/> 的静态类型规则编码配置值。
        /// </summary>
        /// <typeparam name="T">配置值的声明类型。</typeparam>
        /// <param name="value">待编码值；<c>null</c> 不受支持。</param>
        /// <returns>可写入等号右侧的文本，或类型/值诊断。</returns>
        public static ConfigFileResult<string> EncodeValue<T>(T value)
        {
            return Encode(value);
        }

        /// <summary>
        /// 按泛型声明类型生成人工可读的类型提示。
        /// </summary>
        /// <typeparam name="T">配置值的声明类型。</typeparam>
        /// <returns>类型提示，或不受支持的类型诊断。</returns>
        public static ConfigFileResult<string> EncodeValueType<T>()
        {
            return EncodeValueType(typeof(T));
        }

        /// <summary>
        /// 按运行时类型生成人工可读的配置值类型提示。
        /// 集合会在元素类型提示后追加 <c>[]</c>；当前实现不会传播嵌套元素类型的失败结果，不受支持的泛型元素可能退化为空类型提示 <c>[]</c>。
        /// </summary>
        /// <param name="type">配置值声明类型；适配器类型必须提供可访问的无参构造函数。</param>
        /// <returns>内置类型、枚举、集合或适配器的类型提示；非集合的其他类型返回失败结果。</returns>
        /// <exception cref="NullReferenceException"><paramref name="type"/> 为空，或集合元素类型无法识别。</exception>
        public static ConfigFileResult<string> EncodeValueType(Type type)
        {
            if (typeof(IConfigEntryValue).IsAssignableFrom(type))
            {
                try
                {
                    var adapterInstance = (IConfigEntryValue)Activator.CreateInstance(type);
                    var result = adapterInstance.EncodeValueType();
                    if (!result.Success)
                        return ConfigFileResult<string>.Fail(result.Errors);

                    return result;
                }
                catch (Exception ex)
                {
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, $"Failed to encode value type for {type.FullName}: {ex.Message}"));
                }
            }

            switch (type)
            {
                case Type t when t == typeof(string):
                    return "String";
                case Type t when t == typeof(sbyte):
                    return "Int8";
                case Type t when t == typeof(short):
                    return "Int16";
                case Type t when t == typeof(int):
                    return "Int32";
                case Type t when t == typeof(long):
                    return "Int64";
                case Type t when t == typeof(byte):
                    return "UInt8";
                case Type t when t == typeof(ushort):
                    return "UInt16";
                case Type t when t == typeof(uint):
                    return "UInt32";
                case Type t when t == typeof(ulong):
                    return "UInt64";
                case Type t when t == typeof(float):
                    return "Float";
                case Type t when t == typeof(double):
                    return "Double";
                case Type t when t == typeof(bool):
                    return "Boolean";
                default:
                    break;
            }

            if (type.IsEnum)
                return $"Enum {type.Name}";

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return $"{EncodeValueType(GetCollectionElementType(type))}[]";

            return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Unsupported type: {type.FullName}"));
        }

        /// <summary>
        /// 获取枚举或枚举集合允许的成员名提示。
        /// </summary>
        /// <typeparam name="T">枚举或枚举集合类型。</typeparam>
        /// <returns>逗号分隔的枚举成员名；非枚举元素类型返回失败结果。</returns>
        public static ConfigFileResult<string> EncodeAcceptableValues<T>()
        {
            return EncodeAcceptableValues(typeof(T));
        }

        /// <summary>
        /// 按运行时类型获取枚举配置允许的成员名提示。
        /// </summary>
        /// <param name="type">枚举或枚举集合类型。</param>
        /// <returns>逗号分隔的枚举成员名；非枚举元素类型返回失败结果。</returns>
        /// <exception cref="NullReferenceException"><paramref name="type"/> 为空，或集合元素类型无法识别。</exception>
        public static ConfigFileResult<string> EncodeAcceptableValues(Type type)
        {
            if (typeof(IEnumerable).IsAssignableFrom(type))
                return EncodeAcceptableValues(GetCollectionElementType(type));

            if (!type.IsEnum)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, $"Type is not an enum: {type.FullName}"));

            var enumNames = Enum.GetNames(type);
            var acceptableValues = string.Join(", ", enumNames);
            return acceptableValues;
        }

        /// <summary>
        /// 从强类型当前值和默认值创建文件项，并生成名称、说明及类型元数据。
        /// 当前实现不自动填充 <see cref="AcceptableValues"/>，枚举绑定会由 <see cref="ConfigEntry{T}"/> 补充该提示。
        /// </summary>
        /// <typeparam name="T">配置值的声明类型。</typeparam>
        /// <param name="key">配置项键名。</param>
        /// <param name="value">当前值。</param>
        /// <param name="defaultValue">声明默认值。</param>
        /// <param name="name">多语言展示名称。</param>
        /// <param name="description">多语言说明。</param>
        /// <returns>初始化完成的文件项，或首个编码/校验诊断。</returns>
        public static ConfigFileResult<ConfigFileEntry> CreateEntry<T>(string key, T value, T defaultValue, Translator name, Translator description)
        {
            if (!IsValidKeyName(key))
                return ConfigFileResult<ConfigFileEntry>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidKeyName, $"Invalid key name: {key}"));

            var encodedValueResult = EncodeValue(value);
            if (!encodedValueResult.Success)
                return ConfigFileResult<ConfigFileEntry>.Fail(encodedValueResult.Errors);

            var encodedDefaultValueResult = EncodeValue(defaultValue);
            if (!encodedDefaultValueResult.Success)
                return ConfigFileResult<ConfigFileEntry>.Fail(encodedDefaultValueResult.Errors);

            var encodedValueTypeResult = EncodeValueType<T>();
            if (!encodedValueTypeResult.Success)
                return ConfigFileResult<ConfigFileEntry>.Fail(encodedValueTypeResult.Errors);

            var entry = new ConfigFileEntry
            {
                Name = name,
                Description = description,
                Key = key,
                Value = encodedValueResult.Value,
                DefaultValue = encodedDefaultValueResult.Value,
                ValueType = encodedValueTypeResult.Value
            };

            return entry;
        }

        /// <summary>
        /// 按泛型声明类型解码等号右侧的配置文本。
        /// </summary>
        /// <typeparam name="T">目标配置值类型。</typeparam>
        /// <param name="value">待解码的值文本。</param>
        /// <returns>强类型值，或格式/类型诊断。</returns>
        public static ConfigFileResult<T> DecodeValue<T>(string value)
        {
            return Decode<T>(value);
        }

        /// <summary>
        /// 解析单行键值对。
        /// </summary>
        /// <param name="content">形如 <c>Key = Value</c> 的配置行，值部分可包含额外的等号。</param>
        /// <returns>移除键和值首尾空白后的字符串。</returns>
        /// <exception cref="NullReferenceException"><paramref name="content"/> 为 <c>null</c>。</exception>
        public static ConfigFileResult<(string, string)> DecodeKeyValuePair(string content)
        {
            if (!IsKeyValuePair(content))
                return ConfigFileResult<(string, string)>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidKeyValuePair, $"Invalid key-value pair: {content}"));

            var parts = content.Split(new[] { '=' }, 2);
            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (!IsValidKeyName(key))
                return ConfigFileResult<(string, string)>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidKeyName, $"Invalid key name: {key}"));

            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<(string, string)>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Value cannot be empty for key: {key}"));

            return (key, value);
        }

        /// <summary>
        /// 从当前位置开始解析下一个配置项。
        /// 空行和注释会被跳过且不会恢复为元数据；遇到第一个非法内容行时会消费该行并立即返回失败。
        /// </summary>
        /// <param name="content">按行拆分后的配置文件内容。</param>
        /// <param name="index">读取起点；返回时会推进到已消费内容之后。</param>
        /// <returns>仅包含键和值的文件项；到达结尾时返回 <see cref="ConfigFileErrorCode.EndOfContent"/>。</returns>
        public static ConfigFileResult<ConfigFileEntry> DecodeEntry(string[] content, ref int index)
        {
            // 解析阶段只信任实际键值行；注释用于人工阅读，启动后会由运行时声明重新写入最新元数据。
            for (; index < content.Length; index++)
            {
                var line = content[index];

                if (IsComment(line) || string.IsNullOrWhiteSpace(line))
                    continue;

                if (IsKeyValuePair(line))
                {
                    var keyValuePairResult = DecodeKeyValuePair(line);
                    index++;
                    if (!keyValuePairResult.Success)
                        return ConfigFileResult<ConfigFileEntry>.Fail(keyValuePairResult.Errors);

                    var entryResult = new ConfigFileEntry
                    {
                        Key = keyValuePairResult.Value.Item1,
                        Value = keyValuePairResult.Value.Item2
                    };

                    return entryResult;
                }
                else
                {
                    index++;
                    return ConfigFileResult<ConfigFileEntry>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidKeyValuePair, $"Invalid key-value pair: {line}"));
                }
            }

            return ConfigFileResult<ConfigFileEntry>.Fail(new ConfigFileError(ConfigFileErrorCode.EndOfContent, "No more content to process"));
        }
    }
}
