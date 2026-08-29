using System;
using System.Globalization;

namespace UnityModBase.HConfigSpace
{
    public static partial class ConfigFileModel
    {
        /// <summary>
        /// 将基础类型值编码为配置文本。
        /// 数值与布尔统一使用 <see cref="CultureInfo.InvariantCulture"/>，保证配置文件内容不随运行机器的系统区域设置变化；
        /// 字符串委托 <see cref="EncodeString(string, bool, bool, bool)"/> 加引号并转义。
        /// </summary>
        /// <param name="type">基础类型的声明类型，用于选择编码分支。</param>
        /// <param name="value">待编码值，运行时必须能强制转换为 <paramref name="type"/>。</param>
        /// <returns>编码文本；类型不受支持或值为 <c>null</c> 时返回失败。</returns>
        /// <exception cref="InvalidCastException"><paramref name="value"/> 的运行时类型与 <paramref name="type"/> 不兼容。</exception>
        public static ConfigFileResult<string> EncodePrimitive(Type type, object value)
        {
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null."));
            if (value == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null."));

            switch (type)
            {
                case Type t when t == typeof(string):
                    return ConfigFileResult<string>.Ok(EncodeString((string)value));
                case Type t when t == typeof(sbyte):
                    return ConfigFileResult<string>.Ok(((sbyte)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(short):
                    return ConfigFileResult<string>.Ok(((short)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(int):
                    return ConfigFileResult<string>.Ok(((int)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(long):
                    return ConfigFileResult<string>.Ok(((long)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(byte):
                    return ConfigFileResult<string>.Ok(((byte)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(ushort):
                    return ConfigFileResult<string>.Ok(((ushort)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(uint):
                    return ConfigFileResult<string>.Ok(((uint)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(ulong):
                    return ConfigFileResult<string>.Ok(((ulong)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(float):
                    return ConfigFileResult<string>.Ok(((float)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(double):
                    return ConfigFileResult<string>.Ok(((double)value).ToString(CultureInfo.InvariantCulture));
                case Type t when t == typeof(bool):
                    return ConfigFileResult<string>.Ok(((bool)value).ToString(CultureInfo.InvariantCulture));
                default:
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Type '{type.FullName}' is not a supported primitive type."));
            }
        }

        /// <summary>
        /// 将配置文本解码为基础类型值。
        /// 数值使用 <see cref="CultureInfo.InvariantCulture"/> 解析，与编码侧保持同一区域无关约定；
        /// 字符串委托 <see cref="DecodeString"/> 处理引号与转义。解析失败一律返回 <see cref="ConfigFileErrorCode.InvalidValue"/> 失败结果，不向调用方抛转换异常。
        /// </summary>
        /// <param name="type">目标基础类型。</param>
        /// <param name="value">已去除首尾空白的值文本；字符串值应保留外层引号。</param>
        /// <returns>解码后的装箱值；类型不受支持或文本格式非法时返回失败。</returns>
        public static ConfigFileResult<object> DecodePrimitive(Type type, string value)
        {
            if (type == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null."));

            switch (type)
            {
                case Type t when t == typeof(string):
                    return DecodeString(value);
                case Type t when t == typeof(sbyte):
                    if (sbyte.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var sbyteResult))
                        return ConfigFileResult<object>.Ok(sbyteResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid sbyte value: {value}"));
                case Type t when t == typeof(short):
                    if (short.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var shortResult))
                        return ConfigFileResult<object>.Ok(shortResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid short value: {value}"));
                case Type t when t == typeof(int):
                    if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intResult))
                        return ConfigFileResult<object>.Ok(intResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid int value: {value}"));
                case Type t when t == typeof(long):
                    if (long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var longResult))
                        return ConfigFileResult<object>.Ok(longResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid long value: {value}"));
                case Type t when t == typeof(byte):
                    if (byte.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var byteResult))
                        return ConfigFileResult<object>.Ok(byteResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid byte value: {value}"));
                case Type t when t == typeof(ushort):
                    if (ushort.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ushortResult))
                        return ConfigFileResult<object>.Ok(ushortResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid ushort value: {value}"));
                case Type t when t == typeof(uint):
                    if (uint.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var uintResult))
                        return ConfigFileResult<object>.Ok(uintResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid uint value: {value}"));
                case Type t when t == typeof(ulong):
                    if (ulong.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ulongResult))
                        return ConfigFileResult<object>.Ok(ulongResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid ulong value: {value}"));
                case Type t when t == typeof(float):
                    if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var floatResult))
                        return ConfigFileResult<object>.Ok(floatResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid float value: {value}"));
                case Type t when t == typeof(double):
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleResult))
                        return ConfigFileResult<object>.Ok(doubleResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid double value: {value}"));
                case Type t when t == typeof(bool):
                    if (bool.TryParse(value, out var boolResult))
                        return ConfigFileResult<object>.Ok(boolResult);
                    else
                        return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Invalid bool value: {value}"));
                default:
                    return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Type '{type.FullName}' is not a supported primitive type."));
            }
        }

        /// <summary>
        /// 生成基础类型的人工可读类型提示（如 <c>Int32</c>、<c>String</c>），写入配置文件注释供人工编辑参考。
        /// 名称是本配置格式的独立约定，与反射类型名无关，不参与解码校验。
        /// </summary>
        /// <param name="type">基础类型的声明类型。</param>
        /// <returns>类型提示；类型不受支持时返回失败。</returns>
        public static ConfigFileResult<string> EncodePrimitiveType(Type type)
        {
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null."));

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
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Type '{type.FullName}' is not a supported primitive type."));
            }
        }
    }
}
