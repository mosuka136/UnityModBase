using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HLogSpace;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HLogGUI
{
    public class GroupEditor
    {
        private Vector2 _scrollPosition;

        public LogLevel Level { get; set; } = LogLevel.Info;
        public bool IsColumnWidthDirty { get; set; } = true;
        public bool HasRepeatedEntry { get; private set; } = false;
        public bool HasExceptionEntry { get; private set; } = false;
        public float TotalColumnWidth { get; private set; }
        public Dictionary<EntryContentType, ColumnEditor> ColumnEditorList { get; private set; }
        public EntryEditor EntryEditor { get; }

        public ToastEditor ToastEditor { get; }
        public IUnityGuiProvider UnityGui { get; }
        public IUnityProvider UnityService { get; }
        public StyleResource StyleProvider { get; }

        public GroupEditor(IUnityGuiProvider unityGui, IUnityProvider unityService, StyleResource styleProvider, ToastEditor toastEditor)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
            UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
            StyleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider), "StyleProvider cannot be null.");
            ToastEditor = toastEditor ?? throw new ArgumentNullException(nameof(toastEditor), "ToastEditor cannot be null.");
            EntryEditor = new EntryEditor(unityGui, unityService, toastEditor);
        }

        public class ColumnEditor
        {
            private readonly float MinColumnWidth = 32f;
            private const float ColumnWidthPadding = 18f;

            public bool IsVisible { get; set; } = true;
            public bool ExpendWidth { get; set; } = false;
            public float Width { get; set; } = 0f;

            public EntryContentType ContentType { get; }
            public Translator Header { get; }
            public GUIStyle Style { get; }
            public IUnityGuiProvider UnityGui { get; }
            public IUnityProvider UnityService { get; }

            public ColumnEditor(EntryContentType sortOrder, Translator header, GUIStyle style, IUnityGuiProvider unityGui, IUnityProvider unityService)
            {
                UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui), "UnityGui cannot be null.");
                UnityService = unityService ?? throw new ArgumentNullException(nameof(unityService), "UnityService cannot be null.");
                Style = style ?? throw new ArgumentNullException(nameof(style), "Style cannot be null.");
                Header = header ?? throw new ArgumentNullException(nameof(header), "Header cannot be null.");
                ContentType = sortOrder;
            }

            public void DrawHeader(GroupBinding group)
            {
                if (group == null)
                    throw new ArgumentNullException(nameof(group), "Group cannot be null.");

                if (!IsVisible)
                    return;

                var oldFontStyle = Style.fontStyle;
                if (group.SortOrder == ContentType)
                    Style.fontStyle = group.IsSortDescending ? FontStyle.BoldAndItalic : FontStyle.Bold;

                if (UnityGui.Button(Header, Style, ExpendWidth ? UnityGui.ExpandWidth(true) : UnityGui.Width(Width)))
                {
                    if (group.SortOrder == ContentType)
                        group.IsSortDescending = !group.IsSortDescending;
                    else
                    {
                        group.SortOrder = ContentType;
                        group.IsSortDescending = false;
                    }
                }

                Style.fontStyle = oldFontStyle;
            }

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

            public float MeasureTextWidth(string text)
            {
                var width = Style.CalcSize(UnityGui.GetContent(text)).x + ColumnWidthPadding;
                return UnityService.Max(width, MinColumnWidth);
            }

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

        public void Draw(GuiContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Context cannot be null.");

            var group = context.UserData;

            CheckDrawCondition(group);

            DrawColumnVisibilityMenu();
            DrawColumnLogLevelMenu();

            _scrollPosition = UnityGui.BeginScrollView(_scrollPosition);
            UnityGui.BeginHorizontal(UnityGui.BoxStyle);

            foreach (var columnEditor in ColumnEditorList.Values)
            {
                UnityGui.BeginVertical();
                columnEditor.DrawHeader(group);
                foreach (var entry in group.SortedGroup)
                    columnEditor.DrawCell(entry, EntryEditor, Level);
                UnityGui.EndVertical();
            }

            UnityGui.BeginVertical();
            DrawMiscMenuHeader();
            foreach (var entry in group.SortedGroup)
                DrawMiscMenu(entry);
            UnityGui.EndVertical();

            UnityGui.EndHorizontal();
            UnityGui.EndScrollView();
        }

        public void CheckDrawCondition(GroupBinding group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group), "Group cannot be null.");

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

            if (IsColumnWidthDirty)
            {
                foreach (var columnEditor in ColumnEditorList.Values)
                    columnEditor.UpdateWidth(group, Level);
                UpdateTotalColumnWidth();
                IsColumnWidthDirty = false;
            }

            if (!HasRepeatedEntry && group.HasRepeatedEntry)
            {
                ColumnEditorList[EntryContentType.LastRepeatTime].IsVisible = true;
                ColumnEditorList[EntryContentType.RepeatCount].IsVisible = true;
                HasRepeatedEntry = true;
            }

            if (!HasExceptionEntry && group.HasExceptionEntry)
            {
                ColumnEditorList[EntryContentType.Exception].IsVisible = true;
                HasExceptionEntry = true;
            }
        }

        public void UpdateTotalColumnWidth()
        {
            TotalColumnWidth = 0f;
            foreach (var columnEditor in ColumnEditorList.Values)
            {
                if (columnEditor.IsVisible)
                    TotalColumnWidth += columnEditor.Width + 10f;
            }

            TotalColumnWidth += UnityService.Max(
                UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.MiscMenu)).x,
                UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(TranslatorResource.CopyLog)).x) + 10f;

            var menuWidth = 0f;
            foreach (var columnEditor in ColumnEditorList.Values)
                menuWidth += StyleProvider.ColumnVisibilityToggleStyle.CalcSize(UnityGui.GetContent(columnEditor.Header)).x + 10f;

            TotalColumnWidth = UnityService.Max(TotalColumnWidth, menuWidth);
        }

        public void DrawMiscMenuHeader()
        {
            UnityGui.Button(TranslatorResource.MiscMenu, StyleProvider.MiscButtonStyle, UnityGui.ExpandWidth(true));
        }

        public void DrawMiscMenu(EntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            if (!IsLogLevelHigherOrEqual(entry.Level, Level))
                return;

            if (UnityGui.Button(TranslatorResource.CopyLog, StyleProvider.MiscButtonStyle, UnityGui.ExpandWidth(true)))
            {
                UnityService.ClipboardCopy(entry.ToString());
                ToastEditor.SetToast($"{TranslatorResource.Copied} Log{entry.Id}");
            }
        }

        public void DrawColumnVisibilityMenu()
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle);
            UnityGui.Space(4);
            UnityGui.BeginHorizontal();

            foreach (var columnEditor in ColumnEditorList.Values)
            {
                var isVisible = columnEditor.IsVisible;
                isVisible = UnityGui.Toggle(isVisible, columnEditor.Header, StyleProvider.ColumnVisibilityToggleStyle);
                if (columnEditor.IsVisible != isVisible)
                {
                    columnEditor.IsVisible = isVisible;
                    UpdateTotalColumnWidth();
                }
            }

            UnityGui.EndHorizontal();
            UnityGui.Space(4);
            UnityGui.EndVertical();
        }

        public void DrawColumnLogLevelMenu()
        {
            UnityGui.BeginVertical(UnityGui.BoxStyle);
            UnityGui.Space(4);
            UnityGui.BeginHorizontal();

            foreach (var logLevel in Enum.GetValues(typeof(LogLevel)))
            {
                var level = (LogLevel)logLevel;
                if (UnityGui.Toggle(Level == level, level.ToString(), StyleProvider.ColumnLogLevelToggleStyle))
                {
                    if (level != Level)
                    {
                        Level = level;
                        IsColumnWidthDirty = true;
                    }
                }
                UnityGui.Space(4);
            }

            UnityGui.EndHorizontal();
            UnityGui.Space(4);
            UnityGui.EndVertical();
        }

        public static bool IsLogLevelHigherOrEqual(string entryLogLevel, LogLevel filterLogLevel)
        {
            if (Enum.TryParse(entryLogLevel, out LogLevel entryLevel))
                return entryLevel >= filterLogLevel;
            return false;
        }
    }
}
