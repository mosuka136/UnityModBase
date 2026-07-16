using System;
using System.Collections.Generic;

namespace UnityModBase.HotkeyManager
{
    /// <summary>
    /// 表示热键解析操作的值或有序错误集合。错误列表在构造时复制，之后不会随调用方集合变化。
    /// </summary>
    /// <typeparam name="T">成功结果的值类型。</typeparam>
    public class HotkeyResult<T>
    {
        /// <summary>
        /// 成功时的返回值；失败时为该类型默认值。
        /// </summary>
        public T Value { get; private set; }
        /// <summary>
        /// 当前结果是否成功。
        /// </summary>
        public bool Success { get; private set; }
        /// <summary>
        /// 失败原因集合；成功时为空集合而不是 <c>null</c>。
        /// </summary>
        public IReadOnlyList<string> Errors { get; private set; }

        /// <summary>
        /// 创建默认失败状态；值为类型默认值，错误集合为空。
        /// </summary>
        public HotkeyResult()
        {
            Errors = Array.Empty<string>();
        }

        /// <summary>
        /// 使用显式值、状态和错误创建结果。
        /// </summary>
        /// <param name="value">结果值。</param>
        /// <param name="success">操作是否成功。</param>
        /// <param name="errors">错误集合；<c>null</c> 或空集合会规范化为空只读集合，其余内容会被复制。</param>
        public HotkeyResult(T value, bool success, IReadOnlyList<string> errors)
        {
            Value = value;
            Success = success;
            if (errors == null || errors.Count == 0)
                Errors = Array.Empty<string>();
            else
                Errors = new List<string>(errors).AsReadOnly();
        }

        /// <summary>
        /// 创建不含错误的成功结果。
        /// </summary>
        /// <param name="value">成功值。</param>
        /// <returns>成功结果。</returns>
        public static HotkeyResult<T> Ok(T value)
        {
            return new HotkeyResult<T>(value, true, Array.Empty<string>());
        }

        /// <summary>
        /// 从错误列表创建失败结果。
        /// </summary>
        /// <param name="errors">错误列表；<c>null</c> 会转换为空集合。</param>
        /// <returns>值为类型默认值的失败结果。</returns>
        public static HotkeyResult<T> Fail(IReadOnlyList<string> errors)
        {
            return new HotkeyResult<T>(default, false, errors);
        }

        /// <summary>
        /// 从可变参数错误文本创建失败结果。
        /// </summary>
        /// <param name="errors">按顺序保存的错误文本。</param>
        /// <returns>值为类型默认值的失败结果。</returns>
        public static HotkeyResult<T> Fail(params string[] errors)
        {
            return new HotkeyResult<T>(default, false, errors);
        }

        /// <summary>
        /// 创建以主错误开头、随后按参数顺序合并附加错误列表的失败结果。
        /// </summary>
        /// <param name="error">首条错误文本。</param>
        /// <param name="errors">要依次追加的错误列表；列表项不可为 <c>null</c>。</param>
        /// <returns>合并后的失败结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="errors"/> 中包含 <c>null</c> 列表时抛出。</exception>
        public static HotkeyResult<T> Fail(string error, params IReadOnlyList<string>[] errors)
        {
            var allErrors = new List<string> { error };
            foreach (var errorList in errors)
            {
                allErrors.AddRange(errorList);
            }
            return new HotkeyResult<T>(default, false, allErrors);
        }

        /// <summary>
        /// 将值转换为不含错误的成功结果。
        /// </summary>
        /// <param name="value">成功值。</param>
        /// <returns>包含该值的成功结果。</returns>
        public static implicit operator HotkeyResult<T>(T value)
        {
            return Ok(value);
        }

        /// <summary>
        /// 返回结果值的文本；值为 <c>null</c> 时返回空字符串，不输出错误列表。
        /// </summary>
        /// <returns>值文本或空字符串。</returns>
        public override string ToString()
        {
            return Value?.ToString() ?? string.Empty;
        }
    }
}
