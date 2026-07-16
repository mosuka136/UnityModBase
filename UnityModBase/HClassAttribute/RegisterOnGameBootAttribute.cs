using System;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 标记一个应在宿主首次派发游戏启动回调时创建的常驻 Unity 组件类型。
    /// 被标记类型必须继承 <see cref="UnityEngine.Component"/>；类型筛选本身不验证该约束，启动注册阶段才会拒绝无效类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterOnGameBootAttribute : Attribute
    {
    }
}
