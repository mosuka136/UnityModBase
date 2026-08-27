using System;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 为值编辑器统一解析条目当前应显示的值，并区分“原始输入回显”和“可安全用于业务控件的有效值”。
    /// 本工具只读取缓冲区和已提交值，不会触发提交或清理状态。
    /// </summary>
    public static class ValueProvider
    {
        /// <summary>
        /// 获取最后一次暂存输入；没有暂存输入时返回已提交值。
        /// 无效输入也会原样返回，适合文本框保留用户尚未完成的内容。
        /// </summary>
        /// <param name="entry">要读取的条目绑定。</param>
        /// <returns>最新暂存输入或已提交值。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public static object GetValue(IEntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.EditBuffer.IsUsing)
            {
                var latestEntry = entry.EditBuffer.GetLatestValue();
                if (!latestEntry.IsEmpty)
                    return latestEntry.Value;
            }

            return entry.Value;
        }

        /// <summary>
        /// 获取当前回显值并转换为指定类型。转换规则和异常行为由 <see cref="TypeConvert"/> 定义。
        /// </summary>
        /// <typeparam name="T">目标值类型。</typeparam>
        /// <param name="entry">要读取的条目绑定。</param>
        /// <returns>转换后的当前回显值。</returns>
        public static T GetValue<T>(IEntryBinding entry)
        {
            return TypeConvert.To<T>(GetValue(entry));
        }

        /// <summary>
        /// 获取最后一次有效暂存值；最新输入无效或不存在时回退到已提交值。
        /// 适合滑条、开关等不能消费中间无效状态的控件。
        /// </summary>
        /// <param name="entry">要读取的条目绑定。</param>
        /// <returns>最新有效暂存值或已提交值。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public static object GetValidValue(IEntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.EditBuffer.IsUsing)
            {
                var latestEntry = entry.EditBuffer.GetLatestValue();
                if (!latestEntry.IsEmpty && latestEntry.IsValid)
                    return latestEntry.Value;
            }

            return entry.Value;
        }

        /// <summary>
        /// 获取当前有效值并转换为指定类型。转换规则和异常行为由 <see cref="TypeConvert"/> 定义。
        /// </summary>
        /// <typeparam name="T">目标值类型。</typeparam>
        /// <param name="entry">要读取的条目绑定。</param>
        /// <returns>转换后的当前有效值。</returns>
        public static T GetValidValue<T>(IEntryBinding entry)
        {
            return TypeConvert.To<T>(GetValidValue(entry));
        }
    }
}
