using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModBase.BSpace;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 基于特性的反射辅助工具。
    /// 该类型集中处理配置 UI 元数据、游戏启动注册等反射查询，并缓存属性特性以减少每帧 GUI 查询成本。
    /// </summary>
    public class ClassHelper
    {
        // 属性特性按“类型 + 属性名 + 特性类型”缓存，ConcurrentDictionary 只保证缓存访问本身可并发。
        private static readonly ConcurrentDictionary<(Type classType, string propertyName, Type attributeType), Attribute> _attributeCache = new ConcurrentDictionary<(Type classType, string propertyName, Type attributeType), Attribute>();

        /// <summary>
        /// 获取指定属性上的特性。
        /// </summary>
        /// <typeparam name="TAttribute">要读取的特性类型。</typeparam>
        /// <param name="classType">声明属性的类型。</param>
        /// <param name="propertyName">拥有指定特性的属性名。</param>
        /// <returns>找到的特性；属性存在但未标记时返回 <c>null</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="ArgumentException">属性名为空或属性不存在时抛出。</exception>
        public static TAttribute GetAttribute<TAttribute>(Type classType, string propertyName) where TAttribute : Attribute
        {
            var key = (classType, propertyName, typeof(TAttribute));
            if (_attributeCache.TryGetValue(key, out var cachedAttribute))
            {
                return (TAttribute)cachedAttribute;
            }
            var propertyInfo = classType.GetProperty(propertyName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found in class '{classType.FullName}'.");
            }
            var attribute = propertyInfo.GetCustomAttributes(typeof(TAttribute), false).FirstOrDefault() as TAttribute;
            _attributeCache[key] = attribute;
            return attribute;
        }

        /// <summary>
        /// 获取程序集内直接标记了指定特性的类型；单个类型的特性读取失败只记录日志并继续扫描。
        /// </summary>
        /// <typeparam name="TAttribute">用于筛选类型的特性。</typeparam>
        /// <param name="assembly">要扫描的程序集。</param>
        /// <returns>扫描成功且带有指定特性的类型数组。</returns>
        public static Type[] GetClasses<TAttribute>(Assembly assembly) where TAttribute : Attribute
        {
            var result = new List<Type>();

            foreach (var type in GetTypeSafe(assembly))
            {
                try
                {
                    if (type.GetCustomAttributes(typeof(TAttribute), false).Length > 0)
                        result.Add(type);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to get attributes from type '{type.FullName}'.", ex);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 获取程序集各类型中直接标记了指定特性的方法，包括公开/非公开及静态/实例方法。
        /// 单个类型或方法反射失败只记录日志，不会终止其余类型的扫描。
        /// </summary>
        /// <typeparam name="TAttribute">用于筛选方法的特性。</typeparam>
        /// <param name="assembly">要扫描的程序集。</param>
        /// <returns>扫描成功且带有指定特性的方法数组。</returns>
        public static MethodInfo[] GetMethods<TAttribute>(Assembly assembly) where TAttribute : Attribute
        {
            var result = new List<MethodInfo>();

            foreach (var type in GetTypeSafe(assembly))
            {
                MethodInfo[] methods;

                try
                {
                    methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to get methods from type '{type.FullName}'.", ex);
                    continue;
                }

                foreach (var method in methods)
                {
                    try
                    {
                        if (method.GetCustomAttributes(typeof(TAttribute), false).Length > 0)
                            result.Add(method);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Failed to get attributes from method '{method.Name}' in type '{type.FullName}'.", ex);
                    }
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 获取程序集中的可加载类型；部分类型加载失败时保留其余非空类型，整体失败时返回空数组。
        /// 所有失败都会写入框架日志而不会向调用方传播反射异常。
        /// </summary>
        /// <param name="assembly">要读取的程序集。</param>
        /// <returns>成功加载的类型数组。</returns>
        public static Type[] GetTypeSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                BLog.Error($"Failed to load some types from assembly '{assembly.FullName}'. Loaded {ex.Types.Length} types, {ex.LoaderExceptions.Length} loader exceptions.", ex);
                return ex.Types.Where(t => t != null).ToArray();
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to load types from assembly '{assembly.FullName}'.", ex);
                return Array.Empty<Type>();
            }
        }

        /// <summary>
        /// 读取配置属性上的滑条范围和步进元数据。
        /// </summary>
        /// <param name="classType">声明配置属性的类型。</param>
        /// <param name="propertyName">配置属性名。</param>
        /// <returns>存在 <see cref="ConfigSliderAttribute"/> 时返回其范围和步进，否则返回 <c>null</c>。</returns>
        public static (float Min, float Max, float Step)? GetSliderInfo(Type classType, string propertyName)
        {
            var sliderAttribute = GetAttribute<ConfigSliderAttribute>(classType, propertyName);
            if (sliderAttribute != null)
            {
                return (sliderAttribute.Min, sliderAttribute.Max, sliderAttribute.Step);
            }
            return null;
        }

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
