using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;

namespace UnityModBase
{
    public static class GameBootRegistery
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 游戏启动阶段的一次性回调集合。
        /// </summary>
        public static event Action OnGameBoot;

        public static void Initialize()
        {
            if (_initialized)
                return;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            RegisterAssemblies(assemblies);

            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                var assembly = args.LoadedAssembly;
                if (IsShouldSkipAssembly(assembly))
                    return;
                RegisterAssembly(assembly);
            };

            _initialized = true;
        }

        public static void Boot()
        {
            lock (_lock)
            {
                foreach (var handler in (OnGameBoot?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action>())
                {
                    try
                    {
                        handler?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        BLog.Error("An error occurred while invoking OnGameBoot event.", ex);
                    }
                }

                OnGameBoot = null;
                BLog.Debug("Game boot initialization completed.");
            }
        }

        public static void RegisterAssemblies(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                if (IsShouldSkipAssembly(assembly))
                    continue;
                RegisterAssembly(assembly);
            }
        }

        public static bool IsShouldSkipAssembly(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null)
                return true;

            return assemblyName.StartsWith("UnityEngine") ||
                   assemblyName.StartsWith("UnityEditor") ||
                   assemblyName.StartsWith("System") ||
                   assemblyName.StartsWith("mscorlib") ||
                   assemblyName.StartsWith("netstandard") ||
                   assemblyName.StartsWith("Mono.") ||
                   assemblyName.StartsWith("Microsoft.") ||
                   assemblyName.StartsWith("Newtonsoft.") ||
                   assemblyName.StartsWith("BepInEx.") ||
                   assemblyName.StartsWith("HarmonyLib");
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            var types = ClassHelper.GetRegisterOnGameBootClasses(assembly);
            int registeredComponentCount = 0;
            foreach (var type in types)
            {
                registeredComponentCount++;
                RegisterComponentOnGameBoot(type);
            }

            var methods = ClassHelper.GetInitializeOnGameBootMethods(assembly);
            int registeredMethodCount = 0;
            foreach (var method in methods)
            {
                registeredMethodCount++;
                RegisterMethodOnGameBoot(method);
            }

            BLog.Debug($"Registered {registeredComponentCount} game boot components and {registeredMethodCount} game boot methods from assembly: {assembly.FullName}.");
        }

        /// <summary>
        /// 将一个 Unity 组件类型注册为游戏启动后创建的常驻对象。
        /// </summary>
        /// <param name="type">必须派生自 <see cref="Component"/>；非法类型只记录警告，不抛出异常。</param>
        public static void RegisterComponentOnGameBoot(Type type)
        {
            lock (_lock)
            {
                if (!typeof(Component).IsAssignableFrom(type))
                {
                    BLog.Warn($"Type {type.FullName} is not a Component, cannot register for game boot.");
                    return;
                }

                BLog.Debug($"Register game boot component: {type.FullName}");
                OnGameBoot += () =>
                {
                    try
                    {
                        var go = new GameObject($"{nameof(UnityModBase)}_{type.FullName}")
                        {
                            hideFlags = HideFlags.HideAndDontSave
                        };
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        go.AddComponent(type);
                        BLog.Debug($"Created game boot component: {type.FullName}");
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Failed to create game boot component: {type.FullName}", ex);
                    }
                };
            }
        }

        /// <summary>
        /// 将一个静态方法注册为游戏启动后执行的初始化逻辑。非法方法只记录警告，不抛出异常。
        /// </summary>
        /// <param name="method">必须为无参数且返回 <see cref="void"/> 的静态方法。</param>
        public static void RegisterMethodOnGameBoot(MethodInfo method)
        {
            lock (_lock)
            {
                if (method == null)
                {
                    BLog.Notice("Cannot register a null method for game boot.");
                    return;
                }

                var methodName = $"{method.DeclaringType.FullName}.{method.Name}";
                BLog.Debug($"Register game boot method: {methodName}");
                OnGameBoot += () =>
                {
                    try
                    {
                        BLog.Debug($"Invoke game boot method: {methodName}");
                        method.Invoke(null, null);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Failed to invoke game boot method: {methodName}", ex);
                    }
                };
            }
        }
    }
}
