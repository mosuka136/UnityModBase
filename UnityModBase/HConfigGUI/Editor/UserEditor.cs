using System;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class UserEditor
    {
        public bool IsExpanded { get; set; } = false;
        public string SelectedKey { get; set; } = null;

        public IUnityProvider UnityService { get; }
        public IUnityGuiProvider UnityGui { get; }
        public StyleResource StyleProvider { get; }

        public SheetEditor SheetEditor { get; }

        public UserEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, GuiStateStore guiStateStore, StyleResource styleProvider)
        {
            UnityService = unityService;
            UnityGui = unityGui;
            StyleProvider = styleProvider;

            SheetEditor = new SheetEditor(unityService, unityGui, guiStateStore, styleProvider);
        }

        public void Draw(UserBinding user)
        {
            if (user == null)
            {
                BLog.Error("User is null. Cannot draw user editor.");
                return;
            }

            var key = SelectedKey ?? user.UserKeys.FirstOrDefault() ?? "No User";

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            UnityGui.Space(4);

            if (UnityGui.Button(key))
                IsExpanded = true;

            if (IsExpanded)
            {
                var userArray = user.UserKeys.ToArray();
                var currentIndex = Array.IndexOf(userArray, key);
                currentIndex = currentIndex < 0 ? 0 : currentIndex;
                var newIndex = UnityGui.SelectionGrid(currentIndex, userArray, 1);

                if (currentIndex != newIndex)
                {
                    key = userArray[newIndex];
                    IsExpanded = false;
                }
            }

            UnityGui.Space(4);
            UnityGui.EndVertical();

            SelectedKey = key;

            SheetEditor.DrawSheet(user.GetSheet(key));
        }

        public void Update(float unscaledDeltaTime)
        {
            SheetEditor.Update(unscaledDeltaTime);
        }

        public void UpdateLayout()
        {
            SheetEditor.UpdateLayout();
        }
    }
}
