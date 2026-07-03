using System;
using System.Collections.Generic;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class UserBinding : IDisposable
    {
        private readonly Dictionary<string, SheetBinding> _user = new Dictionary<string, SheetBinding>();

        public IEnumerable<string> UserKeys => _user.Keys;
        public IEnumerable<SheetBinding> UserSheets => _user.Values;

        public UserBinding()
        {
            foreach (var service in ServiceRegistry.Services)
                AddSheet(service);
            ServiceRegistry.OnServiceRegistered += AddSheet;
        }

        public void AddSheet(ServiceRegistry service)
        {
            if (service == null || _user.ContainsKey(service.Key))
                return;
            _user[service.Key] = new SheetBinding(service);
        }

        public SheetBinding GetSheet(string key)
        {
            if (key == null || !_user.ContainsKey(key))
                return null;
            return _user[key];
        }

        public void Dispose()
        {
            ServiceRegistry.OnServiceRegistered -= AddSheet;
            _user.Clear();
        }
    }
}
