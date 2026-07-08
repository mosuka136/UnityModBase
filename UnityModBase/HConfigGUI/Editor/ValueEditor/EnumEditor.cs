using System;
using System.Collections.Generic;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HEnumHelper;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class EnumEditor : IValueEditor
    {
        private readonly Dictionary<IEntryBinding, (Array values, List<int> mapIndex, string[] names)> _cacheEnumInfo =
            new Dictionary<IEntryBinding, (Array values, List<int> mapIndex, string[] names)>();

        public IUnityGuiProvider UnityGui { get; }

        public EnumEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        public bool CanEdit(IEntryBinding entry)
        {
            return entry.ValueType.IsEnum;
        }

        public void DrawValue(IEntryBinding entry, GuiContext context)
        {
            if (!entry.ValueType.IsEnum)
                return;

            var value = ValueProvider.GetValidValue<Enum>(entry);
            if (UnityGui.Button(EnumHelper.GetDescription(entry.ValueType, value), UnityGui.ExpandWidth(true)))
            {
                if (entry.Key == context.ExpandedEnumKey)
                    context.ExpandedEnumKey = string.Empty;
                else
                    context.ExpandedEnumKey = entry.Key;
            }
        }

        public void DrawExtra(IEntryBinding entry, GuiContext context)
        {
            if (!entry.ValueType.IsEnum)
                return;

            if (entry.Key != context.ExpandedEnumKey)
                return;

            var value = ValueProvider.GetValidValue<Enum>(entry);

            (Array values, List<int> mapIndexList, string[] names) = GetEnumInfo(entry);

            int currentIndex = mapIndexList.IndexOf(Array.IndexOf(values, value));
            // 当前值不在可显示枚举集合中时回退到第一个选项，避免 SelectionGrid 使用非法索引。
            currentIndex = currentIndex >= 0 ? currentIndex : 0;

            UnityGui.BeginHorizontal();
            UnityGui.Space(context.GetEntryLabelWidth(context.SelectedGroupKey));

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            int newIndex = UnityGui.SelectionGrid(currentIndex, names, 1, UnityGui.ExpandWidth(true));
            UnityGui.EndVertical();

            UnityGui.Space(context.ResetButtonWidth);
            UnityGui.EndHorizontal();

            if (currentIndex != newIndex)
            {
                context.ExpandedEnumKey = string.Empty;
                context.ChangeSink.SetValue(entry, values.GetValue(mapIndexList[newIndex]));
            }
        }

        public (Array values, List<int> mapIndex, string[] names) GetEnumInfo(IEntryBinding entry)
        {
            if (!entry.ValueType.IsEnum)
                return (null, null, null);

            var type = entry.ValueType;

            Array values;
            List<int> mapIndexList;
            string[] names;
            if (!_cacheEnumInfo.ContainsKey(entry))
            {
                values = Enum.GetValues(type);

                mapIndexList = new List<int>();
                var namesList = new List<string>();
                for (int i = 0; i < values.Length; i++)
                {
                    var enumValue = (Enum)values.GetValue(i);
                    if (EnumHelper.IsDisplay(type, enumValue))
                    {
                        namesList.Add(EnumHelper.GetDescription(type, enumValue));
                        mapIndexList.Add(i);
                    }
                }

                names = namesList.ToArray();

                _cacheEnumInfo[entry] = (values, mapIndexList, names);
            }
            else
            {
                var cache = _cacheEnumInfo[entry];
                values = cache.values;
                mapIndexList = cache.mapIndex;
                names = cache.names;
            }

            return (values, mapIndexList, names);
        }
    }
}
