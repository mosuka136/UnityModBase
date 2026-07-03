using UnityModBase.HGuiSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class UserBinding : UserBindingBase<SheetBinding>
    {
        public UserBinding() : base(service => new SheetBinding(service))
        {
            foreach (var service in ServiceRegistry.Services)
                AddData(service);
            ServiceRegistry.OnServiceRegistered += AddData;
        }
    }
}
