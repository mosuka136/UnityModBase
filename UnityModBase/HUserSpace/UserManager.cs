using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;

namespace UnityModBase.HUserSpace
{
    /// <summary>
    /// 维护进程级用户上下文注册表，并把用户注册及配置模型变化转换为带用户身份的全局事件。
    /// 它不负责持久化用户信息；<see cref="Dispose"/> 会释放并移除当前进程中的全部注册项。
    /// </summary>
    /// <remarks>
    /// 内部注册表不是并发集合。注册、查询、移除和释放应由调用方在同一受控线程或外部锁下串行执行。
    /// </remarks>
    public static class UserManager
    {
        private static readonly Dictionary<string, UserContext> _userContexts = new Dictionary<string, UserContext>();

        /// <summary>
        /// <see cref="Register"/> 完成上下文及服务事件接线后触发；单个订阅者失败只记录日志。
        /// </summary>
        public static event Action<UserContext> OnUserRegistered;

        /// <summary>
        /// 已接线用户的当前配置服务触发 <see cref="ConfigService.OnConfigChanged"/> 时同步转发，
        /// 参数为拥有该服务的用户上下文；单个订阅者异常只记录日志。
        /// </summary>
        /// <remarks>
        /// 转发接线仅由 <see cref="Register"/> 建立；<see cref="CreateUser"/> 创建的上下文不参与转发。
        /// <see cref="Register"/> 会在配置服务创建前登记转发处理器，因此后续首次注册及替换配置服务时均会挂接；
        /// 但配置服务构造期间的首次文件读取早于实际挂接，不会产生全局通知。
        /// <see cref="Dispose"/> 会清空本事件的全局订阅者。
        /// </remarks>
        public static event Action<UserContext> OnConfigChanged;

        /// <summary>
        /// 当前注册表键的实时视图；注册表变化期间枚举可能失效，不是快照。
        /// </summary>
        public static IEnumerable<string> UserIds => _userContexts.Keys;

        /// <summary>
        /// 当前注册表值的实时视图；注册表变化期间枚举可能失效，不是快照。
        /// </summary>
        public static IEnumerable<UserContext> UserContexts => _userContexts.Values;

        /// <summary>
        /// 返回当前枚举顺序中的首个用户标识；注册表为空时返回空字符串。
        /// </summary>
        /// <returns>首个用户标识或空字符串。</returns>
        public static string GetDefaultUserId()
        {
            return UserIds.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        /// 判断注册表是否包含指定标识；<c>null</c> 或空字符串直接返回 <c>false</c>。
        /// </summary>
        /// <param name="userId">要查询的用户标识。</param>
        /// <returns>存在完全匹配的键时为 <c>true</c>。</returns>
        public static bool ContainsUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;
            return _userContexts.ContainsKey(userId);
        }

        /// <summary>
        /// 创建用户并预先接入配置模型变化转发。标识为空时自动生成，冲突时追加随机后缀而不复用已有上下文。
        /// </summary>
        /// <param name="userId">期望的用户标识；<c>null</c> 或空字符串表示自动生成。仅空白字符串不会被视为空。</param>
        /// <param name="name">显示名称；<c>null</c> 最终规范化为空字符串。</param>
        /// <returns>新建并登记到进程级注册表的用户上下文。</returns>
        /// <remarks>
        /// 新建上下文最初不包含配置服务；转发处理器由 <see cref="UserService.OnConfigChanged"/> 暂存，
        /// 并在随后调用 <see cref="UserService.RegisterConfig(Type, string)"/> 时挂到新服务。
        /// </remarks>
        public static UserContext Register(string userId, string name)
        {
            if (string.IsNullOrEmpty(userId))
                userId = $"User_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            while (ContainsUser(userId))
                userId += $"_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            var context = CreateUser(userId, name);

            var h = new ServiceEventHandler(context);
            context.Service.OnConfigChanged += h.OnConfigChangedHandler;

            foreach (var handler in OnUserRegistered.GetInvocationListOrEmpty())
            {
                try
                {
                    handler.Invoke(context);
                }
                catch (Exception ex)
                {
                    BLog.Error("Error invoking OnUserRegistered handler!", ex);
                }
            }

            return context;
        }

        /// <summary>
        /// 将配置服务不携带来源的模型变化事件转换为包含所属用户上下文的全局事件。
        /// 实例由用户服务的事件订阅持有，生命周期与该订阅一致。
        /// </summary>
        private sealed class ServiceEventHandler
        {
            private readonly UserContext _context;

            internal ServiceEventHandler(UserContext context)
            {
                _context = context;
            }

            internal void OnConfigChangedHandler()
            {
                foreach (var handler in OnConfigChanged.GetInvocationListOrEmpty())
                {
                    try
                    {
                        handler.Invoke(_context);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error("Error invoking OnConfigChanged handler!", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 创建并登记用户上下文，但不接入 <see cref="OnUserRegistered"/> 通知或 <see cref="OnConfigChanged"/> 转发。
        /// 注册表内标识已存在时直接返回原上下文并忽略新的名称。
        /// </summary>
        /// <param name="userId">非 <c>null</c> 且非空字符串的用户标识；仅空白值会继续交由 <see cref="UserContext"/> 拒绝。</param>
        /// <param name="name">显示名称。</param>
        /// <returns>新建上下文或同标识的现有上下文。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="userId"/> 为 <c>null</c> 或空字符串时抛出。</exception>
        /// <exception cref="ArgumentException"><paramref name="userId"/> 仅包含空白字符时由 <see cref="UserContext"/> 抛出。</exception>
        public static UserContext CreateUser(string userId, string name)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId), "UserId cannot be null or empty!");

            if (_userContexts.ContainsKey(userId))
                return _userContexts[userId];

            var context = new UserContext(userId, name);
            _userContexts.Add(userId, context);
            return context;
        }

        /// <summary>
        /// 按标识获取当前注册表中的用户；本方法不会创建缺失的上下文。
        /// </summary>
        /// <param name="userId">非 <c>null</c> 且非空字符串的用户标识。</param>
        /// <returns>已注册上下文；未找到时为 <c>null</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="userId"/> 为 <c>null</c> 或空字符串时抛出。</exception>
        public static UserContext GetUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId), "UserId cannot be null or empty!");

            if (_userContexts.TryGetValue(userId, out var context))
                return context;
            else
                return null;
        }

        /// <summary>
        /// 释放并移除指定用户。空标识或未知标识按空操作处理。
        /// </summary>
        /// <param name="userId">要移除的用户标识。</param>
        public static void RemoveUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;

            if (_userContexts.TryGetValue(userId, out var context))
            {
                context.Dispose();
                _userContexts.Remove(userId);
            }
        }

        /// <summary>
        /// 尽力释放全部用户上下文，并清空注册表及两个全局事件的订阅者。
        /// 单个上下文释放失败不会阻止其余上下文的清理。
        /// </summary>
        public static void Dispose()
        {
            try
            {
                foreach (var context in _userContexts.Values.ToArray())
                {
                    try { context.Dispose(); }
                    catch { }
                }
                _userContexts.Clear();
                OnUserRegistered = null;
                OnConfigChanged = null;
            }
            catch { }
        }
    }
}
