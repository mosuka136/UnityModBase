using System;

namespace UnityModBase.HConfigGUI
{
    public static class TypeConvert
    {
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

        public static T To<T>(object value)
        {
            return (T)To(value, typeof(T));
        }

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
