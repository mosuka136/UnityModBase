using System;
using System.Collections.Concurrent;
using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HUserSpace
{
    /// <summary>
    /// 表示单个插件或框架用户的资源作用域，持有该用户的服务以及按键索引的直接子上下文。
    /// 本类型负责单个作用域内的资源归属，不负责将用户加入或移出 <see cref="UserManager"/> 注册表。
    /// </summary>
    /// <remarks>
    /// 子上下文映射支持并发的单次增删查，但增删、服务替换和释放组成的完整生命周期不具备原子性。
    /// 本类型不记录已释放状态；调用方应自行串行化生命周期操作，并避免在释放期间或释放后继续增删子上下文，
    /// 否则并发加入的对象可能未经释放便被最终清理映射。
    /// </remarks>
    public partial class UserContext : IDisposable
    {
        // 仅保护键到直接子上下文的映射；子对象状态以及与 Dispose 组合的操作不受该并发容器保护。
        private readonly ConcurrentDictionary<string, IUserContext> _childrenContext = new ConcurrentDictionary<string, IUserContext>();

        /// <summary>
        /// 当前作用域的非空白用户标识；直接构造时不保证全局唯一，由 <see cref="UserManager"/> 注册时约束唯一性。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 面向界面的非空可翻译名称。属性保留构造时传入的 <see cref="Translator"/> 实例，
        /// 文本在使用时按实例语言或 <see cref="Translator.DefaultLanguage"/> 解析。
        /// </summary>
        public Translator Name { get; }

        /// <summary>
        /// 此上下文拥有的配置与日志服务。替换该引用不会自动释放原服务；释放上下文时只释放当前引用。
        /// </summary>
        public UserService Service { get; set; }

        /// <summary>
        /// 创建用户资源作用域并初始化对应的用户服务；不会自动登记到 <see cref="UserManager"/>。
        /// </summary>
        /// <param name="userId">非空白的用户唯一标识。</param>
        /// <param name="name">非空的可翻译显示名称；实例按引用保存，不会复制其文本或语言状态。</param>
        /// <exception cref="ArgumentException"><paramref name="userId"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> 为 <c>null</c> 时抛出。</exception>
        public UserContext(string userId, Translator name)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or whitespace.", nameof(userId));

            UserId = userId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Service = new UserService(userId);
        }

        /// <summary>
        /// 按键登记一个直接子上下文；登记成功后，其生命周期所有权由当前作用域接管。
        /// 同键上下文已存在时保留原对象并抛出异常，不会释放传入对象。
        /// </summary>
        /// <param name="key">非空白的子上下文键。</param>
        /// <param name="context">要转移生命周期所有权的上下文。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="InvalidOperationException">同键子上下文已存在时抛出。</exception>
        /// <remarks>
        /// 本方法不检测引用环。不得传入当前实例，也不得构造会在释放时重新进入当前作用域的间接环，
        /// 否则父子级联释放会递归进入同一对象。
        /// </remarks>
        public void AddChildContext(string key, IUserContext context)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!_childrenContext.TryAdd(key, context))
                throw new InvalidOperationException($"A child context with the key '{key}' already exists.");
        }

        /// <summary>
        /// 移除指定子上下文登记，但不释放被移除的对象；后续生命周期由调用方负责。
        /// 不存在的键按空操作处理。
        /// </summary>
        /// <param name="key">非空白的子上下文键。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        public void RemoveChildContext(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            _childrenContext.TryRemove(key, out _);
        }

        /// <summary>
        /// 获取指定键对应的直接子上下文，不沿后代节点继续查找。
        /// </summary>
        /// <param name="key">非空白的子上下文键。</param>
        /// <returns>已登记的直接子上下文；键不存在时为 <c>null</c>。</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        public IUserContext GetChildContext(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            if (_childrenContext.TryGetValue(key, out var context))
                return context;
            else
                return null;
        }

        /// <summary>
        /// 依次释放当前枚举到的子上下文和用户服务，并在服务释放后清空子上下文映射。
        /// 单个子上下文的释放异常会按键记录并继续处理其余对象；其他释放异常也只记录日志，不向调用方传播。
        /// </summary>
        /// <remarks>
        /// 该方法不会把 <see cref="Service"/> 引用置空，因此重复调用仍会再次释放同一服务。
        /// 本方法不与子上下文增删操作组成原子事务；调用方不得让释放与增删并发执行。
        /// </remarks>
        public void Dispose()
        {
            try
            {
                foreach (var childContext in _childrenContext)
                {
                    try
                    {
                        childContext.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        BLog.Error($"Failed to dispose child context with key '{childContext.Key}'.", ex);
                    }
                }
                Service?.Dispose();
                _childrenContext.Clear();
            }
            catch (Exception ex)
            {
                BLog.Error("Failed to dispose user context.", ex);
            }
        }
    }
}
