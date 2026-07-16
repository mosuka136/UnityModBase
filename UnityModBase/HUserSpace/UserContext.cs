using System;
using System.Collections.Concurrent;

namespace UnityModBase.HUserSpace
{
    /// <summary>
    /// 表示一个插件或框架用户的资源作用域，拥有对应的 <see cref="UserService"/> 及按键索引的子上下文。
    /// 释放时会尽力释放全部子上下文和服务，但不会阻止对象随后再次被访问或添加子上下文。
    /// </summary>
    /// <remarks>
    /// 子上下文容器支持并发的单次增删查；创建、替换服务与释放组成的完整生命周期不具备原子性，调用方应自行串行化。
    /// </remarks>
    public class UserContext : IUserContext
    {
        // 只保护子上下文映射；子对象自身状态及与 Dispose 的组合操作不在保护范围内。
        private readonly ConcurrentDictionary<string, IUserContext> _contexts;

        /// <summary>
        /// 用户注册表中的唯一标识；仅 <see cref="InvalidUserContext"/> 使用空字符串。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 面向界面显示的名称；构造参数为 <c>null</c> 时规范化为空字符串。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 此上下文拥有的配置与日志服务。替换该引用不会自动释放原服务；释放上下文时只释放当前引用。
        /// </summary>
        public UserService Service { get; set; }

        /// <summary>
        /// 查找失败时返回的共享空对象；以引用相等判断有效性，不表示一个已注册用户。
        /// </summary>
        public readonly static UserContext InvalidUserContext = new UserContext("Invalid User");

        /// <summary>
        /// 当前实例是否不是共享的 <see cref="InvalidUserContext"/>。
        /// </summary>
        public bool IsValid => !ReferenceEquals(this, InvalidUserContext);

        private UserContext(string name)
        {
            UserId = string.Empty;
            Name = name ?? string.Empty;
            _contexts = new ConcurrentDictionary<string, IUserContext>();
        }

        /// <summary>
        /// 创建用户资源作用域并立即为其建立日志数据库。
        /// </summary>
        /// <param name="userId">非空白的用户唯一标识。</param>
        /// <param name="name">显示名称；<c>null</c> 会转换为空字符串。</param>
        /// <exception cref="ArgumentException"><paramref name="userId"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        public UserContext(string userId, string name)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or whitespace.", nameof(userId));

            UserId = userId;
            Name = name ?? string.Empty;
            Service = new UserService(userId);
            _contexts = new ConcurrentDictionary<string, IUserContext>();
        }

        /// <summary>
        /// 按键登记一个由当前上下文负责释放的子上下文。
        /// 已有同名项时返回 <c>false</c> 并保留原对象，不会释放传入对象。
        /// </summary>
        /// <param name="key">非空白的子上下文键。</param>
        /// <param name="context">要转移生命周期所有权的上下文，不得为当前实例。</param>
        /// <returns>成功添加时为 <c>true</c>；键已存在时为 <c>false</c>。</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为空白，或 <paramref name="context"/> 是当前实例时抛出。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <c>null</c> 时抛出。</exception>
        public bool AddChildContext(string key, IUserContext context)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (ReferenceEquals(context, this))
                throw new ArgumentException("Cannot add the context to itself.", nameof(context));

            return _contexts.TryAdd(key, context);
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
            _contexts.TryRemove(key, out _);
        }

        /// <summary>
        /// 获取指定子上下文；键不存在时返回共享的 <see cref="InvalidUserContext"/>，不返回 <c>null</c>。
        /// </summary>
        /// <param name="key">非空白的子上下文键。</param>
        /// <returns>已登记的上下文或无效上下文哨兵。</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 <c>null</c>、空字符串或仅空白时抛出。</exception>
        public IUserContext GetChildContext(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (_contexts.TryGetValue(key, out var context))
                return context;

            return InvalidUserContext;
        }

        /// <summary>
        /// 尽力释放当前登记的全部子上下文并清空映射，然后释放用户服务。
        /// 单个子上下文或服务抛出的异常会被忽略，尽可能继续清理其余资源。
        /// </summary>
        public void Dispose()
        {
            try
            {
                foreach (var context in _contexts.Values)
                {
                    try { context.Dispose(); }
                    catch { }
                }
                _contexts.Clear();
                Service?.Dispose();
            }
            catch { }
        }
    }
}
