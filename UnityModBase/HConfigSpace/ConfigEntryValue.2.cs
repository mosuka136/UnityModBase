using System;
using System.Collections.Generic;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 双元素元组的配置值适配器，把 <see cref="Value1"/>、<see cref="Value2"/> 编码为形如 <c>v1,v2</c> 的顶层逗号分隔文本。
    /// 与 <see cref="ConfigFileModel"/> 原生 <see cref="ConfigFileModel.EncodeTuple(Type, object)"/> 的圆括号元组不同，本格式不使用外层定界符，
    /// 解码时以 <c>'\0'</c> 作为起止定界符调用 <see cref="ConfigFileModel.SplitCompositeString"/>，即按顶层逗号直接拆分。
    /// 由于该格式无法区分内外层逗号，本类型通过 <see cref="IGenericConfigEntryValue"/> 标记禁止作为另一双元素配置项的直接元素类型。
    /// </summary>
    /// <typeparam name="T1">第一个元素的值类型。</typeparam>
    /// <typeparam name="T2">第二个元素的值类型。</typeparam>
    public class ConfigEntryValue<T1, T2> : IConfigEntryValue, IGenericConfigEntryValue
    {
        /// <summary>
        /// 第一个元素的值。
        /// </summary>
        public T1 Value1 { get; }

        /// <summary>
        /// 第二个元素的值。
        /// </summary>
        public T2 Value2 { get; }

        /// <summary>
        /// 创建两个元素均为类型默认值的实例。
        /// 该无参构造函数是 <see cref="IConfigEntryValue"/> 契约的一部分：框架通过 <see cref="Activator.CreateInstance(Type)"/>
        /// 创建临时实例后再调用 <see cref="Decode"/> / <see cref="EncodeValueType"/>，因此不可移除。
        /// 返回实例仅用于框架回调，其默认元素值没有业务含义。
        /// </summary>
        public ConfigEntryValue()
        {
            Value1 = default;
            Value2 = default;
        }

        /// <summary>
        /// 创建指定元素值的实例。
        /// </summary>
        /// <param name="value1">第一个元素的值。</param>
        /// <param name="value2">第二个元素的值。</param>
        public ConfigEntryValue(T1 value1, T2 value2)
        {
            Value1 = value1;
            Value2 = value2;
        }

        /// <summary>
        /// 将形如 <c>v1,v2</c> 的文本解码为实例，各元素按 <typeparamref name="T1"/>、<typeparamref name="T2"/> 递归解码。
        /// 文本必须恰好拆分为两个元素，否则返回失败。
        /// </summary>
        /// <param name="content">顶层逗号分隔的双值文本。</param>
        /// <returns>解码出的实例；拆分失败或任一元素解码失败时返回失败结果。</returns>
        public ConfigFileResult<object> Decode(string content)
        {
            // 起止定界符均为 '\0' 表示文本无外层包裹，仅按顶层逗号拆分（见 SplitCompositeString 的无定界符模式）。
            var splitResult = ConfigFileModel.SplitCompositeString(content, '\0', '\0');
            if (!splitResult.Success)
                return ConfigFileResult<object>.Fail(splitResult.Errors);

            var splitString = splitResult.Value;
            if (splitString.Length != 2)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Expected two values"));

            var value1Result = ConfigFileModel.Decode<T1>(splitString[0]);
            var value2Result = ConfigFileModel.Decode<T2>(splitString[1]);
            if (!value1Result.Success || !value2Result.Success)
            {
                var errors = new List<ConfigFileError>(value1Result.Errors);
                errors.AddRange(value2Result.Errors);
                return ConfigFileResult<object>.Fail(errors);
            }

            return new ConfigEntryValue<T1, T2>(value1Result.Value, value2Result.Value);
        }

        /// <summary>
        /// 将两个元素编码为形如 <c>v1,v2</c> 的文本，各元素按声明类型递归编码。
        /// </summary>
        /// <returns>编码文本；任一元素编码失败时聚合返回全部错误。</returns>
        public ConfigFileResult<string> Encode()
        {
            var value1Result = ConfigFileModel.Encode(Value1);
            var value2Result = ConfigFileModel.Encode(Value2);
            if (!value1Result.Success || !value2Result.Success)
            {
                var errors = new List<ConfigFileError>(value1Result.Errors);
                errors.AddRange(value2Result.Errors);
                return ConfigFileResult<string>.Fail(errors);
            }
            return $"{value1Result.Value},{value2Result.Value}";
        }

        /// <summary>
        /// 生成形如 <c>T1,T2</c> 的类型提示，元素提示按 <see cref="ConfigFileModel.EncodeValueType(Type)"/> 递归生成。
        /// </summary>
        /// <returns>双元素的类型提示文本。</returns>
        public ConfigFileResult<string> EncodeValueType()
        {
            var type1Result = ConfigFileModel.EncodeValueType(typeof(T1));
            var type2Result = ConfigFileModel.EncodeValueType(typeof(T2));
            if (!type1Result.Success || !type2Result.Success)
            {
                var errors = new List<ConfigFileError>(type1Result.Errors);
                errors.AddRange(type2Result.Errors);
                return ConfigFileResult<string>.Fail(errors);
            }
            return $"{type1Result.Value},{type2Result.Value}";
        }

        /// <summary>
        /// 按元素分别用 <see cref="ConfigFileModel.ValueEqual"/> 深度比较，仅在类型一致且两个元素都相等时视为相等。
        /// </summary>
        /// <param name="other">待比较的另一个配置值。</param>
        /// <returns>两者是否等价。</returns>
        public bool Equals(IConfigEntryValue other)
        {
            if (other == null || other.GetType() != typeof(ConfigEntryValue<T1, T2>))
                return false;

            var otherValue = (ConfigEntryValue<T1, T2>)other;
            return ConfigFileModel.ValueEqual(otherValue.Value1, Value1) && ConfigFileModel.ValueEqual(otherValue.Value2, Value2);
        }
    }
}
