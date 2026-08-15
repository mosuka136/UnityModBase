using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase
{
    /// <summary>
    /// UnityModBase 的进程级生命周期入口，负责按顺序建立和释放配置、日志、启动注册等基础服务。
    /// 该类型不决定游戏何时完成启动；宿主需要在合适的时机另行调用 <see cref="GameBootRegistry.Boot"/>。
    /// </summary>
    /// <remarks>
    /// 初始化与释放通过同一把锁串行化，但这不表示各子服务的任意操作都可跨线程调用。
    /// 涉及 Unity 对象的启动和释放仍应在 Unity 主线程执行。
    /// </remarks>
    public static class UnityModBase
    {
        // 只保护顶层生命周期切换，避免并发初始化或初始化与释放交错。
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化基础服务并开始监听带游戏启动特性的程序集。
        /// 重复调用不会重新创建服务；初始化失败时会回滚已建立的全局状态，并保留原始异常。
        /// </summary>
        /// <param name="baseDirectory">
        /// 框架数据目录或其父目录。目录末级名称不是 UnityModBase 时，基础服务会在其下创建同名子目录。
        /// </param>
        public static void Initialize(string baseDirectory)
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                try
                {
                    BService.Initialize(baseDirectory);
                    GameBootRegistry.Initialize();

                    BLog.Info($"UnityModBase initialized. BaseDirectory='{baseDirectory}'.");
                    _initialized = true;
                }
                catch
                {
                    // 统一走释放路径，避免部分初始化留下事件订阅、用户上下文或文件资源。
                    Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// 释放所有进程级服务和已注册用户上下文，并清空启动、逐帧、退出和语言切换事件的订阅者。
        /// 该方法也用于初始化失败后的回滚；各子模块负责容忍尚未初始化或重复释放的情况。
        /// </summary>
        public static void Dispose()
        {
            lock (_lock)
            {
                // 先派发退出回调，使订阅者执行时其余基础服务仍然可用。
                GameQuitManager.Dispose();
                GameBootRegistry.Dispose();
                // 先解除框架自身的配置联动并丢弃静态引用，再统一释放注册表中所有用户的配置和日志资源。
                BService.Dispose();
                UserManager.Dispose();
                FrameUpdateManager.Dispose();
                Translator.Dispose();

                _initialized = false;
            }
        }
    }
}
