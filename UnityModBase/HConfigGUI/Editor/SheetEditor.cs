using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    public class SheetEditor
    {
        public IUnityProvider UnityService { get; }
        public IUnityGuiProvider UnityGui { get; }
        public GuiStateStore GuiStateStore { get; }
        public StyleResource StyleProvider { get; }

        public float EntryLabelWidth { get; set; } = -1f;
        public float TableButtonWidth { get; set; } = -1f;

        public TableEditor TableEditor { get; }

        private Vector2 _sidebarScrollPosition = Vector2.zero;
        private Vector2 _contentScrollPosition = Vector2.zero;

        public SheetEditor(IUnityProvider unityService, IUnityGuiProvider unityGui, GuiStateStore guiStateStore, StyleResource styleProvider)
        {
            UnityService = unityService;
            UnityGui = unityGui;
            GuiStateStore = guiStateStore;
            StyleProvider = styleProvider;

            TableEditor = new TableEditor(unityService, unityGui, guiStateStore, styleProvider);
        }

        public void DrawSheet(SheetBinding sheet)
        {
            if (sheet == null)
            {
                BLog.Error("Sheet is null. Cannot draw sheet editor.");
                return;
            }

            if (EntryLabelWidth < 0f)
            {
                EntryLabelWidth = GetEntryLabelWidth(sheet);
                GuiStateStore.SetFloat(GuiStateStore.LeadingBlankWidthKey, EntryLabelWidth);
            }

            if (TableButtonWidth < 0f)
                TableButtonWidth = GetTableButtonWidth(sheet);

            var selectedTableIndex = GuiStateStore.GetInt(GuiStateStore.SelectedTableIndexKey, 0);
            selectedTableIndex = selectedTableIndex >= sheet.Sheet.Count ? sheet.Sheet.Count - 1 : selectedTableIndex;
            selectedTableIndex = selectedTableIndex < 0 ? 0 : selectedTableIndex;

            UnityGui.BeginHorizontal();
            DrawSidebar(sheet, ref selectedTableIndex);
            UnityGui.Space(10f);
            DrawSelectedTable(sheet.Sheet[selectedTableIndex]);
            UnityGui.EndHorizontal();

            GuiStateStore.SetInt(GuiStateStore.SelectedTableIndexKey, selectedTableIndex);
        }

        public void DrawSidebar(SheetBinding sheet, ref int selectedTableIndex)
        {
            if (sheet == null)
            {
                BLog.Error("Sheet is null. Cannot draw sidebar.");
                return;
            }

            UnityGui.BeginVertical(UnityGui.BoxStyle, UnityGui.Width(TableButtonWidth));
            _sidebarScrollPosition = UnityGui.BeginScrollView(_sidebarScrollPosition);

            for (int i = 0; i < sheet.Sheet.Count; i++)
            {
                var isSelected = selectedTableIndex == i;
                var table = sheet.Sheet[i];
                if (UnityGui.Button(
                    table.Name,
                    isSelected ? StyleProvider.SidebarSelectedEntryStyle : StyleProvider.SidebarEntryStyle,
                    UnityGui.ExpandWidth(true)))
                {
                    if (!isSelected)
                    {
                        _contentScrollPosition = Vector2.zero;
                        selectedTableIndex = i;
                    }
                }
            }

            UnityGui.EndScrollView();
            UnityGui.EndVertical();
        }

        public void DrawSelectedTable(TableBinding table)
        {
            if (table == null)
            {
                BLog.Error("Selected table is null. Cannot draw table editor.");
                return;
            }

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            _contentScrollPosition = UnityGui.BeginScrollView(_contentScrollPosition);
            TableEditor.DrawTable(table);
            UnityGui.EndScrollView();
            UnityGui.EndVertical();
        }

        public float GetEntryLabelWidth(SheetBinding sheet)
        {
            if (UnityGui == null || sheet == null)
                return 0f;

            float maxWidth = 0f;
            foreach (var table in sheet.Sheet)
            {
                foreach (var entry in table.Table)
                {
                    var width = UnityGui.LabelStyle.CalcSize(UnityGui.GetContent(entry.Name)).x;
                    if (width > maxWidth)
                        maxWidth = width;
                }
            }

            return maxWidth + 10f;
        }

        public float GetTableButtonWidth(SheetBinding sheet)
        {
            if (UnityGui == null || sheet == null)
                return 0f;

            float maxWidth = 0f;
            foreach (var table in sheet.Sheet)
            {
                var width = UnityGui.ButtonStyle.CalcSize(UnityGui.GetContent(table.Name)).x;
                if (width > maxWidth)
                    maxWidth = width;
            }

            return maxWidth + 60f;
        }

        public void Update(float deltaTime)
        {
            TableEditor.Update(deltaTime);
        }

        public void UpdateLayout()
        {
            EntryLabelWidth = -1f;
            TableButtonWidth = -1f;
        }
    }
}
