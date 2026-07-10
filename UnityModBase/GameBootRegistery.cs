using System;
using System.Collections.Generic;
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
        private static readonly List<GameObject> _createdGameBootObjects = new List<GameObject>();

        /// <summary>
        /// 游戏启动阶段的一次性回调集合。
        /// </summary>
        public static event Action OnGameBoot;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                RegisterAssemblies(assemblies);

                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

                _initialized = true;
            }
        }

        public static void Boot()
        {
            lock (_lock)
            {
                foreach (var handler in OnGameBoot.GetInvocationListOrEmpty())
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

            return assemblyName.StartsWith("Unity.") ||
                   assemblyName.StartsWith("UnityEngine") ||
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

                OnGameBoot += new GameBootComponentRegistration(type).Invoke;
                BLog.Debug($"Register game boot component: {type.FullName}");
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
                OnGameBoot += new GameBootMethodRegistration(method, methodName).Invoke;
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            var assembly = args.LoadedAssembly;
            if (IsShouldSkipAssembly(assembly))
                return;
            RegisterAssembly(assembly);
        }

        private static void RegisterCreatedGameBootObject(GameObject gameObject)
        {
            lock (_lock)
            {
                _createdGameBootObjects.Add(gameObject);
            }
        }

        private static void DestroyGameBootObjects(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject is null)
                    continue;

                try
                {
                    DestroyGameBootObject(gameObject);
                }
                catch (Exception ex)
                {
                    BLog.Error("Failed to destroy game boot object.", ex);
                }
            }
        }

        private static void DestroyGameBootObject(GameObject gameObject)
        {
            UnityEngine.Object.Destroy(gameObject);
        }

        private sealed class GameBootComponentRegistration
        {
            private readonly Type _type;

            public GameBootComponentRegistration(Type type)
            {
                _type = type;
            }

            public void Invoke()
            {
                GameObject go = null;

                try
                {
                    go = new GameObject($"{nameof(UnityModBase)}_{_type.FullName}")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.AddComponent(_type);
                    RegisterCreatedGameBootObject(go);
                    BLog.Debug($"Created game boot component: {_type.FullName}");
                }
                catch (Exception ex)
                {
                    DestroyGameBootObjects(new[] { go });
                    BLog.Error($"Failed to create game boot component: {_type.FullName}", ex);
                }
            }
        }

        private sealed class GameBootMethodRegistration
        {
            private readonly MethodInfo _method;
            private readonly string _methodName;

            public GameBootMethodRegistration(MethodInfo method, string methodName)
            {
                _method = method;
                _methodName = methodName;
            }

            public void Invoke()
            {
                try
                {
                    BLog.Debug($"Invoke game boot method: {_methodName}");
                    _method.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to invoke game boot method: {_methodName}", ex);
                }
            }
        }

        public static void Dispose()
        {
            GameObject[] gameObjects;

            lock (_lock)
            {
                if (_initialized)
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

                OnGameBoot = null;
                gameObjects = _createdGameBootObjects.ToArray();
                _createdGameBootObjects.Clear();

                _initialized = false;
            }

            DestroyGameBootObjects(gameObjects);
        }
    }
}
