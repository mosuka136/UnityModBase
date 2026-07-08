using System;
using System.Linq;
using UnityEngine;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HLogGUI
{
    public class EntryEditor
    {
        public event Action<string> OnEntryCopied;

        public IUnityGuiProvider UnityGui { get; }
        public IUnityProvider UnityService { get; }

        public EntryEditor(IUnityGuiProvider unityGui, IUnityProvider unityService)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
        }

        public void Draw(string text, GUIStyle style, float width)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text), "Text cannot be null.");

            if (style == null)
                throw new ArgumentNullException(nameof(style), "Style cannot be null.");

            UnityGui.BeginHorizontal();

            var splitText = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            splitText = splitText.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var showText = splitText.Length > 0 ? splitText[0] : " ";

            var content = splitText.Length > 1 ? UnityGui.GetContent(showText, text) : UnityGui.GetContent(showText);
            if (UnityGui.Button(content, style, UnityGui.Width(width)))
            {
                UnityService.ClipboardCopy(text);
                OnEntryCopied?.Invoke(TranslatorResource.Copied + showText);
            }

            UnityGui.EndHorizontal();
        }
    }
}
