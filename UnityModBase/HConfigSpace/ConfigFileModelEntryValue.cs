using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    public static partial class ConfigFileModel
    {
        /// <summary>
        /// 将多元素条目值编码为无外层定界符的 v1,v2,...,vN 文本。
        /// 各元素按其声明类型递归调用 <see cref="ConfigFileModel.Encode(Type, object)"/>；
        /// 元素值经 <c>Value1..ValueN</c> 公共只读属性反射读取。任一元素失败时聚合返回全部错误，不产出部分文本。
        /// </summary>
        /// <param name="type">封闭的多元素条目值类型，须满足 <see cref="ValidateEntryValueType"/> 的属性契约。</param>
        /// <param name="value">待编码值；为 <c>null</c> 或与类型不兼容时返回失败。</param>
        public static ConfigFileResult<string> EncodeEntryMultipleValue(Type type, object value)
        {
            var validationResult = ValidateEntryValueType(type);
            if (!validationResult.Success)
                return ConfigFileResult<string>.Fail(validationResult.Errors);

            if (value == null || !type.IsInstanceOfType(value))
            {
                return ConfigFileResult<string>.Fail(
                    new ConfigFileError(
                        ConfigFileErrorCode.InvalidValue,
                        $"Value is not assignable to {type?.FullName ?? "<null>"}"));
            }

            var elementTypes = type.GetGenericArguments();
            var encodedElements = new List<string>(elementTypes.Length);
            var errors = new List<ConfigFileError>();
            var allEncoded = true;

            for (int i = 0; i < elementTypes.Length; i++)
            {
                var propertyName = GetElementPropertyName(i);
                var property = GetElementProperty(type, i);

                if (property == null)
                {
                    allEncoded = false;
                    errors.Add(new ConfigFileError(
                        ConfigFileErrorCode.UnsupportedType,
                        $"Cannot find readable property {propertyName} on {type.FullName}"));
                    continue;
                }

                try
                {
                    var elementValue = property.GetValue(value, null);
                    var elementResult = Encode(elementTypes[i], elementValue);

                    if (!elementResult.Success)
                    {
                        allEncoded = false;
                        errors.AddRange(elementResult.Errors);
                        continue;
                    }

                    encodedElements.Add(elementResult.Value);
                }
                catch (Exception ex)
                {
                    allEncoded = false;
                    errors.Add(new ConfigFileError(
                        ConfigFileErrorCode.InvalidValue,
                        $"Failed to encode element {propertyName} of {type.FullName}. Error: {ex.Message}"));
                }
            }

            if (!allEncoded)
                return ConfigFileResult<string>.Fail(errors);

            return ConfigFileResult<string>.Ok(string.Join(",", encodedElements.ToArray()));
        }

        /// <summary>
        /// 从无外层定界符的 v1,v2,...,vN 文本解码多元素条目值。
        /// 以 <c>'\0'</c> 作为起止定界符调用 <see cref="SplitCompositeString"/>，即按顶层逗号拆分（引号内逗号不受影响）；
        /// 拆分数量必须与泛型元素数一致，各元素按声明类型递归解码后，
        /// 经与元素数匹配的公共构造函数按声明顺序重建实例。任一元素失败时聚合返回全部错误。
        /// </summary>
        /// <param name="type">封闭的多元素条目值类型，须提供与泛型参数匹配的公共构造函数。</param>
        /// <param name="value">已去除首尾空白的等号右侧文本。</param>
        public static ConfigFileResult<object> DecodeEntryMultipleValue(Type type, string value)
        {
            var validationResult = ValidateEntryValueType(type);
            if (!validationResult.Success)
                return ConfigFileResult<object>.Fail(validationResult.Errors);

            var splitResult = SplitCompositeString(value, '\0', '\0');
            if (!splitResult.Success)
                return ConfigFileResult<object>.Fail(splitResult.Errors);

            var elementTypes = type.GetGenericArguments();
            if (splitResult.Value.Length != elementTypes.Length)
            {
                return ConfigFileResult<object>.Fail(
                    new ConfigFileError(
                        ConfigFileErrorCode.InvalidValue,
                        $"Expected {elementTypes.Length} values"));
            }

            var decodedElements = new object[elementTypes.Length];
            var errors = new List<ConfigFileError>();
            var allDecoded = true;

            for (int i = 0; i < elementTypes.Length; i++)
            {
                var elementResult = Decode(elementTypes[i], splitResult.Value[i]);

                if (!elementResult.Success)
                {
                    allDecoded = false;
                    errors.AddRange(elementResult.Errors);
                    continue;
                }

                decodedElements[i] = elementResult.Value;
            }

            if (!allDecoded)
                return ConfigFileResult<object>.Fail(errors);

            try
            {
                return ConfigFileResult<object>.Ok(Activator.CreateInstance(type, decodedElements));
            }
            catch (Exception ex)
            {
                return ConfigFileResult<object>.Fail(
                    new ConfigFileError(
                        ConfigFileErrorCode.InvalidValue,
                        $"Failed to create entry value {type.FullName}. Error: {ex.Message}"));
            }
        }

        /// <summary>
        /// 生成多元素条目的类型提示：各元素提示按 <see cref="ConfigFileModel.EncodeValueType(Type)"/> 递归生成后以逗号拼接。
        /// 提示仅写入文件注释供人工阅读，不参与解码校验。
        /// </summary>
        /// <param name="type">封闭的多元素条目值类型。</param>
        public static ConfigFileResult<string> EncodeEntryMultipleValueType(Type type)
        {
            var validationResult = ValidateEntryValueType(type);
            if (!validationResult.Success)
                return ConfigFileResult<string>.Fail(validationResult.Errors);

            var elementTypes = type.GetGenericArguments();
            var typeHints = new List<string>(elementTypes.Length);
            var errors = new List<ConfigFileError>();
            var allEncoded = true;

            for (int i = 0; i < elementTypes.Length; i++)
            {
                var typeResult = EncodeValueType(elementTypes[i]);

                if (!typeResult.Success)
                {
                    allEncoded = false;
                    errors.AddRange(typeResult.Errors);
                    continue;
                }

                typeHints.Add(typeResult.Value);
            }

            if (!allEncoded)
                return ConfigFileResult<string>.Fail(errors);

            return ConfigFileResult<string>.Ok(string.Join(",", typeHints.ToArray()));
        }

        /// <summary>
        /// 校验类型是否为可编解码的多元素条目值：必须通过 <see cref="EntryModel.IsEntryMultipleValueType(Type)"/>
        /// （封闭、实现 <see cref="IEntryMultipleValue"/>、元素非嵌套且受支持），
        /// 且逐个元素存在与泛型参数类型一致、无索引参数的公共可读实例属性（见 <see cref="GetElementPropertyName"/>）。
        /// 属性与构造函数的反射契约由本类型与解码路径共同依赖，实现方不可偏离。
        /// </summary>
        /// <param name="type">待校验类型。</param>
        /// <returns>校验通过时返回原类型；否则返回带诊断的失败结果。</returns>
        private static ConfigFileResult<Type> ValidateEntryValueType(Type type)
        {
            if (!EntryModel.IsEntryMultipleValueType(type))
            {
                return ConfigFileResult<Type>.Fail(
                    new ConfigFileError(
                        ConfigFileErrorCode.UnsupportedType,
                        $"Type {type?.FullName ?? "<null>"} is not a closed multiple entry value type"));
            }

            var elementTypes = type.GetGenericArguments();

            for (int i = 0; i < elementTypes.Length; i++)
            {
                var propertyName = GetElementPropertyName(i);
                var property = GetElementProperty(type, i);

                if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    return ConfigFileResult<Type>.Fail(
                        new ConfigFileError(
                            ConfigFileErrorCode.UnsupportedType,
                            $"Type {type.FullName} must expose readable property {propertyName}"));
                }

                if (property.PropertyType != elementTypes[i])
                {
                    return ConfigFileResult<Type>.Fail(
                        new ConfigFileError(
                            ConfigFileErrorCode.UnsupportedType,
                            $"Property {propertyName} type does not match generic argument {elementTypes[i].FullName}"));
                }
            }

            return ConfigFileResult<Type>.Ok(type);
        }

        /// <summary>多元素条目值第 <paramref name="index"/> 个元素（从 0 计）的固定属性名约定：Value1、Value2、…。</summary>
        private static string GetElementPropertyName(int index)
        {
            return "Value" + (index + 1);
        }

        /// <summary>按属性名约定查找公共实例属性；不存在时返回 <c>null</c>，由调用方给出诊断。</summary>
        private static PropertyInfo GetElementProperty(Type type, int index)
        {
            return type.GetProperty(GetElementPropertyName(index), BindingFlags.Instance | BindingFlags.Public);
        }
    }
}
