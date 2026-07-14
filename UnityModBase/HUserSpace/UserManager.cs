using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;

namespace UnityModBase.HUserSpace
{
    public static class UserManager
    {
        private static readonly Dictionary<string, UserContext> _userContexts = new Dictionary<string, UserContext>();

        public static event Action<UserContext> OnUserRegistered;
        public static event Action<UserContext> OnLogWriterRegistered;
        public static event Action<UserContext> OnConfigRegistered;

        public static IEnumerable<string> UserIds => _userContexts.Keys;
        public static IEnumerable<UserContext> UserContexts => _userContexts.Values;

        public static string GetDefaultUserId()
        {
            return UserIds.FirstOrDefault() ?? string.Empty;
        }

        public static bool ContainsUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;
            return _userContexts.ContainsKey(userId);
        }

        public static UserContext Register(string userId, string name)
        {
            if (string.IsNullOrEmpty(userId))
                userId = $"User_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            while (ContainsUser(userId))
                userId += $"_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            var context = CreateUser(userId, name);

            var h = new ServiceEventHandler(context);
            context.Service.OnLogWriterRegister += h.OnLogWriterRegisteredHandler;
            context.Service.OnConfigRegister += h.OnConfigRegisteredHandler;

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

        private sealed class ServiceEventHandler
        {
            private readonly UserContext _context;

            internal ServiceEventHandler(UserContext context)
            {
                _context = context;
            }

            internal void OnLogWriterRegisteredHandler(LogWriter writer)
            {
                foreach (var handler in OnLogWriterRegistered.GetInvocationListOrEmpty())
                {
                    try
                    {
                        handler.Invoke(_context);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error("Error invoking OnLogWriterRegistered handler!", ex);
                    }
                }
            }

            internal void OnConfigRegisteredHandler(ConfigService config)
            {
                foreach (var handler in OnConfigRegistered.GetInvocationListOrEmpty())
                {
                    try
                    {
                        handler.Invoke(_context);
                    }
                    catch (Exception ex)
                    {
                        BLog.Error("Error invoking OnConfigRegistered handler!", ex);
                    }
                }
            }
        }

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

        public static UserContext GetUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId), "UserId cannot be null or empty!");

            if (_userContexts.TryGetValue(userId, out var context))
                return context;

            return UserContext.InvalidUserContext;
        }

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
                OnLogWriterRegistered = null;
                OnConfigRegistered = null;
            }
            catch { }
        }
    }
}
