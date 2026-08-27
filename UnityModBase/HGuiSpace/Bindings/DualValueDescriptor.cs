using System;
using System.Reflection;

namespace UnityModBase.HGuiSpace.Bindings
{
    /// <summary>
    /// 描述一个具有两个泛型参数、两个只读元素属性和双参数构造函数的值容器，
    /// 使通用 GUI 无需依赖具体业务模型即可投影双元素槽位。
    /// </summary>
    public sealed class DualValueDescriptor
    {
        /// <summary>获取双元素值的开放泛型类型定义。</summary>
        public Type GenericTypeDefinition { get; }

        /// <summary>获取第一个元素的属性名。</summary>
        public string Value1PropertyName { get; }

        /// <summary>获取第二个元素的属性名。</summary>
        public string Value2PropertyName { get; }

        /// <summary>创建双元素值类型描述。</summary>
        public DualValueDescriptor(
            Type genericTypeDefinition,
            string value1PropertyName,
            string value2PropertyName)
        {
            if (genericTypeDefinition == null)
                throw new ArgumentNullException(nameof(genericTypeDefinition));
            if (!genericTypeDefinition.IsGenericTypeDefinition || genericTypeDefinition.GetGenericArguments().Length != 2)
                throw new ArgumentException("Dual value type must be an open generic type with exactly two generic parameters.", nameof(genericTypeDefinition));
            if (string.IsNullOrWhiteSpace(value1PropertyName))
                throw new ArgumentException("Property name cannot be null or whitespace.", nameof(value1PropertyName));
            if (string.IsNullOrWhiteSpace(value2PropertyName))
                throw new ArgumentException("Property name cannot be null or whitespace.", nameof(value2PropertyName));
            if (!HasReadableProperty(genericTypeDefinition, value1PropertyName))
                throw new ArgumentException($"Readable property '{value1PropertyName}' was not found.", nameof(value1PropertyName));
            if (!HasReadableProperty(genericTypeDefinition, value2PropertyName))
                throw new ArgumentException($"Readable property '{value2PropertyName}' was not found.", nameof(value2PropertyName));

            GenericTypeDefinition = genericTypeDefinition;
            Value1PropertyName = value1PropertyName;
            Value2PropertyName = value2PropertyName;
        }

        /// <summary>判断指定运行时类型是否由本描述表示。</summary>
        public bool CanDescribe(Type valueType)
        {
            return valueType != null &&
                   valueType.IsGenericType &&
                   !valueType.ContainsGenericParameters &&
                   valueType.GetGenericTypeDefinition() == GenericTypeDefinition;
        }

        internal Type GetSlotType(Type valueType, int slotIndex)
        {
            ValidateValueTypeAndSlot(valueType, slotIndex);
            return valueType.GetGenericArguments()[slotIndex];
        }

        internal PropertyInfo GetSlotProperty(Type valueType, int slotIndex)
        {
            ValidateValueTypeAndSlot(valueType, slotIndex);
            return valueType.GetProperty(slotIndex == 0 ? Value1PropertyName : Value2PropertyName);
        }

        internal object CreateValue(Type valueType, object value1, object value2)
        {
            if (!CanDescribe(valueType))
                throw new ArgumentException($"Value type is not described by {GenericTypeDefinition.FullName}.", nameof(valueType));
            return Activator.CreateInstance(valueType, value1, value2);
        }

        internal static bool TryCreateConventional(Type valueType, out DualValueDescriptor descriptor)
        {
            descriptor = null;
            if (valueType == null || !valueType.IsGenericType || valueType.ContainsGenericParameters)
                return false;

            try
            {
                descriptor = new DualValueDescriptor(
                    valueType.GetGenericTypeDefinition(),
                    "Value1",
                    "Value2");
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void ValidateValueTypeAndSlot(Type valueType, int slotIndex)
        {
            if (!CanDescribe(valueType))
                throw new ArgumentException($"Value type is not described by {GenericTypeDefinition.FullName}.", nameof(valueType));
            if (slotIndex != 0 && slotIndex != 1)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        private static bool HasReadableProperty(Type type, string propertyName)
        {
            return type.GetProperty(propertyName)?.CanRead == true;
        }
    }
}
