using System;
using UnityEngine;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class PopupEditor
    {
        public readonly int PopupID = Guid.NewGuid().GetHashCode();

        public Rect PopupRect { get; private set; }
        public IUnityGuiProvider UnityGui { get; }
        public StyleResource StyleProvider { get; }

        public PopupEditor(IUnityGuiProvider unityGui, StyleResource styleProvider)
        {
            UnityGui = unityGui;
            StyleProvider = styleProvider;

            float width = UnityGui.ScreenWidth * 0.25f;
            float height = UnityGui.ScreenHeight * 0.15f;
            PopupRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);
        }

        public void DrawPopup(GuiContext context)
        {
            if (context == null || !context.IsValid)
                return;

            var color = UnityGui.Color;
            UnityGui.Color = new Color(0f, 0f, 0f, 0.55f);
            UnityGui.Box(new Rect(0f, 0f, UnityGui.ScreenWidth, UnityGui.ScreenHeight), string.Empty);
            UnityGui.Color = color;

            PopupRect = UnityGui.ModalWindow(PopupID, PopupRect, id => DrawPopupWindow(id, context), string.Empty, UnityGui.BoxStyle);
        }

        private void DrawPopupWindow(int id, GuiContext context)
        {
            var title = context.Popup.Title;
            var drawAction = context.Popup.DrawAction;
            var closeAction = context.Popup.CloseAction;

            UnityGui.BeginVertical();
            UnityGui.Label(title, StyleProvider.PopupTitleStyle, UnityGui.ExpandWidth(true));

            drawAction?.Invoke();

            if (UnityGui.Button(TranslatorResource.Close, UnityGui.ExpandWidth(true)))
            {
                closeAction?.Invoke();
                context.Popup.IsOpen = false;
            }

            UnityGui.EndVertical();
        }
    }
}
