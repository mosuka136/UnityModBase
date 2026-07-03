using System;
using System.Collections.Generic;

namespace UnityModBase.HGuiSpace
{
    public abstract class UserBindingBase<T> : IDisposable where T : class
    {
        protected readonly Dictionary<string, T> _user = new Dictionary<string, T>();
        protected readonly Func<ServiceRegistry, T> _dataFactory;

        public IEnumerable<string> UserKeys => _user.Keys;
        public IEnumerable<T> UserSheets => _user.Values;


        public UserBindingBase(Func<ServiceRegistry, T> factory)
        {
            _dataFactory = factory;
            foreach (var service in ServiceRegistry.Services)
                AddData(service);
            ServiceRegistry.OnServiceRegistered += AddData;
        }

        public virtual void AddData(ServiceRegistry service)
        {
            if (service == null || _user.ContainsKey(service.Key))
                return;
            _user[service.Key] = _dataFactory(service);
        }

        public virtual T GetData(string key)
        {
            if (_user.TryGetValue(key, out var data))
                return data;
            return default;
        }

        public virtual void Dispose()
        {
            ServiceRegistry.OnServiceRegistered -= AddData;
            _user.Clear();
        }
    }
}
