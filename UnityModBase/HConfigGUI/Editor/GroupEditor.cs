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
        public GuiContext Context { get; }
        public StyleResource StyleProvider { get; }
        public LayoutResource LayoutProvider { get; }

        public float EntryLabelWidth { get; private set; } = -1f;
        public float GroupButtonWidth { get; private set; } = -1f;

        public ValueEditorRegistry EditorRegistry { get; }
        public EntryEditor EntryEditor { get; }

        private Vector2 _sidebarScrollPosition = Vector2.zero;
        private Vector2 _contentScrollPosition = Vector2.zero;
        private GroupBinding _currentRoot;

        public GroupEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            GuiContext context,
            StyleResource styleProvider,
            LayoutResource layoutProvider)
        {
            UnityService = unityService;
            UnityGui = unityGui;
            Context = context;
            StyleProvider = styleProvider;
            LayoutProvider = layoutProvider;

            EditorRegistry = new ValueEditorRegistry();
            EditorRegistry.RegisterEditor(new BooleanEditor(unityGui));
            EditorRegistry.RegisterEditor(new StringEditor(unityGui));
            EditorRegistry.RegisterEditor(new SliderEditor(unityGui, unityService, styleProvider));
            EditorRegistry.RegisterEditor(new NumberEditor(unityGui));
            EditorRegistry.RegisterEditor(new EnumEditor(unityGui));
            EditorRegistry.RegisterEditor(new HotkeyEditor(unityGui, styleProvider));

            EntryEditor = new EntryEditor(EditorRegistry, context, unityGui);
        }

        public void Draw(GroupBinding root)
        {
            if (root == null)
            {
                BLog.Error("Root config group is null. Cannot draw group editor.");
                return;
            }

            if (!ReferenceEquals(_currentRoot, root))
            {
                _currentRoot = root;
                _sidebarScrollPosition = Vector2.zero;
                _contentScrollPosition = Vector2.zero;
                UpdateLayout();
            }

            UpdateLayoutIfNeeded(root);

            var groups = GetChildGroups(root);
            if (groups.Count == 0)
            {
                DrawContent(root, false);
                return;
            }

            var selectedGroup = GetSelectedGroup(root, groups);

            UnityGui.BeginHorizontal();
            DrawSidebar(root, groups, ref selectedGroup);
            UnityGui.Space(10f);
            DrawContent(selectedGroup, true);
            UnityGui.EndHorizontal();

            Context.SetText(GetSelectedGroupStateKey(root), selectedGroup.Key);
        }

        public void Update(float deltaTime)
        {
            Context.ChangeSink.FlushValue(deltaTime);
        }

        public void UpdateLayout()
        {
            EntryLabelWidth = -1f;
            GroupButtonWidth = -1f;
        }

        private void UpdateLayoutIfNeeded(GroupBinding root)
        {
            if (EntryLabelWidth < 0f)
            {
                EntryLabelWidth = LayoutProvider.GetEntryLabelWidth(root);
                Context.SetFloat(GuiContext.LeadingBlankWidthKey, EntryLabelWidth);
            }

            if (GroupButtonWidth < 0f)
                GroupButtonWidth = LayoutProvider.GetGroupButtonWidth(root);
        }

        private void DrawSidebar(
            GroupBinding root,
            IReadOnlyList<GroupBinding> groups,
            ref GroupBinding selectedGroup)
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle, UnityGui.Width(GroupButtonWidth));
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
                    Context.SetText(GetSelectedGroupStateKey(root), group.Key);
                }
            }

            UnityGui.EndScrollView();
            UnityGui.EndVertical();
        }

        private void DrawContent(GroupBinding group, bool drawTitle)
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle);
            _contentScrollPosition = UnityGui.BeginScrollView(_contentScrollPosition);
            DrawGroup(group, drawTitle);
            UnityGui.EndScrollView();
            UnityGui.EndVertical();
        }

        private void DrawGroup(GroupBinding group, bool drawTitle)
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
                    EntryEditor.Render(entry);
                    continue;
                }

                if (child is GroupBinding childGroup)
                {
                    UnityGui.Space(10f);
                    DrawGroup(childGroup, true);
                }
            }

            UnityGui.Space(10f);
            UnityGui.EndVertical();
        }

        private GroupBinding GetSelectedGroup(GroupBinding root, IReadOnlyList<GroupBinding> groups)
        {
            var selectedKey = Context.GetText(GetSelectedGroupStateKey(root), groups[0].Key);
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

        private static string GetSelectedGroupStateKey(GroupBinding root)
        {
            return root.Key + GuiContext.Separator + GuiContext.SelectedGroupKey;
        }
    }
}
