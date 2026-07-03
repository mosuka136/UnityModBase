using System;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public interface IEntryBinding
    {
        string Key { get; }
        Translator Name { get; }
        Translator Description { get; }
        Type ValueType { get; }
        object Value { get; set; }
        IUiMetadata Metadata { get; }

        void ResetValue();
    }
}
