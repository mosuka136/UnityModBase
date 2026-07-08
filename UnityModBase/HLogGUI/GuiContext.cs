using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    public class GuiContext : IUserContext
    {
        public GroupBinding UserData { get; set; }

        public void Dispose()
        {
            
        }
    }
}
