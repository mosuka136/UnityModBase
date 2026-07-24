using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HLogGUI
{
    /// <summary>
    /// 绘制可筛选、可排序、可选择列的日志表格，并维护列编辑器、列宽与滚动状态。
    /// 日志数据属于各用户 <see cref="GuiContext"/>，而等级筛选、列可见性和滚动位置属于本编辑器实例，会在用户之间共享。
    /// </summary>
    /// <remarks>
    /// 表格绘制会进入剪贴板服务、事件订阅者和可替换的 IMGUI 提供器；所有已成功开启的滚动、水平和垂直布局作用域
    /// 都在 <c>finally</c> 中闭合。异常仍向宿主传播，不会在本类中转换为日志或空操作。
    /// </remarks>
    public class GroupEditor
    {
        private Vector2 _scrollPosition;

        /// <summary>
        /// 完整日志复制成功后触发，参数为用于短时提示的摘要文本。
        /// </summary>
        public event Action<string> OnLogCopied;

        /// <summary>
        /// 获取或设置最低显示等级；比较依赖 <see cref="LogLevel"/> 的枚举顺序。
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;
        /// <summary>
        /// 获取按日志字段索引的列编辑器集合。首次绘制检查时延迟创建，之后由所有用户上下文共享。
        /// </summary>
        public Dictionary<EntryContentType, ColumnEditor> ColumnEditorList { get; private set; }
        /// <summary>
        /// 获取负责绘制和复制单个日志字段的编辑器。
        /// </summary>
        public EntryEditor EntryEditor { get; }

        /// <summary>
        /// 获取日志表格绘制所用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取剪贴板和文本宽度运算所用的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }
        /// <summary>
        /// 获取日志列和菜单样式资源。
        /// </summary>
        public StyleResource StyleProvider { get; }

        /// <summary>
        /// 创建日志表格编辑器。
        /// </summary>
        /// <param name="unityGui">用于绘制日志表格的 IMGUI 提供器。</param>
        /// <param name="unityService">用于剪贴板和文本宽度运算的 Unity 服务。</param>
        /// <param name="styleProvider">提供日志列和菜单样式的资源。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public GroupEditor(IUnityGuiProvider unityGui, IUnityProvider unityService, StyleResource styleProvider)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
            StyleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider), "StyleProvider cannot be null.");
            EntryEditor = new EntryEditor(unityGui, unityService);
        }

        /// <summary>
        /// 管理单个日志字段列的可见性、宽度、排序表头和单元格文本提取。
        /// 传入样式通常来自共享资源；绘制排序状态时对字体的临时修改必须在本次调用结束前恢复。
        /// </summary>
        public class ColumnEditor
        {
            private const float MinColumnWidth = 32f;
            private const float ColumnWidthPadding = 18f;

            /// <summary>
            /// 获取或设置该列是否参与表格绘制和总宽度计算。
            /// </summary>
            public bool IsVisible { get; set; } = true;
            /// <summary>
            /// 获取或设置当前测量得到的列宽，单位为像素。
            /// </summary>
            public float Width { get; set; } = 0f;

            /// <summary>
            /// 获取该列对应的日志字段和排序键。
            /// </summary>
            public EntryContentType ContentType { get; }
            /// <summary>
            /// 获取本地化列标题。
            /// </summary>
            public Translator Header { get; }
            /// <summary>
            /// 获取表头和单元格共享的可变 GUI 样式。
            /// </summary>
            public GUIStyle Style { get; }
            /// <summary>
            /// 获取该列使用的 IMGUI 提供器。
            /// </summary>
            public IUnityGuiProvider UnityGui { get; }
            /// <summary>
            /// 获取该列用于宽度运算的 Unity 服务。
            /// </summary>
            public IUnityProvider UnityService { get; }

            /// <summary>
            /// 创建日志字段列编辑器。
            /// </summary>
            /// <param name="sortOrder">列对应的字段和排序键。</param>
            /// <param name="header">本地化列标题。</param>
            /// <param name="style">表头和单元格共享样式。</param>
            /// <param name="unityGui">用于绘制和测量内容的 IMGUI 提供器。</param>
            /// <param name="unityService">用于宽度运算的 Unity 服务。</param>
            /// <exception cref="ArgumentNullException">标题、样式或任一服务为 null。</exception>
            public ColumnEditor(EntryContentType sortOrder, Translator header, GUIStyle style, IUnityGuiProvider unityGui, IUnityProvider unityService)
            {
                UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
                UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
                Style = style ?? throw new ArgumentNullException(nameof(style), "Style cannot be null.");
                Header = header ?? throw new ArgumentNullException(nameof(header), "Header cannot be null.");
                ContentType = sortOrder;
            }

            /// <summary>
            /// 绘制可点击排序表头。连续点击当前列会切换升降序，选择新列时从升序开始。
            /// </summary>
            /// <param name="group">接收排序状态变化的日志分组。</param>
            /// <exception cref="ArgumentNullException"><paramref name="group"/> 为 null。</exception>
            public void DrawHeader(GroupBinding group)
            {
                if (group == null)
                    throw new ArgumentNullException(nameof(group), "Group cannot be null.");

                if (!IsVisible)
                    return;

                var oldFontStyle = Style.fontStyle;
                if (group.SortOrder == ContentType)
                    Style.fontStyle = group.IsSortDescending ? FontStyle.BoldAndItalic : FontStyle.Bold;

                if (UnityGui.Button(Header, Style, UnityGui.Width(Width)))
                {
                    if (group.SortOrder == ContentType)
                        group.IsSortDescending = !group.IsSortDescending;
                    else
                    {
                        group.SortOrder = ContentType;
                        group.IsSortDescending = false;
                    }
                }

                // StyleResource 缓存并共享样式实例，必须恢复字体状态，避免排序强调泄漏到后续单元格或其他列。
                Style.fontStyle = oldFontStyle;
            }

            /// <summary>
            /// 在列可见且日志达到最低等级时绘制单元格；点击复制行为由 <paramref name="editor"/> 处理。
            /// </summary>
            /// <param name="entry">要绘制的日志绑定。</param>
            /// <param name="editor">负责绘制和复制字段文本的编辑器。</param>
            /// <param name="level">最低显示日志等级。</param>
            /// <exception cref="ArgumentNullException"><paramref name="entry"/> 或 <paramref name="editor"/> 为 null。</exception>
            public void DrawCell(EntryBinding entry, EntryEditor editor, LogLevel level)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

                if (editor == null)
                    throw new ArgumentNullException(nameof(editor), "Editor cannot be null.");

                if (!IsVisible)
                    return;

                if (!IsLogLevelHigherOrEqual(entry.Level, level))
                    return;

                editor.Draw(GetText(entry, ContentType), Style, Width);
            }

            /// <summary>
            /// 按当前等级筛选后的日志首行和表头测量列宽，并应用最小宽度与水平留白。
            /// 多行字段的后续内容通过工具提示查看，不参与宽度计算。
            /// </summary>
            /// <param name="group">提供当前排序日志视图的分组。</param>
            /// <param name="level">参与测量的最低日志等级。</param>
            /// <exception cref="ArgumentNullException"><paramref name="group"/> 为 null。</exception>
            public void UpdateWidth(GroupBinding group, LogLevel level)
            {
                if (group == null)
                    throw new ArgumentNullException(nameof(group), "Group cannot be null.");

                var width = 0f;
                foreach (var entry in group.SortedGroup)
                {
                    if (!IsLogLevelHigherOrEqual(entry.Level, level))
                        continue;
                    var splitText = GetText(entry, ContentType).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var showText = splitText.Length > 0 ? splitText[0] : " ";
                    width = UnityService.Max(width, MeasureTextWidth(showText));
                }
                Width = UnityService.Max(width, MeasureTextWidth(Header));
            }

            /// <summary>
            /// 计算单行文本所需宽度，并应用 18 像素留白和 32 像素最小值。
            /// </summary>
            /// <param name="text">要测量的单行文本。</param>
            /// <returns>应用留白和最小值后的宽度。</returns>
            public float MeasureTextWidth(string text)
            {
                var width = Style.CalcSize(UnityGui.GetContent(text)).x + ColumnWidthPadding;
                return UnityService.Max(width, MinColumnWidth);
            }

            /// <summary>
            /// 将字段类型映射到绑定的显示文本；<see cref="EntryContentType.None"/> 或未知值返回空字符串。
            /// </summary>
            /// <param name="entry">要读取的日志绑定。</param>
            /// <param name="contentType">目标日志字段。</param>
            /// <returns>对应字段的显示文本，或空字符串。</returns>
            /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
            public static string GetText(EntryBinding entry, EntryContentType contentType)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

                switch (contentType)
                {
                    case EntryContentType.Id:
                        return entry.Id;
                    case EntryContentType.Timestamp:
                        return entry.Timestamp;
                    case EntryContentType.ThreadId:
                        return entry.ThreadId;
                    case EntryContentType.Frame:
                        return entry.Frame;
                    case EntryContentType.Scene:
                        return entry.Scene;
                    case EntryContentType.Level:
                        return entry.Level;
                    case EntryContentType.Message:
                        return entry.Message;
                    case EntryContentType.File:
                        return entry.File;
                    case EntryContentType.Line:
                        return entry.Line;
                    case EntryContentType.Member:
                        return entry.Member;
                    case EntryContentType.Exception:
                        return entry.Exception;
                    case EntryContentType.LastRepeatTime:
                        return entry.LastRepeatTime;
                    case EntryContentType.RepeatCount:
                        return entry.RepeatCount;
                    default:
                        return string.Empty;
                }
            }
        }

        /// <summary>
        /// 检查列状态和宽度缓存后，按列绘制排序表头、过滤后的单元格及逐行复制入口。
        /// </summary>
        /// <param name="context">提供日志分组和列布局状态的 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void Draw(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            CheckDrawCondition(context);

            DrawColumnVisibilityMenu(context);
            DrawColumnLogLevelMenu(context);

            _scrollPosition = UnityGui.BeginScrollView(_scrollPosition);
            try
            {
                UnityGui.BeginHorizontal(UnityGui.BoxStyle);
                try
                {
                    var groupBinding = context.UserData;
                    var sortedGroup = groupBinding.SortedGroup;
                    foreach (var columnEditor in ColumnEditorList.Values)
                    {
                        UnityGui.BeginVertical();
                        try
                        {
                            columnEditor.DrawHeader(groupBinding);
                            foreach (var entry in sortedGroup)
                                columnEditor.DrawCell(entry, EntryEditor, Level);
                        }
                        finally
                        {
                            UnityGui.EndVertical();
                        }
                    }

                    UnityGui.BeginVertical();
                    try
                    {
                        DrawMiscMenuHeader();
                        foreach (var entry in sortedGroup)
                            DrawMiscMenu(entry);
                    }
                    finally
                    {
                        UnityGui.EndVertical();
                    }
                }
                finally
                {
                    UnityGui.EndHorizontal();
                }
            }
            finally
            {
                UnityGui.EndScrollView();
            }
        }

        /// <summary>
        /// 延迟创建列编辑器，按日志内容自动显示重复和异常列，并在上下文标脏时重新测量全部列宽。
        /// 自动显示标志在上下文生命周期内只从 false 变为 true；日志移除后不会自动隐藏已出现过的特殊列。
        /// </summary>
        /// <param name="context">提供日志分组和列布局状态的 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void CheckDrawCondition(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            if (ColumnEditorList == null)
            {
                ColumnEditorList = new Dictionary<EntryContentType, ColumnEditor>()
                {
                    { EntryContentType.Id, new ColumnEditor(EntryContentType.Id, TranslatorResource.IdTopBar, StyleProvider.IdButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Timestamp, new ColumnEditor(EntryContentType.Timestamp, TranslatorResource.TimestampTopBar, StyleProvider.TimestampButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.LastRepeatTime, new ColumnEditor(EntryContentType.LastRepeatTime, TranslatorResource.LastRepeatTimeTopBar, StyleProvider.LastRepeatTimeButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.ThreadId, new ColumnEditor(EntryContentType.ThreadId, TranslatorResource.ThreadIdTopBar, StyleProvider.ThreadIdButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Frame, new ColumnEditor(EntryContentType.Frame, TranslatorResource.FrameTopBar, StyleProvider.FrameButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Scene, new ColumnEditor(EntryContentType.Scene, TranslatorResource.SceneTopBar, StyleProvider.SceneButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Level, new ColumnEditor(EntryContentType.Level, TranslatorResource.LevelTopBar, StyleProvider.LevelButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Message, new ColumnEditor(EntryContentType.Message, TranslatorResource.MessageTopBar, StyleProvider.MessageButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Exception, new ColumnEditor(EntryContentType.Exception, TranslatorResource.ExceptionTopBar, StyleProvider.ExceptionButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.File, new ColumnEditor(EntryContentType.File, TranslatorResource.FileTopBar, StyleProvider.FileButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Line, new ColumnEditor(EntryContentType.Line, TranslatorResource.LineTopBar, StyleProvider.LineButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.Member, new ColumnEditor(EntryContentType.Member, TranslatorResource.MemberTopBar, StyleProvider.MemberButtonStyle, UnityGui, UnityService) },
                    { EntryContentType.RepeatCount, new ColumnEditor(EntryContentType.RepeatCount, TranslatorResource.RepeatCountTopBar, StyleProvider.RepeatCountButtonStyle, UnityGui, UnityService) },
                };

                ColumnEditorList[EntryContentType.LastRepeatTime].IsVisible = false;
                ColumnEditorList[EntryContentType.Exception].IsVisible = false;
                ColumnEditorList[EntryContentType.File].IsVisible = false;
                ColumnEditorList[EntryContentType.Line].IsVisible = false;
                ColumnEditorList[EntryContentType.Member].IsVisible = false;
                ColumnEditorList[EntryContentType.RepeatCount].IsVisible = false;
            }

            var group = context.UserData;

            if (!context.HasRepeatedEntry && group.HasRepeatedEntry)
            {
                ColumnEditorList[EntryContentType.LastRepeatTime].IsVisible = true;
                ColumnEditorList[EntryContentType.RepeatCount].IsVisible = true;
                context.HasRepeatedEntry = true;
            }

            if (!context.HasExceptionEntry && group.HasExceptionEntry)
            {
                ColumnEditorList[EntryContentType.Exception].IsVisible = true;
                context.HasExceptionEntry = true;
            }

            if (context.IsColumnWidthDirty)
            {
                // 只确认测量开始时的版本；测量期间到达的新日志会递增版本，并在下一次绘制时再次触发测量。
                int columnWidthVersion = context.CaptureColumnWidthVersion();
                foreach (var columnEditor in ColumnEditorList.Values)
                    columnEditor.UpdateWidth(group, Level);
                UpdateTotalColumnWidth(context);
                context.CompleteColumnWidthMeasurement(columnWidthVersion);
            }
        }

        /// <summary>
        /// 根据当前可见列、逐行复制列和列选择菜单计算窗口所需总宽度。
        /// 结果写入上下文，宿主仍会将最终窗口宽度限制在屏幕的 50%～90%。
        /// </summary>
        /// <param name="context">接收总列宽的日志 GUI 上下文。</param>
        public void UpdateTotalColumnWidth(GuiContext context)
        {
            var width = 0f;
            foreach (var columnEditor in ColumnEditorList.Values)
            {
                if (columnEditor.IsVisible)
                    width += columnEditor.Width + 10f;
            }

            width += UnityService.Max(
                UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.MiscMenu)).x,
                UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.CopyLog)).x) + 10f;

            var menuWidth = 0f;
            foreach (var columnEditor in ColumnEditorList.Values)
                menuWidth += StyleProvider.ColumnVisibilityToggleStyle.CalcSize(UnityGui.GetContent(columnEditor.Header)).x + 10f;

            context.TotalColumnWidth = UnityService.Max(width, menuWidth);
        }

        /// <summary>
        /// 绘制逐行操作列的固定表头。
        /// </summary>
        public void DrawMiscMenuHeader()
        {
            UnityGui.Button(TranslatorResource.MiscMenu, StyleProvider.MiscButtonStyle, UnityGui.ExpandWidth(true));
        }

        /// <summary>
        /// 绘制整条日志复制按钮；只处理达到当前最低等级的条目。
        /// 点击会把 <see cref="EntryBinding.ToString"/> 的完整内容写入系统剪贴板并发送提示事件。
        /// </summary>
        /// <param name="entry">要提供整条复制操作的日志绑定。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void DrawMiscMenu(EntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            if (!IsLogLevelHigherOrEqual(entry.Level, Level))
                return;

            if (UnityGui.Button(TranslatorResource.CopyLog, StyleProvider.MiscButtonStyle, UnityGui.ExpandWidth(true)))
            {
                UnityService.ClipboardCopy(entry.ToString());
                OnLogCopied?.Invoke($"{TranslatorResource.Copied} Log{entry.Id}");
            }
        }

        /// <summary>
        /// 绘制所有列的可见性开关。变化会立即重算窗口总宽度，但不重新测量单列内容宽度。
        /// </summary>
        /// <param name="context">接收总列宽变化的日志 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void DrawColumnVisibilityMenu(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            try
            {
                UnityGui.Space(4);
                UnityGui.BeginHorizontal();
                try
                {
                    foreach (var columnEditor in ColumnEditorList.Values)
                    {
                        var isVisible = columnEditor.IsVisible;
                        isVisible = UnityGui.Toggle(isVisible, columnEditor.Header, StyleProvider.ColumnVisibilityToggleStyle);
                        if (columnEditor.IsVisible != isVisible)
                        {
                            columnEditor.IsVisible = isVisible;
                            UpdateTotalColumnWidth(context);
                        }
                    }
                }
                finally
                {
                    UnityGui.EndHorizontal();
                }

                UnityGui.Space(4);
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }

        /// <summary>
        /// 绘制最低日志等级选择。等级变化会标记列宽为脏，测量将在下一次绘制检查中完成。
        /// </summary>
        /// <param name="context">接收列宽失效标记的日志 GUI 上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void DrawColumnLogLevelMenu(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            try
            {
                UnityGui.Space(4);
                UnityGui.BeginHorizontal();
                try
                {
                    foreach (var logLevel in Enum.GetValues(typeof(LogLevel)))
                    {
                        var level = (LogLevel)logLevel;
                        if (UnityGui.Toggle(Level == level, level.ToString(), StyleProvider.ColumnLogLevelToggleStyle))
                        {
                            if (level != Level)
                            {
                                Level = level;
                                context.IsColumnWidthDirty = true;
                            }
                        }
                        UnityGui.Space(4);
                    }
                }
                finally
                {
                    UnityGui.EndHorizontal();
                }

                UnityGui.Space(4);
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }

        /// <summary>
        /// 按 <see cref="LogLevel"/> 枚举顺序判断日志是否达到筛选等级；无法解析的等级文本视为不匹配。
        /// </summary>
        /// <param name="entryLogLevel">日志绑定提供的等级名称。</param>
        /// <param name="filterLogLevel">最低显示等级。</param>
        /// <returns>等级可解析且不低于筛选等级时返回 true。</returns>
        public static bool IsLogLevelHigherOrEqual(string entryLogLevel, LogLevel filterLogLevel)
        {
            if (Enum.TryParse(entryLogLevel, out LogLevel entryLevel))
                return entryLevel >= filterLogLevel;
            return false;
        }
    }
}
