using UnityModBase.HUserSpace;

namespace UnityModBase.HConfigGUI
{
    public class GuiContext : IUserContext
    {
        public GuiStateStore GuiStateStore { get; private set; }
        public EntryChangeSink ChangeSink { get; private set; }

        public float EntryLabelWidth { get; private set; } = -1f;
        public float GroupButtonWidth { get; private set; } = -1f;

        public GuiContext()
        {
            GuiStateStore = new GuiStateStore();
            ChangeSink = new EntryChangeSink();
        }

        public void Dispose()
        {
            
        }
    }
}
