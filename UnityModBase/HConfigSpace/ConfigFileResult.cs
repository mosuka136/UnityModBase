using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 配置文件解析和编码流程使用的轻量结果类型。
    /// 它允许调用方在不抛异常的情况下同时携带值和多个诊断；<see cref="Success"/> 与 <see cref="HasErrors"/> 相互独立，
    /// 因而“成功且包含错误”表示流程保留了可用的部分结果。调用方应根据场景同时检查这两个状态。
    /// </summary>
    /// <typeparam name="T">结果可携带的值类型。</typeparam>
    public class ConfigFileResult<T>
    {
        /// <summary>
        /// 当前携带的值。是否可作为完整结果使用由 <see cref="Success"/> 决定；显式状态构造函数也允许失败结果携带非默认值。
        /// </summary>
        public T Value { get; private set; }

        /// <summary>
        /// 当前结果是否携带调用方可继续使用的值；为 <c>true</c> 时仍可能存在可恢复错误。
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// 是否至少包含一条诊断，不等同于 <see cref="Success"/> 的反值。
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// 处理过程中收集的只读诊断快照；始终非 <c>null</c>，并且可以与成功值同时存在。
        /// </summary>
        public IReadOnlyList<ConfigFileError> Errors { get; private set; }

        /// <summary>
        /// 创建尚未设置值的失败结果，错误集合为空。
        /// </summary>
        public ConfigFileResult()
        {
            Errors = Array.Empty<ConfigFileError>();
        }

        /// <summary>
        /// 创建不含错误的成功结果。
        /// </summary>
        /// <param name="value">结果值，允许为 <c>null</c>。</param>
        public ConfigFileResult(T value)
        {
            Value = value;
            Success = true;
            Errors = Array.Empty<ConfigFileError>();
        }

        /// <summary>
        /// 使用显式状态创建结果，并复制错误列表以隔离调用方后续修改。
        /// </summary>
        /// <param name="value">结果携带的值。</param>
        /// <param name="success">该值是否可供调用方继续使用。</param>
        /// <param name="errors">诊断列表；为 <c>null</c> 时按空列表处理。</param>
        public ConfigFileResult(T value, bool success, IReadOnlyList<ConfigFileError> errors)
        {
            Value = value;
            Success = success;
            if (errors == null || errors.Count == 0)
                Errors = Array.Empty<ConfigFileError>();
            else
                Errors = new List<ConfigFileError>(errors).AsReadOnly();
        }

        /// <summary>
        /// 设置结果值并标记为成功；已有诊断会保留，允许表达部分成功。
        /// </summary>
        /// <param name="value">新的结果值，允许为 <c>null</c>。</param>
        public void SetValue(T value)
        {
            Value = value;
            Success = true;
            if (Errors == null)
                Errors = Array.Empty<ConfigFileError>();
        }

        /// <summary>
        /// 按原顺序追加诊断，但不改变 <see cref="Success"/> 状态。
        /// </summary>
        /// <param name="errors">待追加的诊断。</param>
        /// <exception cref="ArgumentNullException"><paramref name="errors"/> 为 <c>null</c>。</exception>
        public void AddError(IReadOnlyList<ConfigFileError> errors)
        {
            var errorList = new List<ConfigFileError>(Errors ?? Array.Empty<ConfigFileError>());
            errorList.AddRange(errors);
            Errors = errorList.AsReadOnly();
        }

        /// <summary>
        /// 按原顺序追加诊断，但不改变 <see cref="Success"/> 状态。
        /// </summary>
        /// <param name="errors">待追加的诊断。</param>
        /// <exception cref="ArgumentNullException"><paramref name="errors"/> 为 <c>null</c>。</exception>
        public void AddError(params ConfigFileError[] errors)
        {
            var errorList = new List<ConfigFileError>(Errors ?? Array.Empty<ConfigFileError>());
            errorList.AddRange(errors);
            Errors = errorList.AsReadOnly();
        }

        /// <summary>
        /// 创建不含错误的成功结果。
        /// </summary>
        /// <param name="value">结果值。</param>
        /// <returns>携带指定值的成功结果。</returns>
        public static ConfigFileResult<T> Ok(T value)
        {
            return new ConfigFileResult<T>
            {
                Value = value,
                Success = true,
                Errors = Array.Empty<ConfigFileError>()
            };
        }

        /// <summary>
        /// 创建不携带有效值的失败结果，并复制错误列表。
        /// </summary>
        /// <param name="errors">失败诊断；为 <c>null</c> 时使用空列表。</param>
        /// <returns>值为 <typeparamref name="T"/> 默认值的失败结果。</returns>
        public static ConfigFileResult<T> Fail(IReadOnlyList<ConfigFileError> errors)
        {
            return new ConfigFileResult<T>(default, false, errors);
        }

        /// <summary>
        /// 创建不携带有效值的失败结果，并复制错误数组。
        /// </summary>
        /// <param name="errors">失败诊断；为 <c>null</c> 时使用空列表。</param>
        /// <returns>值为 <typeparamref name="T"/> 默认值的失败结果。</returns>
        public static ConfigFileResult<T> Fail(params ConfigFileError[] errors)
        {
            return new ConfigFileResult<T>(default, false, errors);
        }

        /// <summary>
        /// 返回当前值的文本表示；值为 <c>null</c> 时返回空字符串，不检查成功状态。
        /// </summary>
        /// <returns>当前值的文本表示或空字符串。</returns>
        public override string ToString()
        {
            return Value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 将普通值包装为不含诊断的成功结果。
        /// </summary>
        /// <param name="value">待包装的值。</param>
        /// <returns>成功结果。</returns>
        public static implicit operator ConfigFileResult<T>(T value)
        {
            return Ok(value);
        }

        /// <summary>
        /// 从成功结果中提取值。
        /// </summary>
        /// <param name="result">待提取的结果。</param>
        /// <returns>结果携带的值。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="result"/> 为失败状态。</exception>
        /// <exception cref="NullReferenceException"><paramref name="result"/> 为 <c>null</c>。</exception>
        public static explicit operator T(ConfigFileResult<T> result)
        {
            if (!result.Success)
                throw new InvalidOperationException("Cannot convert a failed ConfigFileResult to its value.");

            return result.Value;
        }

        /// <summary>
        /// 将结果转换为对象结果，并复制当前诊断快照。
        /// </summary>
        /// <param name="result">待转换的强类型结果。</param>
        /// <returns>保持值、成功状态和现有诊断的对象结果。</returns>
        /// <exception cref="NullReferenceException"><paramref name="result"/> 为 <c>null</c>。</exception>
        public static implicit operator ConfigFileResult<object>(ConfigFileResult<T> result)
        {
            return new ConfigFileResult<object>(result.Value, result.Success, result.Errors);
        }
    }

    /// <summary>
    /// 配置文件处理中的单条结构化诊断，用错误码支持流程判断，并保留日志所需的来源信息。
    /// </summary>
    public class ConfigFileError
    {
        /// <summary>
        /// 机器可判定的错误类型。
        /// </summary>
        public ConfigFileErrorCode Code { get; private set; }
        /// <summary>
        /// 面向日志的错误说明。
        /// </summary>
        public string Message { get; private set; }
        /// <summary>
        /// 创建错误对象的调用成员名，用于快速定位错误来源。
        /// </summary>
        public string Caller { get; private set; }

        /// <summary>
        /// 创建配置文件诊断；未显式提供来源时由编译器填入调用成员名。
        /// </summary>
        /// <param name="code">机器可判定的错误类型。</param>
        /// <param name="message">面向日志的具体说明。</param>
        /// <param name="caller">诊断来源成员名。</param>
        public ConfigFileError(ConfigFileErrorCode code, string message, [CallerMemberName] string caller = "")
        {
            Code = code;
            Message = message;
            Caller = caller;
        }

        /// <summary>
        /// 返回适合日志记录的完整诊断文本。
        /// </summary>
        /// <returns>包含错误码、消息和来源成员名的文本。</returns>
        public override string ToString()
        {
            return $"[{Code}] {Message} (Caller: {Caller})";
        }

        /// <summary>
        /// 获取与 <see cref="ToString"/> 相同的完整诊断文本。
        /// </summary>
        /// <returns>包含错误码、消息和来源成员名的文本。</returns>
        public string GetFullMessage()
        {
            return $"[{Code}] {Message} (Caller: {Caller})";
        }
    }

    /// <summary>
    /// 配置文件解析/编码阶段可识别的诊断类别。
    /// 部分代码（例如 <see cref="EndOfContent"/>）用于控制增量解析流程，并不一定代表文件损坏。
    /// </summary>
    public enum ConfigFileErrorCode
    {
        /// <summary>
        /// 声明类型没有可用的编码、解码或集合构造路径。
        /// </summary>
        UnsupportedType,

        /// <summary>
        /// 值为空、格式非法或无法赋给目标类型。
        /// </summary>
        InvalidValue,

        /// <summary>
        /// 配置行不符合键值对语法。
        /// </summary>
        InvalidKeyValuePair,

        /// <summary>
        /// 配置项键名非法或在同一表内重复。
        /// </summary>
        InvalidKeyName,

        /// <summary>
        /// 表键名非法或在同一工作表内重复。
        /// </summary>
        InvalidTableName,

        /// <summary>
        /// 查询的配置项不存在，或调用方传入了空配置项。
        /// </summary>
        EntryNotFound,

        /// <summary>
        /// 查询的配置表不存在，或调用方传入了空配置表。
        /// </summary>
        TableNotFound,

        /// <summary>
        /// 首个有效内容行不是方括号表头。
        /// </summary>
        InvalidTableHeader,

        /// <summary>
        /// 增量解析器已消费完输入；上层通常用它正常结束表或文件循环。
        /// </summary>
        EndOfContent,

        /// <summary>
        /// 类型不满足当前操作要求，或自定义类型说明生成失败。
        /// </summary>
        InvalidType,
    }
}
