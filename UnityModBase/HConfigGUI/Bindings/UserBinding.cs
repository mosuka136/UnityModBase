using UnityModBase.HGuiSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class UserBinding : UserBindingBase<SheetBinding>
    {
        public UserBinding() : base(service => new SheetBinding(service))
        {
            foreach (var context in UserManager.UserContexts)
                AddData(context);
            UserManager.OnUserRegistered += AddData;
        }
    }
}
