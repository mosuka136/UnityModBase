using System;
using System.Collections.Concurrent;

namespace UnityModBase.HUserSpace
{
    public class UserContext : IDisposable
    {
        private readonly ConcurrentDictionary<string, IUserContext> _contexts;

        public string UserId { get; }
        public string Name { get; }

        public UserService Service { get; set; }

        public UserContext(string userId, string name)
        {
            UserId = userId;
            Name = name ?? string.Empty;
            _contexts = new ConcurrentDictionary<string, IUserContext>();
        }

        public bool AddContext(string key, IUserContext context)
        {
            if (string.IsNullOrWhiteSpace(key) || context == null)
                return false;
            return _contexts.TryAdd(key, context);
        }

        public IUserContext GetContext(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            _contexts.TryGetValue(key, out var context);
            return context;
        }

        public void Dispose()
        {
            Service?.Dispose();
            foreach (var context in _contexts.Values)
                context.Dispose();
            _contexts.Clear();
        }
    }
}
