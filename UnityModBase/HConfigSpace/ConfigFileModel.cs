using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 配置文件值的编码/解码工具。
    /// 该类型定义了项目内部配置文本格式的基础规则：字符串带双引号并转义，集合用方括号、元组用圆括号并以逗号分隔元素，数字使用不随系统区域变化的格式。
    /// 它只处理单个值、集合、元组与多元素条目值；<see cref="IsKeyValuePair"/>、<see cref="IsComment"/> 仅提供行分类，
    /// 键值行和表结构的解析由 <see cref="ConfigFileEntry"/>、<see cref="ConfigFileTable"/> 负责。
    /// 类型识别与等值比较等共享规则统一委托 <see cref="EntryModel"/>；内置类型路径不维护共享状态，
    /// 适配器路径会执行 <see cref="IConfigEntryValue"/> 实现代码，其线程安全和副作用由适配器自行保证。
    /// </summary>
    public static partial class ConfigFileModel
    {
        /// <summary>
        /// 将强类型值编码为配置文件中的文本表示。
        /// </summary>
        /// <typeparam name="T">待编码值的静态类型；编码规则不会改用对象的运行时类型。</typeparam>
        /// <param name="value">待编码值，当前格式不支持 <c>null</c>。</param>
        /// <returns>编码结果；不支持的类型会返回失败结果。</returns>
        public static ConfigFileResult<string> Encode<T>(T value)
        {
            return Encode(typeof(T), value);
        }

        /// <summary>
        /// 按指定类型将对象编码为配置文本。
        /// </summary>
        /// <param name="type">用于选择编码规则的声明类型；应与 <paramref name="value"/> 兼容。</param>
        /// <param name="value">待编码对象。</param>
        /// <returns>编码后的字符串，或包含错误信息的失败结果。</returns>
        /// <exception cref="InvalidCastException">内置类型的值与 <paramref name="type"/> 不兼容。</exception>
        public static ConfigFileResult<string> Encode(Type type, object value)
        {
            if (value == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null"));
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Type cannot be null"));

            if (EntryModel.IsEntryMultipleValueType(type))
                return EncodeEntryMultipleValue(type, value);

            if (typeof(IConfigEntryValue).IsAssignableFrom(type))
            {
                try
                {
                    var adapter = (IConfigEntryValue)value;
                    var result = adapter.Encode();
                    if (!result.Success)
                        return ConfigFileResult<string>.Fail(result.Errors);

                    return ConfigFileResult<string>.Ok(result.Value);
                }
                catch (Exception ex)
                {
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to encode using {nameof(IConfigEntryValue)}. Error: {ex.Message}"));
                }
            }

            if (EntryModel.IsPrimitiveType(type))
                return EncodePrimitive(type, value);

            if (EntryModel.IsEnumType(type))
                return ConfigFileResult<string>.Ok(value.ToString());

            if (EntryModel.IsTupleType(type))
                return EncodeTuple(type, value);

            if (EntryModel.IsCollectionType(type))
                return EncodeCollection(type, value);

            return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Unsupported type"));
        }

        /// <summary>
        /// 将配置文本解码为指定类型。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="value">配置文件中的值文本。</param>
        /// <returns>解码后且可直接转换为 <typeparamref name="T"/> 的值，否则返回失败结果。</returns>
        public static ConfigFileResult<T> Decode<T>(string value)
        {
            var result = Decode(typeof(T), value);
            if (!result.Success)
                return ConfigFileResult<T>.Fail(result.Errors);

            if (result.Value is T typedValue)
                return ConfigFileResult<T>.Ok(typedValue);

            return ConfigFileResult<T>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Decoded value cannot be converted to {typeof(T).FullName}"));
        }

        /// <summary>
        /// 按运行时类型解码配置文本。
        /// </summary>
        /// <param name="type">目标声明类型。适配器类型必须提供可访问的无参构造函数。</param>
        /// <param name="value">配置文件中的值文本。</param>
        /// <returns>解码后的对象，或包含错误信息的失败结果。</returns>
        public static ConfigFileResult<object> Decode(Type type, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null or whitespace"));
            if (type == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Type cannot be null"));

            value = value.Trim();

            if (EntryModel.IsEntryMultipleValueType(type))
                return DecodeEntryMultipleValue(type, value);

            if (typeof(IConfigEntryValue).IsAssignableFrom(type))
            {
                try
                {
                    var adapterInstance = (IConfigEntryValue)Activator.CreateInstance(type);
                    var result = adapterInstance.Decode(value);
                    if (!result.Success)
                        return ConfigFileResult<object>.Fail(result.Errors);
                    return ConfigFileResult<object>.Ok(result.Value);
                }
                catch (Exception ex)
                {
                    return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to decode using {nameof(IConfigEntryValue)}. Error: {ex.Message}"));
                }
            }

            if (EntryModel.IsPrimitiveType(type))
                return DecodePrimitive(type, value);

            if (EntryModel.IsEnumType(type))
            {
                try
                {
                    var enumValue = Enum.Parse(type, value, true);
                    return ConfigFileResult<object>.Ok(enumValue);
                }
                catch (Exception ex)
                {
                    return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid enum value: {value}. Error: {ex.Message}"));
                }
            }

            if (EntryModel.IsTupleType(type))
                return DecodeTuple(type, value);

            if (EntryModel.IsCollectionType(type))
                return DecodeCollection(type, value);

            return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Decoding not implemented"));
        }

        /// <summary>
        /// 按顶层逗号拆分复合值文本，是集合与元组拆分共用的词法扫描实现。
        /// 引号内的逗号、括号不视为结构字符；嵌套的 <c>[</c> <c>]</c>、<c>(</c> <c>)</c> 必须配对平衡。
        /// 引号内的转义序列（<c>\\</c>、<c>\"</c>、<c>\n</c>、<c>\r</c>、<c>\t</c>）在此阶段按原文保留，由后续 <see cref="DecodeString"/> 统一反转义。
        /// </summary>
        /// <param name="value">待拆分文本，允许带首尾空白。</param>
        /// <param name="opening">外层起始定界符；与 <paramref name="closing"/> 同为 <c>'\0'</c> 时表示文本无外层定界符（用于 <c>EntryValue</c> 这类顶层逗号分隔编码）。</param>
        /// <param name="closing">外层结束定界符；非 <c>'\0'</c> 时要求文本去除首尾空白后必须被该对定界符完整包裹，拆分前会先去掉这对定界符。</param>
        /// <returns>
        /// 各元素的原始文本（已去首尾空白、未反转义）；去除定界符后内容为空时返回空数组。
        /// 引号未闭合、转义序列非法、括号不平衡或出现空元素（如 <c>[a,,b]</c>）时返回失败。
        /// </returns>
        public static ConfigFileResult<string[]> SplitCompositeString(string value, char opening, char closing)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null or whitespace"));

            value = value.Trim();

            if (opening != '\0' || closing != '\0')
            {
                if (value.Length < 2 || value[0] != opening || value[value.Length - 1] != closing)
                    return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Value must start with '{opening}' and end with '{closing}'"));

                value = value.Substring(1, value.Length - 2).Trim();
            }

            if (value.Length == 0)
                return ConfigFileResult<string[]>.Ok(new string[0]);

            var elements = new List<string>();
            var currentElement = new StringBuilder();

            var brackets = new Stack<char>();

            bool inQuotes = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (inQuotes)
                {
                    if (c == '\\')
                    {
                        if (i + 1 >= value.Length)
                            return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Invalid escape sequence at end of string"));

                        char next = value[i + 1];
                        if (next != '\\' && next != '"' && next != 'n' && next != 'r' && next != 't')
                            return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid escape sequence: \\{next}"));

                        currentElement.Append(c);
                        currentElement.Append(next);

                        i++;
                        continue;
                    }

                    if (c == '"')
                        inQuotes = false;

                    currentElement.Append(c);
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    currentElement.Append(c);
                    continue;
                }

                if (c == '[' || c == '(')
                {
                    brackets.Push(c);
                    currentElement.Append(c);
                    continue;
                }

                if (c == ']' || c == ')')
                {
                    if (brackets.Count == 0)
                        return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Unexpected closing delimiter '{c}'"));

                    char expectedOpening = (c == ']' ? '[' : '(');

                    if (brackets.Peek() != expectedOpening)
                        return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Mismatched closing delimiter '{c}'"));

                    brackets.Pop();

                    currentElement.Append(c);
                    continue;
                }

                if (c == ',' && brackets.Count == 0)
                {
                    var element = currentElement.ToString().Trim();
                    if (element.Length == 0)
                        return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Composite value contains empty element"));

                    elements.Add(element);
                    currentElement.Clear();

                    continue;
                }

                currentElement.Append(c);
            }

            if (inQuotes)
                return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Unclosed quoted string"));

            if (brackets.Count != 0)
                return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Unbalanced nested delimiters"));

            var lastElement = currentElement.ToString().Trim();
            if (lastElement.Length == 0)
                return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Composite value contains empty element"));

            elements.Add(lastElement);

            return ConfigFileResult<string[]>.Ok(elements.ToArray());
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
        public static ConfigFileResult<string> EncodeValueType(Type type)
        {
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null"));

            if (EntryModel.IsEntryMultipleValueType(type))
                return EncodeEntryMultipleValueType(type);

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

            if (EntryModel.IsPrimitiveType(type))
                return EncodePrimitiveType(type);

            if (EntryModel.IsEnumType(type))
                return $"Enum {type.Name}";

            if (EntryModel.IsTupleType(type))
                return EncodeTupleType(type);

            if (EntryModel.IsCollectionType(type))
                return $"{EncodeValueType(EntryModel.GetCollectionElementType(type))}[]";

            return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Unsupported type: {type.FullName}"));
        }

        /// <summary>
        /// 粗略判断一行是否可能是键值对。
        /// 该检查只要求存在等号且不是注释，不校验键名和值；需要可靠结果时应调用 <see cref="ConfigFileEntry.DecodeKeyValuePair"/>。
        /// </summary>
        /// <param name="content">待分类的单行文本；为 <c>null</c> 时返回 <c>false</c>。</param>
        /// <returns>该行是否应进入键值解析流程。</returns>
        public static bool IsKeyValuePair(string content)
        {
            if (content == null)
                return false;
            content = content.Trim();
            return content.Contains('=') && !content.StartsWith("#");
        }

        /// <summary>
        /// 判断忽略行首空白后是否以井号开头。
        /// </summary>
        /// <param name="content">待分类的单行文本；为 <c>null</c> 时返回 <c>false</c>。</param>
        /// <returns>该行是否为配置注释。</returns>
        public static bool IsComment(string content)
        {
            return content != null && content.TrimStart().StartsWith("#");
        }
    }
}
