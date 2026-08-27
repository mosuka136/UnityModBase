using System;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 提供可编辑 GUI 输入所需的有限类型转换：整数类型、float、double、布尔、字符串及枚举；不包含 decimal、char 或自定义类型。
    /// 基础类型遵循 <see cref="Convert"/> 的当前区域性和溢出规则；不尝试通用类型转换器或自定义解析协议。
    /// </summary>
    public static class TypeConvert
    {
        /// <summary>
        /// 将非 null 输入转换为目标类型；已可赋值的实例会原样返回。
        /// 字符串枚举名称按 <see cref="Enum.Parse(Type, string)"/> 的区分大小写规则解析。
        /// </summary>
        /// <param name="value">待转换的非 null 值。</param>
        /// <param name="targetType">目标运行时类型。</param>
        /// <returns>目标类型的值；输入已可赋值时返回原实例。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> 或 <paramref name="targetType"/> 为 null。</exception>
        /// <exception cref="ArgumentException">枚举文本或数值无法按目标枚举类型解析。</exception>
        /// <exception cref="FormatException">输入文本不符合目标基础类型格式。</exception>
        /// <exception cref="OverflowException">数值超出目标类型范围。</exception>
        /// <exception cref="InvalidCastException">目标类型不在支持范围内。</exception>
        public static object To(object value, Type targetType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            switch (targetType)
            {
                case Type t when t == typeof(byte):
                    return Convert.ToByte(value);
                case Type t when t == typeof(sbyte):
                    return Convert.ToSByte(value);
                case Type t when t == typeof(short):
                    return Convert.ToInt16(value);
                case Type t when t == typeof(ushort):
                    return Convert.ToUInt16(value);
                case Type t when t == typeof(int):
                    return Convert.ToInt32(value);
                case Type t when t == typeof(uint):
                    return Convert.ToUInt32(value);
                case Type t when t == typeof(long):
                    return Convert.ToInt64(value);
                case Type t when t == typeof(ulong):
                    return Convert.ToUInt64(value);
                case Type t when t == typeof(float):
                    return Convert.ToSingle(value);
                case Type t when t == typeof(double):
                    return Convert.ToDouble(value);
                case Type t when t == typeof(bool):
                    return Convert.ToBoolean(value);
                case Type t when t == typeof(string):
                    return Convert.ToString(value);
                default:
                    break;
            }

            if (targetType.IsEnum)
            {
                if (value is string strValue)
                    return Enum.Parse(targetType, strValue);
                else
                    return Enum.ToObject(targetType, value);
            }

            throw new InvalidCastException($"Cannot convert from {value.GetType()} to {targetType}");
        }

        /// <summary>
        /// 将输入转换为 <typeparamref name="T"/>。
        /// </summary>
        /// <typeparam name="T">目标值类型。</typeparam>
        /// <param name="value">待转换值。</param>
        /// <returns>转换后的 <typeparamref name="T"/> 值。</returns>
        public static T To<T>(object value)
        {
            return (T)To(value, typeof(T));
        }

        /// <summary>
        /// 尝试按 <see cref="To(object, Type)"/> 的规则转换。
        /// 格式、类型、参数或溢出错误返回 false；目标类型为 null 仍视为调用错误并抛出异常。
        /// </summary>
        /// <param name="value">待转换值。</param>
        /// <param name="targetType">目标运行时类型。</param>
        /// <param name="result">成功时为转换值，失败时为 null。</param>
        /// <returns>转换成功时返回 true。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="targetType"/> 为 null。</exception>
        public static bool TryTo(object value, Type targetType, out object result)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            try
            {
                result = To(value, targetType);
                return true;
            }
            catch (ArgumentException)
            {
            }
            catch (FormatException)
            {
            }
            catch (InvalidCastException)
            {
            }
            catch (OverflowException)
            {
            }

            result = null;
            return false;
        }

        /// <summary>
        /// 尝试转换为 <typeparamref name="T"/>；失败时输出该类型的默认值。
        /// </summary>
        /// <typeparam name="T">目标值类型。</typeparam>
        /// <param name="value">待转换值。</param>
        /// <param name="result">成功时为转换值，失败时为 <typeparamref name="T"/> 的默认值。</param>
        /// <returns>转换成功时返回 true。</returns>
        public static bool TryTo<T>(object value, out T result)
        {
            if (TryTo(value, typeof(T), out var convertedValue))
            {
                result = (T)convertedValue;
                return true;
            }

            result = default;
            return false;
        }
    }
}
