using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor.ValueEditor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class GroupEditor
    {
        public IUnityProvider UnityService { get; }
        public IUnityGuiProvider UnityGui { get; }
        public StyleResource StyleProvider { get; }

        public ValueEditorRegistry EditorRegistry { get; }
        public EntryEditor EntryEditor { get; }
        public HotkeyEditor HotkeyEditor { get; }

        private Vector2 _sidebarScrollPosition = Vector2.zero;
        private Vector2 _contentScrollPosition = Vector2.zero;
        private GroupBinding _currentRoot;

        public GroupEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, StyleResource styleProvider)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
            StyleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider), "StyleProvider cannot be null.");

            EditorRegistry = new ValueEditorRegistry();
            EditorRegistry.RegisterEditor(new BooleanEditor(unityGui));
            EditorRegistry.RegisterEditor(new StringEditor(unityGui));
            EditorRegistry.RegisterEditor(new SliderEditor(unityGui, unityService, styleProvider));
            EditorRegistry.RegisterEditor(new NumberEditor(unityGui));
            EditorRegistry.RegisterEditor(new EnumEditor(unityGui));
            HotkeyEditor = new HotkeyEditor(unityGui, styleProvider);
            EditorRegistry.RegisterEditor(HotkeyEditor);

            EntryEditor = new EntryEditor(EditorRegistry, unityGui);
        }

        public void Draw(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error("Root config group is invalid.");
                return;
            }

            var root = context.UserData;
            if (!ReferenceEquals(_currentRoot, root))
            {
                HotkeyEditor.Session.CancelEdit();
                _currentRoot = root;
                _sidebarScrollPosition = Vector2.zero;
                _contentScrollPosition = Vector2.zero;
                SetLayoutDirty(context);
            }

            UpdateLayoutIfNeeded(root, context);

            var groups = GetChildGroups(root);
            if (groups.Count == 0)
            {
                DrawContent(root, false, context);
                return;
            }

            var selectedGroup = GetSelectedGroup(groups, context);

            UnityGui.BeginHorizontal();
            DrawSidebar(groups, ref selectedGroup, context);
            UnityGui.Space(10f);
            DrawContent(selectedGroup, true, context);
            UnityGui.EndHorizontal();

            context.SelectedGroupKey = selectedGroup.Key;
        }

        public void Update(GuiContext context, float deltaTime)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Invalid GuiContext provided to {nameof(Update)}.");
                return;
            }

            context.ChangeSink.FlushValue(deltaTime);
        }

        public void SetLayoutDirty(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Invalid GuiContext provided to {nameof(SetLayoutDirty)}.");
                return;
            }

            context.SetLayoutDirtyFlags();
        }

        public void UpdateLayoutIfNeeded(GroupBinding root, GuiContext context)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root), "Root config group cannot be null.");

            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Invalid GuiContext provided to {nameof(UpdateLayoutIfNeeded)}.");
                return;
            }

            if (context.IsEntryLabelWidthDirty)
            {
                var groups = GetChildGroups(root);
                foreach (var group in groups)
                {
                    var width = GetEntryLabelWidth(group);
                    context.SetEntryLabelWidth(group.Key, width);
                }
                context.IsEntryLabelWidthDirty = false;
            }

            if (context.IsGroupButtonWidthDirty)
            {
                context.GroupButtonWidth = GetGroupButtonWidth(root);
                context.IsGroupButtonWidthDirty = false;
            }

            if (context.IsResetButtonWidthDirty)
            {
                context.ResetButtonWidth = UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.Reset)).x;
                context.IsResetButtonWidthDirty = false;
            }
        }

        private void DrawSidebar(IReadOnlyList<GroupBinding> groups, ref GroupBinding selectedGroup, GuiContext context)
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle, UnityGui.Width(context.GroupButtonWidth));
            _sidebarScrollPosition = UnityGui.BeginScrollView(_sidebarScrollPosition);

            foreach (var group in groups)
            {
                var isSelected = ReferenceEquals(selectedGroup, group);
                if (!UnityGui.Button(
                    group.Name,
                    isSelected ? StyleProvider.SidebarSelectedEntryStyle : StyleProvider.SidebarEntryStyle,
                    UnityGui.ExpandWidth(true)))
                {
                    continue;
                }

                if (!isSelected)
                {
                    selectedGroup = group;
                    _contentScrollPosition = Vector2.zero;
                    context.SelectedGroupKey = group.Key;
                }
            }

            UnityGui.EndScrollView();
            UnityGui.EndVertical();
        }

        private void DrawContent(GroupBinding group, bool drawTitle, GuiContext context)
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle);
            _contentScrollPosition = UnityGui.BeginScrollView(_contentScrollPosition);
            DrawGroup(group, drawTitle, context);
            UnityGui.EndScrollView();
            UnityGui.EndVertical();
        }

        private void DrawGroup(GroupBinding group, bool drawTitle, GuiContext context)
        {
            UnityGui.BeginVertical();

            if (drawTitle)
            {
                UnityGui.BeginHorizontal();
                UnityGui.Label(
                    UnityGui.GetContent(group.Name, group.Description),
                    StyleProvider.TableTitleStyle,
                    UnityGui.ExpandWidth(true));
                UnityGui.EndHorizontal();
            }

            foreach (var child in group.Children)
            {
                if (child is IEntryBinding entry)
                {
                    EntryEditor.Draw(entry, context);
                    continue;
                }

                if (child is GroupBinding childGroup)
                {
                    UnityGui.Space(10f);
                    DrawGroup(childGroup, true, context);
                }
            }

            UnityGui.Space(10f);
            UnityGui.EndVertical();
        }

        private static GroupBinding GetSelectedGroup(IReadOnlyList<GroupBinding> groups, GuiContext context)
        {
            var selectedKey = context.SelectedGroupKey;
            foreach (var group in groups)
            {
                if (group.Key == selectedKey)
                    return group;
            }

            return groups[0];
        }

        private static List<GroupBinding> GetChildGroups(GroupBinding root)
        {
            var groups = new List<GroupBinding>();
            foreach (var child in root.Children)
            {
                if (child is GroupBinding group)
                    groups.Add(group);
            }

            return groups;
        }

        public float GetEntryLabelWidth(GroupBinding root)
        {
            if (root == null)
                return 0f;

            float maxWidth = 0f;
            foreach (var entry in EnumerateEntries(root))
            {
                var width = UnityGui.LabelStyle.CalcSize(UnityGui.GetContent(entry.Name)).x;
                if (width > maxWidth)
                    maxWidth = width;
            }

            return maxWidth + 10f;
        }

        public float GetGroupButtonWidth(GroupBinding root)
        {
            if (root == null)
                return 0f;

            float maxWidth = 0f;
            foreach (var node in root.Children)
            {
                if (!(node is GroupBinding group))
                    continue;

                var width = UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(group.Name)).x;
                if (width > maxWidth)
                    maxWidth = width;
            }

            return maxWidth + 60f;
        }

        private static IEnumerable<IEntryBinding> EnumerateEntries(GroupBinding group)
        {
            foreach (var child in group.Children)
            {
                if (child is IEntryBinding entry)
                {
                    yield return entry;
                    continue;
                }

                if (!(child is GroupBinding childGroup))
                    continue;

                foreach (var descendant in EnumerateEntries(childGroup))
                    yield return descendant;
            }
        }
    }
}
