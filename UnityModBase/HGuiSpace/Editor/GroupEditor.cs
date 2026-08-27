using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// 负责通用绑定树的分组导航、递归内容绘制、布局尺寸缓存刷新和延迟值提交。
    /// 编辑器本身保存当前根节点及滚动位置，用户相关选择和尺寸缓存保存在 <see cref="EditableGuiContext"/>。
    /// </summary>
    /// <remarks>
    /// 分组树最终会进入可扩展的值编辑器和提交回调，因此每个已成功开启的 IMGUI 布局与滚动视图都在
    /// <c>finally</c> 中闭合；异常仍向调用方传播，但不会把未配对的布局状态带入后续窗口或帧。
    /// </remarks>
    public class GroupEditor : IDisposable
    {
        /// <summary>
        /// 获取布局测量和数值运算使用的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }

        /// <summary>
        /// 获取分组绘制所用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 获取侧栏、标题和滑条样式资源。
        /// </summary>
        public IEntryStyleResource StyleProvider { get; }

        /// <summary>
        /// 获取单项条目行编辑器。
        /// </summary>
        public EntryEditor EntryEditor { get; }

        /// <summary>
        /// 获取本分组编辑器独占的值编辑器注册表。
        /// </summary>
        public ValueEditorRegistry ValueEditors => _valueEditors;

        private Vector2 _sidebarScrollPosition = Vector2.zero;
        private Vector2 _contentScrollPosition = Vector2.zero;
        private GroupBinding _currentRoot;
        private readonly ValueEditorRegistry _valueEditors;
        private readonly Action _onRootChanged;

        /// <summary>
        /// 创建拥有独立值编辑器注册表的分组编辑器。
        /// </summary>
        public GroupEditor(
            IUnityProvider unityService,
            IUnityGuiProvider unityGui,
            IEntryStyleResource styleProvider,
            ValueEditorRegistry valueEditors,
            EntryEditor entryEditor = null,
            Action onRootChanged = null)
        {
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
            StyleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider), "StyleProvider cannot be null.");
            _valueEditors = valueEditors ?? throw new ArgumentNullException(nameof(valueEditors));
            EntryEditor = entryEditor ?? new EntryEditor(unityGui, valueEditors);
            _onRootChanged = onRootChanged;
        }

        /// <summary>
        /// 绘制当前用户的绑定树。根节点变化时重置滚动位置、调用扩展回调并使布局缓存失效。
        /// 没有一级分组的根节点会直接作为内容绘制，不显示侧边栏。
        /// </summary>
        /// <param name="context">包含根绑定和用户级显示状态的可编辑 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void Draw(EditableGuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Group draw skipped because the GUI context is invalid. SelectedGroup='{context.SelectedGroupKey}'.");
                return;
            }

            var root = context.UserData;
            if (!ReferenceEquals(_currentRoot, root))
            {
                _onRootChanged?.Invoke();
                _currentRoot = root;
                _sidebarScrollPosition = Vector2.zero;
                _contentScrollPosition = Vector2.zero;
                context.SetLayoutDirtyFlags();
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
            try
            {
                DrawSidebar(groups, ref selectedGroup, context);
                UnityGui.Space(10f);
                DrawContent(selectedGroup, true, context);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }

            context.SelectedGroupKey = selectedGroup.Key;
        }

        /// <summary>
        /// 推进指定上下文的延迟值提交，不处理其他用户上下文中的待提交项。
        /// </summary>
        /// <param name="context">要推进延迟提交的可编辑 GUI 上下文。</param>
        /// <param name="deltaTime">用于递减提交延迟的时间增量，通常为非缩放秒数。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void Update(EditableGuiContext context, float deltaTime)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Editor update skipped because the GUI context is invalid. Operation='{nameof(Update)}', DeltaTime={deltaTime}.");
                return;
            }

            context.ChangeSink.FlushValue(deltaTime);
        }

        /// <summary>
        /// 仅重算上下文中标记为脏的标签宽度、侧栏宽度和尾部操作宽度。
        /// 这些尺寸依赖翻译文本和绑定结构，调用方应在相应内容变化后先设置脏标记。
        /// </summary>
        /// <param name="root">要测量的根绑定。</param>
        /// <param name="context">保存尺寸缓存和脏标记的 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> 或 <paramref name="context"/> 为 null。</exception>
        public void UpdateLayoutIfNeeded(GroupBinding root, EditableGuiContext context)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root), "Root config group cannot be null.");

            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (!context.IsValid)
            {
                BLog.Error($"Layout update skipped because the GUI context is invalid. Operation='{nameof(UpdateLayoutIfNeeded)}'.");
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

            if (context.IsTrailingActionWidthDirty)
            {
                context.TrailingActionWidth = EntryEditor.TrailingActionWidth;
                context.IsTrailingActionWidthDirty = false;
            }
        }

        private void DrawSidebar(IReadOnlyList<GroupBinding> groups, ref GroupBinding selectedGroup, EditableGuiContext context)
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle, UnityGui.Width(context.GroupButtonWidth));
            try
            {
                _sidebarScrollPosition = UnityGui.BeginScrollView(_sidebarScrollPosition);
                try
                {
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
                }
                finally
                {
                    UnityGui.EndScrollView();
                }
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }

        private void DrawContent(GroupBinding group, bool drawTitle, EditableGuiContext context)
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle);
            try
            {
                _contentScrollPosition = UnityGui.BeginScrollView(_contentScrollPosition);
                try
                {
                    DrawGroup(group, drawTitle, context);
                }
                finally
                {
                    UnityGui.EndScrollView();
                }
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }

        private void DrawGroup(GroupBinding group, bool drawTitle, EditableGuiContext context)
        {
            UnityGui.BeginVertical();
            try
            {
                if (drawTitle)
                {
                    UnityGui.BeginHorizontal();
                    try
                    {
                        UnityGui.Label(
                            UnityGui.GetContent(group.Name, group.Description),
                            StyleProvider.TableTitleStyle,
                            UnityGui.ExpandWidth(true));
                    }
                    finally
                    {
                        UnityGui.EndHorizontal();
                    }
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
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }

        private static GroupBinding GetSelectedGroup(IReadOnlyList<GroupBinding> groups, EditableGuiContext context)
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

        /// <summary>
        /// 计算分组及其后代条目名称所需的最大标签宽度，并附加 10 像素间距。
        /// </summary>
        /// <param name="root">要递归测量的分组；null 时返回 0。</param>
        /// <returns>标签最大宽度加 10 像素间距；根为 null 时返回 0。</returns>
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

        /// <summary>
        /// 计算根节点直属分组按钮的最大宽度，并附加 60 像素侧栏余量。
        /// </summary>
        /// <param name="root">要测量直属分组的根节点；null 时返回 0。</param>
        /// <returns>按钮最大宽度加 60 像素余量；根为 null 时返回 0。</returns>
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

        /// <summary>
        /// 释放并清空本分组编辑器独占的值编辑器注册表。
        /// </summary>
        public void Dispose()
        {
            try
            {
                _valueEditors.Dispose();
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to dispose the value editor registry.", ex);
            }
        }
    }
}
