using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    public abstract class UserEditorBase : IDisposable
    {
        public bool IsExpanded { get; set; } = false;
        public IUnityProvider UnityService { get; }
        public IUnityGuiProvider UnityGui { get; }

        public UserEditorBase(IUnityProvider unityService, IUnityGuiProvider unityGui)
        {
            UnityService = unityService;
            UnityGui = unityGui;
        }

        public virtual void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            if (users == null)
                throw new ArgumentNullException(nameof(users));

            if (!UserManager.ContainsUser(selectedKey))
                throw new ArgumentException($"The selectedKey '{selectedKey}' does not exist in the user list.", nameof(selectedKey));

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            UnityGui.Space(4);

            if (UnityGui.Button(UserManager.GetUser(selectedKey).Name))
                IsExpanded = !IsExpanded;

            if (IsExpanded)
            {
                var userArray = users.Select(u => u.UserId).ToArray();
                var currentIndex = Array.IndexOf(userArray, selectedKey);
                currentIndex = currentIndex < 0 ? 0 : currentIndex;
                var newIndex = UnityGui.SelectionGrid(currentIndex, users.Select(u => u.Name).ToArray(), 1);

                if (currentIndex != newIndex)
                {
                    selectedKey = userArray[newIndex];
                    IsExpanded = false;
                }
            }

            UnityGui.Space(4);
            UnityGui.EndVertical();
        }

        public abstract void SetStatusDirty(IUserContext context);

        public virtual void Dispose()
        {
        }
    }
}
