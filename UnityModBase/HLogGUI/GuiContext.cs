using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    public class GuiContext : IUserContext
    {
        public GroupBinding UserData { get; set; }

        public bool IsColumnWidthDirty { get; set; } = true;
        public bool HasRepeatedEntry { get; set; } = false;
        public bool HasExceptionEntry { get; set; } = false;
        public float TotalColumnWidth { get; set; } = 0f;

        public void Dispose()
        {
            
        }
    }
}
