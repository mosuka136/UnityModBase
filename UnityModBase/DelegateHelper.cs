using System;
using System.Collections.Generic;

namespace UnityModBase
{
    /// <summary>
    /// 为事件派发提供空值安全的调用列表快照，便于调用方逐个隔离订阅者异常。
    /// </summary>
    public static class DelegateHelper
    {
        /// <summary>
        /// 按注册顺序枚举委托当前时刻的调用列表；委托为 <c>null</c> 时返回空序列。
        /// </summary>
        /// <typeparam name="T">委托类型。</typeparam>
        /// <param name="del">要快照的多播委托，可为 <c>null</c>。</param>
        /// <returns>与后续订阅或退订无关的调用列表快照。</returns>
        /// <exception cref="InvalidCastException">调用列表中的委托无法转换为 <typeparamref name="T"/> 时抛出。</exception>
        public static IEnumerable<T> GetInvocationListOrEmpty<T>(this T del) where T : Delegate
        {
            if (del == null)
                yield break;

            foreach (var d in del.GetInvocationList())
                yield return d as T ?? throw new InvalidCastException($"Cannot cast delegate of type {d.GetType().FullName} to {typeof(T).FullName}.");
        }
    }
}
