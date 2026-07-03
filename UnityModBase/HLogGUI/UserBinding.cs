using System;
using System.Collections.Generic;

namespace UnityModBase.HLogGUI
{
    public class UserBinding : IDisposable
    {
        private readonly Dictionary<string, EntryListBinding> _user = new Dictionary<string, EntryListBinding>();

        public IEnumerable<string> UserKeys => _user.Keys;
        public IEnumerable<EntryListBinding> UserEntryList => _user.Values;

        public UserBinding()
        {
            foreach (var service in ServiceRegistry.Services)
                AddEntryList(service);
            ServiceRegistry.OnServiceRegistered += AddEntryList;
        }

        public void AddEntryList(ServiceRegistry service)
        {
            if (service == null || _user.ContainsKey(service.Name))
                return;
            var list = new EntryListBinding();
            _user[service.Key] = list;
            service.LogDatabase.OnLogAdded += l => list.AddEntry(new EntryBinding(l));
            service.LogDatabase.OnLogRepeated += l => list.AddEntry(new EntryBinding(l));
        }

        public EntryListBinding GetEntryList(string key)
        {
            if (_user.TryGetValue(key, out var entryList))
                return entryList;
            return null;
        }

        public void Dispose()
        {
            ServiceRegistry.OnServiceRegistered -= AddEntryList;
            _user.Clear();
        }
    }
}
