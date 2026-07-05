using System;

namespace UnityModBase.HConfigGUI.Bindings
{
    public interface IEntryBinding : INodeBinding
    {
        Type ValueType { get; }
        object Value { get; set; }
        IUiMetadata Metadata { get; }
        EntryEditBuffer EditBuffer { get; }

        void ResetValue();
    }
}
