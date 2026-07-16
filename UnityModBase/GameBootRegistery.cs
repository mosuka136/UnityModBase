using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;

namespace UnityModBase
{
    /// <summary>
    /// 发现并登记游戏启动扩展点，在宿主确认首次场景可用后一次性创建常驻组件并调用初始化方法。
    /// </summary>
    /// <remarks>
    /// 该注册器只管理带 <see cref="RegisterOnGameBootAttribute"/> 或
    /// <see cref="InitializeOnGameBootAttribute"/> 的扩展点，不负责判断具体游戏是否已进入可操作状态。
    /// 启动回调可能使用 Unity API，因此 <see cref="Boot"/> 和 <see cref="Dispose"/> 应在 Unity 主线程调用。
    /// </remarks>
    public static class GameBootRegistery
    {
        private static bool _initialized = false;

        // 进程级一次性哨兵。Dispose 不会复位它，释放后重新初始化也不会再次派发游戏启动回调。
        private static bool _gameBootInvoked = false;

        // 串行化初始化、启动派发、回调登记以及已创建 Unity 对象列表的变更。
        private static readonly object _lock = new object();

        // 仅记录由本注册器成功创建的常驻对象，用于插件卸载时集中销毁。
        private static readonly List<GameObject> _createdGameBootObjects = new List<GameObject>();

        /// <summary>
        /// 游戏启动阶段的一次性回调。首次 <see cref="Boot"/> 后会清空；此后新增订阅也不会再执行。
        /// </summary>
        public static event Action OnGameBoot;

        /// <summary>
        /// 扫描当前已加载程序集中的启动特性，并监听后续程序集加载事件。
        /// 该方法只完成登记，不会执行启动回调；重复调用不会重复扫描或订阅。
        /// </summary>
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

        /// <summary>
        /// 执行登记的游戏启动回调，并保证同一进程内最多派发一次。
        /// 单个回调失败只会记录日志，不会阻止其余回调；重入调用会立即返回。
        /// </summary>
        /// <remarks>
        /// 执行前即设置一次性哨兵，以避免回调重入造成重复初始化。回调按订阅顺序同步执行，
        /// 调用期间其他线程上的登记或启动请求会等待当前派发结束。
        /// </remarks>
        public static void Boot()
        {
            lock (_lock)
            {
                if (_gameBootInvoked)
                    return;

                _gameBootInvoked = true;

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

        /// <summary>
        /// 扫描一组程序集并登记其中的游戏启动扩展点。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集集合；数组及其中的元素不可为 <c>null</c>。框架程序集会按名称前缀跳过。</param>
        public static void RegisterAssemblies(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                if (IsShouldSkipAssembly(assembly))
                    continue;
                RegisterAssembly(assembly);
            }
        }

        /// <summary>
        /// 判断程序集是否属于无需扫描的 Unity、运行库或常见基础设施程序集。
        /// </summary>
        /// <param name="assembly">要检查的程序集，不可为 <c>null</c>。</param>
        /// <returns>程序集简单名称为空或命中排除前缀时为 <c>true</c>。</returns>
        /// <remarks>筛选仅依据程序集简单名称；自定义程序集若使用相同前缀，也会被排除。</remarks>
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

        /// <summary>
        /// 反射指定程序集，登记带启动特性的组件类型和方法。
        /// 游戏启动完成后才加载的程序集不会补执行其中的扩展点。
        /// </summary>
        /// <param name="assembly">要扫描的程序集，不可为 <c>null</c>。</param>
        public static void RegisterAssembly(Assembly assembly)
        {
            var types = ClassHelper.GetRegisterOnGameBootClasses(assembly);
            var methods = ClassHelper.GetInitializeOnGameBootMethods(assembly);

            if (_gameBootInvoked && (types.Length > 0 || methods.Length > 0))
            {
                BLog.Warn($"Assembly {assembly.FullName} is loaded after game boot, any registered components or methods will not be invoked.");
                return;
            }

            int registeredComponentCount = 0;
            foreach (var type in types)
            {
                if (RegisterComponentOnGameBoot(type))
                    registeredComponentCount++;
            }

            int registeredMethodCount = 0;
            foreach (var method in methods)
            {
                if (RegisterMethodOnGameBoot(method))
                    registeredMethodCount++;
            }

            BLog.Debug($"Registered {registeredComponentCount} game boot components and {registeredMethodCount} game boot methods from assembly: {assembly.FullName}.");
        }

        /// <summary>
        /// 登记一个在游戏启动时创建的常驻 Unity 组件。
        /// </summary>
        /// <param name="type">必须派生自 <see cref="Component"/>；<c>null</c> 或非组件类型不会被登记。</param>
        /// <returns>通过组件类型检查并加入启动回调时为 <c>true</c>，否则为 <c>false</c>。</returns>
        /// <remarks>
        /// 抽象类型等无法由 Unity 实例化的情况会延迟到启动阶段处理，失败时记录日志且不影响其他回调。
        /// 必须在首次 <see cref="Boot"/> 前调用；启动后的直接登记不会被补执行。
        /// </remarks>
        public static bool RegisterComponentOnGameBoot(Type type)
        {
            lock (_lock)
            {
                if (type == null)
                {
                    BLog.Warn("Cannot register a null component type for game boot.");
                    return false;
                }

                if (!typeof(Component).IsAssignableFrom(type))
                {
                    BLog.Warn($"Type {type.FullName} is not a Component, cannot register for game boot.");
                    return false;
                }

                OnGameBoot += new GameBootComponentRegistration(type).Invoke;
                BLog.Debug($"Register game boot component: {type.FullName}");
                return true;
            }
        }

        /// <summary>
        /// 登记一个在游戏启动时执行的初始化方法。
        /// </summary>
        /// <param name="method">
        /// 候选方法；<c>null</c> 不会被登记。有效签名必须为不含未绑定泛型参数、无参数、返回 <see cref="void"/> 的静态方法。
        /// </param>
        /// <returns><paramref name="method"/> 非空并加入启动回调时为 <c>true</c>，否则为 <c>false</c>。</returns>
        /// <remarks>
        /// 签名在启动回调执行时校验，因此签名无效的方法也可能登记成功，但不会被调用。
        /// 必须在首次 <see cref="Boot"/> 前调用；启动后的直接登记不会被补执行。
        /// </remarks>
        public static bool RegisterMethodOnGameBoot(MethodInfo method)
        {
            lock (_lock)
            {
                if (method == null)
                {
                    BLog.Notice("Cannot register a null method for game boot.");
                    return false;
                }

                var methodName = $"{method.DeclaringType.FullName}.{method.Name}";
                BLog.Debug($"Register game boot method: {methodName}");
                OnGameBoot += new GameBootMethodRegistration(method, methodName).Invoke;
                return true;
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

            internal GameBootComponentRegistration(Type type)
            {
                _type = type;
            }

            internal void Invoke()
            {
                GameObject go = null;

                try
                {
                    // 启动组件不应出现在层级或被场景保存，并需跨场景保留到框架统一释放。
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

            internal GameBootMethodRegistration(MethodInfo method, string methodName)
            {
                _method = method;
                _methodName = methodName;
            }

            internal void Invoke()
            {
                try
                {
                    // 校验放在执行端，确保反射发现的非法扩展点不会中断整批启动回调。
                    if (!IsValidGameBootMethod(_method))
                    {
                        BLog.Error($"Invalid game boot method: {_methodName}. It must be a static method with no parameters and return void.");
                        return;
                    }
                    _method.Invoke(null, null);
                    BLog.Debug($"Invoke game boot method: {_methodName}");
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to invoke game boot method: {_methodName}", ex);
                }
            }

            private static bool IsValidGameBootMethod(MethodInfo method)
            {
                if (method == null)
                    return false;
                if (!method.IsStatic)
                    return false;
                if (method.GetParameters().Length != 0)
                    return false;
                if (method.ReturnType != typeof(void))
                    return false;
                if (method.ContainsGenericParameters)
                    return false;
                return true;
            }
        }

        /// <summary>
        /// 停止监听程序集加载、丢弃尚未执行的启动回调，并销毁本注册器创建的常驻对象。
        /// </summary>
        /// <remarks>
        /// Unity 对象在锁外销毁，避免销毁过程中的 Unity 回调进入注册器时形成锁内副作用。
        /// 进程级启动哨兵不会复位，因此该方法不是开始第二轮游戏启动周期的重置操作。
        /// </remarks>
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
