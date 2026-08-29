using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    public static partial class ConfigFileModel
    {
        /// <summary>
        /// 拆分形如 <c>(a,b)</c> 的元组文本，词法规则与集合拆分一致（引号、转义、嵌套括号）。
        /// </summary>
        /// <param name="value">待拆分的元组文本，必须被圆括号完整包裹。</param>
        /// <returns>各元素的原始文本；格式非法时返回失败。</returns>
        public static ConfigFileResult<string[]> SplitTupleString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Tuple string cannot be null or empty"));
            else
                return SplitCompositeString(value, '(', ')');
        }

        /// <summary>
        /// 将 ValueTuple 编码为形如 <c>(a,b)</c> 的元组文本，各元素按声明类型递归编码。
        /// 仅支持不超过 7 个直接元素的元组：8 元及以上的 ValueTuple 把第 8 个泛型参数设计为嵌套的 Rest 字段，
        /// 不存在 Item8 实例字段，本格式不展开该嵌套结构。
        /// </summary>
        /// <param name="type">元组声明类型，用于确定元素类型与数量。</param>
        /// <param name="value">待编码元组；通过反射读取 <c>Item1</c> 至 <c>ItemN</c> 公共实例字段。</param>
        /// <returns>编码文本；类型非元组、元素过多或任一元素编码失败时返回失败。</returns>
        public static ConfigFileResult<string> EncodeTuple(Type type, object value)
        {
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null"));
            if (value == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null"));
            if (!EntryModel.IsTupleType(type))
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Type {type.FullName} is not a ValueTuple"));

            var elementTypes = type.GetGenericArguments();
            if (elementTypes.Length > 7)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "ValueTuple with more than 7 direct elements is not supported"));

            var elements = new List<string>();
            for (int i = 0; i < elementTypes.Length; i++)
            {
                var field = type.GetField("Item" + (i + 1), BindingFlags.Instance | BindingFlags.Public);
                if (field == null)
                    return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Cannot find tuple field Item{i + 1}"));

                var item = field.GetValue(value);

                var result = Encode(elementTypes[i], item);
                if (!result.Success)
                    return ConfigFileResult<string>.Fail(result.Errors);

                elements.Add(result.Value);
            }

            return ConfigFileResult<string>.Ok($"({string.Join(",", elements)})");
        }

        /// <summary>
        /// 将形如 <c>(a,b)</c> 的元组文本解码为 ValueTuple 实例。
        /// 文本中的元素数量必须与声明类型的泛型参数数量严格一致；各元素按对应类型递归解码后，
        /// 通过 <see cref="Activator.CreateInstance(Type, object[])"/> 按位置构造元组。与 <see cref="EncodeTuple"/> 一样不支持超过 7 个直接元素的元组。
        /// </summary>
        /// <param name="type">目标元组类型。</param>
        /// <param name="value">被圆括号包裹的元组文本。</param>
        /// <returns>构造出的元组实例；格式非法、元素数量不匹配或构造抛异常时返回失败。</returns>
        public static ConfigFileResult<object> DecodeTuple(Type type, string value)
        {
            if (type == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null"));
            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Tuple value cannot be null or empty"));
            if (!EntryModel.IsTupleType(type))
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Type {type.FullName} is not a ValueTuple"));

            var elementTypes = type.GetGenericArguments();
            if (elementTypes.Length > 7)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "ValueTuple with more than 7 direct elements is not supported"));

            var splitResult = SplitTupleString(value);
            if (!splitResult.Success)
                return ConfigFileResult<object>.Fail(splitResult.Errors);

            var sourceElements = splitResult.Value;
            if (sourceElements.Length != elementTypes.Length)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Tuple element count mismatch. Expected {elementTypes.Length}, actual {sourceElements.Length}"));

            var elements = new object[elementTypes.Length];
            for (int i = 0; i < elementTypes.Length; i++)
            {
                var result = Decode(elementTypes[i], sourceElements[i].Trim());

                if (!result.Success)
                    return ConfigFileResult<object>.Fail(result.Errors);

                elements[i] = result.Value;
            }

            try
            {
                var tuple = Activator.CreateInstance(type, elements);
                return ConfigFileResult<object>.Ok(tuple);
            }
            catch (Exception ex)
            {
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to create tuple {type.FullName}. Error: {ex.Message}"));
            }
        }

        /// <summary>
        /// 生成元组的人工可读类型提示，形如 <c>(Int32,String)</c>，元素提示按 <see cref="EncodeValueType(Type)"/> 递归生成。
        /// 与编解码路径一致，仅支持不超过 7 个直接元素的元组。
        /// </summary>
        /// <param name="type">元组声明类型。</param>
        /// <returns>类型提示；类型非元组、元素过多或任一元素类型提示生成失败时返回失败。</returns>
        public static ConfigFileResult<string> EncodeTupleType(Type type)
        {
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidType, "Type cannot be null"));
            if (!EntryModel.IsTupleType(type))
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Type {type.FullName} is not a ValueTuple"));

            var elementTypes = type.GetGenericArguments();
            if (elementTypes.Length > 7)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "ValueTuple with more than 7 direct elements is not supported"));

            var encodedTypes = new List<string>();
            foreach (var elementType in elementTypes)
            {
                var result = EncodeValueType(elementType);
                if (!result.Success)
                    return ConfigFileResult<string>.Fail(result.Errors);
                encodedTypes.Add(result.Value);
            }

            return ConfigFileResult<string>.Ok($"({string.Join(",", encodedTypes)})");
        }
    }
}
