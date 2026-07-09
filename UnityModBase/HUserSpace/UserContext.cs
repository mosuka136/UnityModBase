using System;
using System.Collections.Concurrent;

namespace UnityModBase.HUserSpace
{
    public class UserContext : IUserContext
    {
        private readonly ConcurrentDictionary<string, IUserContext> _contexts;

        public string UserId { get; }
        public string Name { get; }

        public UserService Service { get; set; }

        public readonly static UserContext InvalidUserContext = new UserContext("Invalid User");
        public bool IsValid => !ReferenceEquals(this, InvalidUserContext);

        private UserContext(string name)
        {
            UserId = string.Empty;
            Name = name ?? string.Empty;
            _contexts = new ConcurrentDictionary<string, IUserContext>();
        }

        public UserContext(string userId, string name)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or whitespace.", nameof(userId));

            UserId = userId;
            Name = name ?? string.Empty;
            _contexts = new ConcurrentDictionary<string, IUserContext>();
        }

        public bool AddContext(string key, IUserContext context)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            return _contexts.TryAdd(key, context);
        }

        public IUserContext GetContext(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (_contexts.TryGetValue(key, out var context))
                return context;

            return InvalidUserContext;
        }

        public void Dispose()
        {
            foreach (var context in _contexts.Values)
                context.Dispose();
            _contexts.Clear();
            Service?.Dispose();
        }
    }
}
