using UnityEngine;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace
{
    public interface IStyleResource
    {
        IUnityGuiProvider UnityGui { get; }
        GUIStyle ToastStyle { get; }
        GUIStyle TooltipStyle { get; }
    }
}
