using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    public interface INodeBinding
    {
        string Key { get; }
        Translator Name { get; }
        Translator Description { get; }
    }
}
