using System;
using System.Reflection;

namespace UnityModBase.HClassAttribute
{
    public static partial class ClassHelper
    {
        /// <summary>
        /// 获取程序集内带 <see cref="RegisterOnGameBootAttribute"/> 的组件候选类型。
        /// 此处只执行特性筛选，组件继承关系由启动注册器校验。
        /// </summary>
        /// <param name="assembly">要扫描的程序集。</param>
        /// <returns>带游戏启动注册特性的类型数组。</returns>
        public static Type[] GetRegisterOnGameBootClasses(Assembly assembly)
        {
            return GetClasses<RegisterOnGameBootAttribute>(assembly);
        }

        /// <summary>
        /// 获取程序集内带 <see cref="InitializeOnGameBootAttribute"/> 的方法候选项。
        /// 此处不校验静态、无参及返回类型约束，签名由启动注册器在执行阶段校验。
        /// </summary>
        /// <param name="assembly">要扫描的程序集。</param>
        /// <returns>带游戏启动初始化特性的方法数组。</returns>
        public static MethodInfo[] GetInitializeOnGameBootMethods(Assembly assembly)
        {
            return GetMethods<InitializeOnGameBootAttribute>(assembly);
        }
    }
}
