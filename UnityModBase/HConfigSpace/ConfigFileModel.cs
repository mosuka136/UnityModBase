using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 配置文件值的编码/解码工具。
    /// 该类型定义了项目内部配置文本格式的基础规则：字符串带双引号并转义，集合用方括号、元组用圆括号并以逗号分隔元素，数字使用不随系统区域变化的格式。
    /// 它只处理单个值、集合与元组；<see cref="IsKeyValuePair"/>、<see cref="IsComment"/>、<see cref="IsValidKeyName"/> 仅提供行分类与键名校验，
    /// 键值行和表结构的解析由 <see cref="ConfigFileEntry"/>、<see cref="ConfigFileTable"/> 负责。
    /// 内置类型路径不维护共享状态；适配器路径会执行 <see cref="IConfigEntryValue"/> 实现代码，其线程安全和副作用由适配器自行保证。
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

            if (IsPrimitiveType(type))
                return EncodePrimitive(type, value);

            if (type.IsEnum)
                return ConfigFileResult<string>.Ok(value.ToString());

            if (IsTupleType(type))
                return EncodeTuple(type, value);

            if (typeof(IEnumerable).IsAssignableFrom(type))
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

            if (IsPrimitiveType(type))
                return DecodePrimitive(type, value);

            if (type.IsEnum)
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

            if (IsTupleType(type))
                return DecodeTuple(type, value);

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return DecodeCollection(type, value);

            return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Decoding not implemented"));
        }

        /// <summary>
        /// 按顶层逗号拆分复合值文本，是集合与元组拆分共用的词法扫描实现。
        /// 引号内的逗号、括号不视为结构字符；嵌套的 <c>[</c> <c>]</c>、<c>(</c> <c>)</c> 必须配对平衡。
        /// 引号内的转义序列（<c>\\</c>、<c>\"</c>、<c>\n</c>、<c>\r</c>、<c>\t</c>）在此阶段按原文保留，由后续 <see cref="DecodeString"/> 统一反转义。
        /// </summary>
        /// <param name="value">待拆分文本，允许带首尾空白。</param>
        /// <param name="opening">外层起始定界符；与 <paramref name="closing"/> 同为 <c>'\0'</c> 时表示文本无外层定界符（用于 <c>ConfigEntryValue</c> 这类顶层逗号分隔编码）。</param>
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
        /// 按配置模型的等值规则比较两个值，供 <see cref="ConfigEntry{T}"/> 判断“值未变化”以跳过写入与事件。
        /// 两者的运行时类型必须相同，否则直接视为不相等。原始类型、字符串、枚举按 <see cref="object.Equals(object, object)"/> 比较；
        /// 数组与 <see cref="IEnumerable"/> 按元素顺序递归深度比较；<see cref="IConfigEntryValue"/> 委托给其实现的业务等值判断。
        /// 其余类型一律视为不相等，使配置项按“值已变化”保守处理。
        /// </summary>
        /// <param name="a">第一个待比较值，可为 <c>null</c>。</param>
        /// <param name="b">第二个待比较值，可为 <c>null</c>；仅两者同为 <c>null</c> 时相等。</param>
        /// <returns>类型相同且内容符合上述规则时返回 <c>true</c>。</returns>
        /// <remarks>
        /// 数组分支必须先于 <see cref="IEnumerable"/> 判断（数组本身实现了 <see cref="IEnumerable"/>），以便用长度快速排除不等；
        /// <see cref="IConfigEntryValue"/> 分支先于 <see cref="IEnumerable"/>，保证同时可枚举的自定义值类型使用其业务等值语义。
        /// 枚举序列会被完整遍历，且枚举器在可释放时会被释放；调用方不应传入无限序列或枚举有破坏性副作用的源。
        /// </remarks>
        public static bool ValueEqual(object a, object b)
        {
            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            var type = a.GetType();

            if (type != b.GetType())
                return false;

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return object.Equals(a, b);

            if (type.IsArray)
            {
                var arrayA = a as Array;
                var arrayB = b as Array;

                if (arrayA == null || arrayB == null)
                    return false;

                if (arrayA.Length != arrayB.Length)
                    return false;

                for (int i = 0; i < arrayA.Length; i++)
                {
                    if (!ValueEqual(arrayA.GetValue(i), arrayB.GetValue(i)))
                        return false;
                }

                return true;
            }

            if (typeof(IConfigEntryValue).IsAssignableFrom(type))
            {
                var valueA = a as IConfigEntryValue;
                var valueB = b as IConfigEntryValue;

                if (valueA == null || valueB == null)
                    return false;

                return valueA.Equals(valueB);
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var enumA = (a as IEnumerable)?.GetEnumerator();
                var enumB = (b as IEnumerable)?.GetEnumerator();

                if (enumA == null || enumB == null)
                    return false;

                try
                {
                    while (true)
                    {
                        var hasNextA = enumA.MoveNext();
                        var hasNextB = enumB.MoveNext();

                        if (hasNextA != hasNextB)
                            return false;

                        if (!hasNextA)
                            break;

                        if (!ValueEqual(enumA.Current, enumB.Current))
                            return false;
                    }

                    return true;
                }
                finally
                {
                    (enumA as IDisposable)?.Dispose();
                    (enumB as IDisposable)?.Dispose();
                }
            }

            return false;
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

            if (IsPrimitiveType(type))
                return EncodePrimitiveType(type);

            if (type.IsEnum)
                return $"Enum {type.Name}";

            if (IsTupleType(type))
                return EncodeTupleType(type);

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return $"{EncodeValueType(GetCollectionElementType(type))}[]";

            return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Unsupported type: {type.FullName}"));
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
