using System.Collections.Generic;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 提供实时控制缓存使用的强类型等值比较，不借用配置模型的比较规则。
    /// </summary>
    internal static class ControlValueComparer
    {
        internal static bool Equal<T>(T left, T right)
        {
            return EqualityComparer<T>.Default.Equals(left, right);
        }
    }
}
