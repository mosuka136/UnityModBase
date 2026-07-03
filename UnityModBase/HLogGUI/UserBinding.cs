using UnityModBase.HGuiSpace;

namespace UnityModBase.HLogGUI
{
    public class UserBinding : UserBindingBase<EntryListBinding>
    {
        public UserBinding() : base((service) => new EntryListBinding())
        {
            foreach (var service in ServiceRegistry.Services)
                AddData(service);
            ServiceRegistry.OnServiceRegistered += AddData;
        }

        public override void AddData(ServiceRegistry service)
        {
            if (service == null || _user.ContainsKey(service.Name))
                return;
            var list = new EntryListBinding();
            _user[service.Key] = list;

            foreach (var log in service.LogDatabase.Logs)
                list.AddEntry(new EntryBinding(log));
            service.LogDatabase.OnLogAdded += l => list.AddEntry(new EntryBinding(l));
            service.LogDatabase.OnLogRepeated += l => list.AddEntry(new EntryBinding(l));
        }
    }
}
