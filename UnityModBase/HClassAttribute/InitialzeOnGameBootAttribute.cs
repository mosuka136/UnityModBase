using System;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 标记一个应在宿主首次派发游戏启动回调时执行的一次性初始化方法。
    /// 有效签名必须为非泛型、无参、返回 <see cref="void"/> 的静态方法；签名在执行阶段校验。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class InitializeOnGameBootAttribute : Attribute
    {
    }
}
