using System;
using System.Collections.Generic;

namespace UnityModBase
{
    public static class DelegateHelper
    {
        public static IEnumerable<T> GetInvocationListOrEmpty<T>(this T del) where T : Delegate
        {
            if (del == null)
                yield break;

            foreach (var d in del.GetInvocationList())
                yield return d as T ?? throw new InvalidCastException($"Cannot cast delegate of type {d.GetType().FullName} to {typeof(T).FullName}.");
        }
    }
}
