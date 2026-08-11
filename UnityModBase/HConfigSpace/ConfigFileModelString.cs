using System;
using System.Text;

namespace UnityModBase.HConfigSpace
{
    public static partial class ConfigFileModel
    {
        /// <summary>
        /// 按配置格式解码字符串。
        /// <c>null</c>、空字符串和纯空白会在引号校验之前统一解码为空字符串，这是当前格式的兼容规则。
        /// </summary>
        /// <param name="value">待解码文本。</param>
        /// <param name="quote">是否要求文本以双引号包裹。</param>
        /// <param name="trim">是否在解码前去掉首尾空白。</param>
        /// <param name="escape">是否解析反斜杠转义。</param>
        /// <returns>解码后的字符串。</returns>
        public static ConfigFileResult<string> DecodeString(string value, bool quote = true, bool trim = true, bool escape = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<string>.Ok(string.Empty);

            if (trim)
                value = value.Trim();

            if (quote)
            {
                if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);
                else
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "String must start and end with a quote"));
            }

            if (escape)
            {
                var unescapeResult = UnescapeString(value);
                if (!unescapeResult.Success)
                    return unescapeResult;

                value = unescapeResult.Value;
            }

            return ConfigFileResult<string>.Ok(value);
        }

        /// <summary>
        /// 按配置格式编码字符串。
        /// </summary>
        /// <param name="value">待编码字符串；<c>null</c> 按空字符串处理。</param>
        /// <param name="quote">是否用双引号包裹。</param>
        /// <param name="trim">是否在编码前去掉首尾空白。</param>
        /// <param name="escape">是否转义反斜杠、双引号和常见控制字符。</param>
        /// <returns>编码后的字符串。</returns>
        public static string EncodeString(string value, bool quote = true, bool trim = false, bool escape = true)
        {
            if (value == null)
                value = string.Empty;

            if (trim)
                value = value.Trim();
            if (escape)
                value = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            if (quote)
                value = $"\"{value}\"";
            return value;
        }

        /// <summary>
        /// 解析配置字符串中的转义序列。
        /// 仅支持本配置格式写出的反斜杠、双引号、换行、回车和制表符转义。
        /// </summary>
        /// <param name="value">不含外层引号的字符串内容；为 <c>null</c> 时返回失败结果。</param>
        /// <returns>解析后的字符串；出现未识别或截断的转义序列时返回失败。</returns>
        public static ConfigFileResult<string> UnescapeString(string value)
        {
            if (value == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, value));

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (i + 1 >= value.Length)
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Invalid escape sequence at end of string"));

                i++;
                switch (value[i])
                {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid escape sequence: \\{value[i]}"));
                }
            }

            return ConfigFileResult<string>.Ok(builder.ToString());
        }
    }
}
