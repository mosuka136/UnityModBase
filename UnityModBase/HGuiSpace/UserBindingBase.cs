using System;
using System.Collections.Generic;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    public abstract class UserBindingBase<T> : IDisposable where T : class
    {
        protected readonly Dictionary<string, T> _user = new Dictionary<string, T>();
        protected readonly Func<UserContext, T> _dataFactory;

        public IEnumerable<string> UserKeys => _user.Keys;
        public IEnumerable<T> UserSheets => _user.Values;


        public UserBindingBase(IEnumerable<UserContext> contexts, Func<UserContext, T> factory)
        {
            _dataFactory = factory;
            foreach (var context in contexts)
                AddData(context);
            UserManager.OnUserRegistered += AddData;
        }

        public virtual void AddData(UserContext context)
        {
            if (context == null || _user.ContainsKey(context.UserId))
                return;
            _user[context.UserId] = _dataFactory(context);
        }

        public virtual T GetData(string key)
        {
            if (_user.TryGetValue(key, out var data))
                return data;
            return default;
        }

        public virtual void Dispose()
        {
            UserManager.OnUserRegistered -= AddData;
            _user.Clear();
        }
    }
}
