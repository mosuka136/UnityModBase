using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModBase.BSpace;

namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 基于特性的反射辅助工具。
    /// 该类型集中处理配置 UI 元数据、游戏启动注册等反射查询；各查询方法均不缓存结果，高频调用方需自行缓存。
    /// 定义按主题拆分为多个 partial 文件：本文件提供通用的特性、类型、方法与属性扫描；配置 UI 元数据查询位于 ClassHelperConfigMetadata.cs；游戏启动注册扫描位于 ClassHelperOnGameBoot.cs。
    /// </summary>
    public static partial class ClassHelper
    {
        /// <summary>
        /// 获取程序集内直接标记了指定特性的类型；单个类型的特性读取失败只记录日志并继续扫描。
        /// </summary>
        /// <typeparam name="TAttribute">用于筛选类型的特性。</typeparam>
        /// <param name="assembly">要扫描的程序集。</param>
        /// <returns>扫描成功且带有指定特性的类型数组。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <c>null</c>。</exception>
        public static Type[] GetClasses<TAttribute>(Assembly assembly) where TAttribute : Attribute
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

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
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> 为 <c>null</c>。</exception>
        public static MethodInfo[] GetMethods<TAttribute>(Assembly assembly) where TAttribute : Attribute
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

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
        /// 获取类型中直接标记了指定特性的属性（含公开与非公开）。
        /// 只扫描静态属性是约定而非遗漏：配置项以配置管理器的静态属性暴露，调用方需要在没有实例的情况下读取属性当前值。
        /// 属性表读取或单个属性的特性读取失败只记录日志并跳过，返回结果可能是不完整的子集。
        /// 本方法不做缓存，高频调用方应自行缓存结果。
        /// </summary>
        /// <typeparam name="TAttribute">用于筛选属性的特性。</typeparam>
        /// <param name="classType">要扫描的类型。</param>
        /// <returns>带有指定特性的静态属性数组。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="classType"/> 为 <c>null</c>。</exception>
        public static PropertyInfo[] GetProperties<TAttribute>(Type classType) where TAttribute : Attribute
        {
            if (classType == null)
                throw new ArgumentNullException(nameof(classType));

            var result = new List<PropertyInfo>();
            PropertyInfo[] properties;

            try
            {
                properties = classType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to get properties from type '{classType.FullName}'.", ex);
                return result.ToArray();
            }

            foreach (var property in properties)
            {
                try
                {
                    if (property.GetCustomAttributes(typeof(TAttribute), false).Length > 0)
                        result.Add(property);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to get attributes from property '{property.Name}' in type '{classType.FullName}'.", ex);
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
    }
}
