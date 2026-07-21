using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;

namespace UnityModBase
{
    /// <summary>
    /// 发现并登记游戏启动扩展点，在宿主确认当前游戏生命周期的首个可用场景就绪后，
    /// 一次性创建常驻组件并调用初始化方法。
    /// </summary>
    /// <remarks>
    /// 该注册器只管理带 <see cref="RegisterOnGameBootAttribute"/> 或
    /// <see cref="InitializeOnGameBootAttribute"/> 的扩展点，不负责判断具体游戏是否已进入可操作状态。
    /// 启动回调可能使用 Unity API，因此 <see cref="Boot"/> 和 <see cref="Dispose"/> 应在 Unity 主线程调用。
    /// <see cref="Dispose"/> 会结束当前登记周期；随后重新初始化会重新扫描程序集，并允许再次派发启动回调。
    /// 注册器会在修改共享回调、去重集合和对象列表时取得内部锁，但程序集反射发现并非始终处于一个完整临界区内；
    /// 生命周期控制方仍应串行安排初始化、显式登记、启动派发和释放。调用方直接增删 <see cref="OnGameBoot"/> 处理器不经过该锁，
    /// 必须在启动派发前完成，且不得与 <see cref="Boot"/> 或 <see cref="Dispose"/> 并发。
    /// </remarks>
    public static class GameBootRegistry
    {
        // 只表示程序集扫描及 AssemblyLoad 监听已启用，与当前周期是否已经执行 Boot 相互独立。
        private static bool _initialized = false;

        // 当前登记周期的一次性哨兵；Dispose 会复位它，使重新初始化后的下一周期可以再次派发。
        private static bool _gameBootInvoked = false;

        // 保护启动派发、回调登记、扫描去重和已创建 Unity 对象列表的共享状态修改。
        // 反射发现阶段及公开字段式事件的外部 add/remove 不自动取得本锁，因此不属于这里的原子边界。
        private static readonly object _lock = new object();

        // 仅记录当前待派发阶段由反射扫描发现的扩展点，避免显式扫描与 AssemblyLoad 回调重复登记。
        // 直接调用 RegisterComponentOnGameBoot 或 RegisterMethodOnGameBoot 不参与去重；集合会在 Boot 或 Dispose 时清空。
        private static readonly HashSet<Type> _scannedType = new HashSet<Type>();
        private static readonly HashSet<MethodInfo> _scannedMethod = new HashSet<MethodInfo>();

        // 仅记录由本注册器成功创建的常驻对象，用于插件卸载时集中销毁。
        private static readonly List<GameObject> _createdGameBootObjects = new List<GameObject>();

        /// <summary>
        /// 当前登记周期的游戏启动回调。首次 <see cref="Boot"/> 后会清空；
        /// 同一周期内此后新增的订阅不会执行，并会在 <see cref="Dispose"/> 时丢弃。
        /// </summary>
        /// <remarks>调用方应在启动派发前完成订阅；不要让直接订阅或退订与 <see cref="Boot"/>、<see cref="Dispose"/> 并发。</remarks>
        public static event Action OnGameBoot;

        /// <summary>
        /// 扫描当前已加载程序集中的启动特性，并监听后续程序集加载事件。
        /// 该方法只完成登记，不会执行启动回调；成功初始化后的重复调用不会重复扫描或订阅。
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                // 先建立监听再获取快照，避免程序集恰好在二者之间加载而永远漏扫；重叠发现由扫描集合去重。
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                RegisterAssemblies(assemblies);

                _initialized = true;
            }
        }

        /// <summary>
        /// 执行登记的游戏启动回调，并保证两次 <see cref="Dispose"/> 之间最多派发一次。
        /// 单个回调失败只会记录日志，不会阻止其余回调；重入调用会立即返回。
        /// </summary>
        /// <remarks>
        /// 执行前即设置一次性哨兵，以避免回调重入造成重复初始化。回调按订阅顺序同步执行，
        /// 调用期间其他线程通过注册器方法发起的登记或启动请求会等待当前派发结束；
        /// 直接对 <see cref="OnGameBoot"/> 增删处理器不受该锁保护。
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
                // 启动后不再接受扫描所得扩展点，及时释放反射对象引用；下一周期会在重新初始化时建立新记录。
                _scannedType.Clear();
                _scannedMethod.Clear();
                BLog.Debug("Game boot initialization completed.");
            }
        }

        /// <summary>
        /// 扫描一组程序集并登记其中的游戏启动扩展点。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集集合；数组及其中的元素不可为 <c>null</c>。框架程序集会按名称前缀跳过。</param>
        /// <exception cref="NullReferenceException"><paramref name="assemblies"/> 或其中任一元素为 <c>null</c>。</exception>
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
        /// <exception cref="NullReferenceException"><paramref name="assembly"/> 为 <c>null</c>。</exception>
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
        /// 当前周期的游戏启动完成后才加载的程序集不会补执行其中的扩展点；
        /// 结束当前周期并重新初始化后，程序集会在全量扫描中重新参与登记。
        /// </summary>
        /// <param name="assembly">要扫描的程序集，不可为 <c>null</c>。</param>
        /// <remarks>
        /// 同一扩展点在当前待派发阶段被重复发现时只登记一次。该去重仅适用于反射扫描路径，
        /// 不影响调用方通过 <see cref="RegisterComponentOnGameBoot(Type)"/> 或
        /// <see cref="RegisterMethodOnGameBoot(MethodInfo)"/> 显式登记多个回调。
        /// </remarks>
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
                if (ContainsAndAddScannedType(type))
                    continue;

                if (RegisterComponentOnGameBoot(type))
                    registeredComponentCount++;
            }

            int registeredMethodCount = 0;
            foreach (var method in methods)
            {
                if (ContainsAndAddScannedMethod(method))
                    continue;

                if (RegisterMethodOnGameBoot(method))
                    registeredMethodCount++;
            }

            if (registeredComponentCount > 0 || registeredMethodCount > 0)
                BLog.Debug($"Registered {registeredComponentCount} game boot components and {registeredMethodCount} game boot methods from assembly: {assembly.FullName}.");
        }

        /// <summary>
        /// 登记一个在游戏启动时创建的常驻 Unity 组件。
        /// </summary>
        /// <param name="type">必须派生自 <see cref="Component"/>；<c>null</c> 或非组件类型不会被登记。</param>
        /// <returns>通过组件类型检查并加入启动回调时为 <c>true</c>，否则为 <c>false</c>。</returns>
        /// <remarks>
        /// 抽象类型等无法由 Unity 实例化的情况会延迟到启动阶段处理，失败时记录日志且不影响其他回调。
        /// 必须在当前周期首次 <see cref="Boot"/> 前调用；启动后的直接登记不会被补执行，
        /// 且其回调会在 <see cref="Dispose"/> 时丢弃，不会带入下一周期。
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
        /// 必须在当前周期首次 <see cref="Boot"/> 前调用；启动后的直接登记不会被补执行，
        /// 且其回调会在 <see cref="Dispose"/> 时丢弃，不会带入下一周期。
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

        // 将重复检查与标记合并到同一临界区；返回 true 表示调用方应跳过该扩展点。
        private static bool ContainsAndAddScannedType(Type type)
        {
            lock (_lock)
            {
                if (_scannedType.Contains(type))
                    return true;
                _scannedType.Add(type);
                return false;
            }
        }

        private static bool ContainsAndAddScannedMethod(MethodInfo method)
        {
            lock (_lock)
            {
                if (_scannedMethod.Contains(method))
                    return true;
                _scannedMethod.Add(method);
                return false;
            }
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
        /// 结束当前登记周期：停止监听程序集加载、丢弃尚未执行的启动回调，
        /// 清除反射扫描记录，销毁本注册器创建的常驻对象，并允许重新初始化后的下一周期再次执行 <see cref="Boot"/>。
        /// </summary>
        /// <remarks>
        /// Unity 对象在锁外销毁，避免销毁过程中的 Unity 回调进入注册器时形成锁内副作用。
        /// 该方法只复位注册器状态，不会自动重新扫描程序集；下一周期仍需先调用 <see cref="Initialize"/>。
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
                _scannedType.Clear();
                _scannedMethod.Clear();
                _createdGameBootObjects.Clear();

                _gameBootInvoked = false;
                _initialized = false;
            }

            DestroyGameBootObjects(gameObjects);
        }
    }
}
