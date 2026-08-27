using System;
using System.Collections.Generic;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HEnumHelper;
using UnityModBase.HProvider;

namespace UnityModBase.HGuiSpace.Editor.ValueEditor
{
    /// <summary>
    /// 以可展开单选列表编辑枚举配置，并尊重 <see cref="DisplayEnumAttribute"/> 的显示约束。
    /// 可见枚举值、索引映射和说明文本按条目绑定缓存，适用于这些元数据在绑定生命周期内保持不变的场景。
    /// </summary>
    /// <remarks>
    /// 显示约束应至少保留一个枚举值；选择提交依赖可见索引能映射回 <see cref="Enum.GetValues(Type)"/>。
    /// 选择网格绘制失败时会先闭合嵌套布局，再将异常传播给调用方。
    /// </remarks>
    public class EnumEditor : IValueEditor
    {
        // 映射列表保存“可见选项索引 -> Enum.GetValues 原始索引”，避免隐藏项破坏 SelectionGrid 的索引对应关系。
        private readonly Dictionary<IEntryBinding, (Array values, List<int> mapIndex, string[] names)> _cacheEnumInfo =
            new Dictionary<IEntryBinding, (Array values, List<int> mapIndex, string[] names)>();

        /// <summary>
        /// 获取枚举按钮和选择列表使用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建枚举编辑器及其绑定级元数据缓存。
        /// </summary>
        /// <param name="unityGui">用于绘制枚举控件的 IMGUI 提供器。</param>
        public EnumEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui ?? throw new ArgumentNullException(nameof(unityGui));
        }

        /// <inheritdoc/>
        public bool CanEdit(IEntryBinding entry)
        {
            if (entry != null && entry.ValueType.IsEnum && entry.Metadata == null)
                return true;
            else
                return false;
        }

        /// <inheritdoc/>
        public void DrawValue(IEntryBinding entry, EditableGuiContext context)
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

        /// <inheritdoc/>
        public void DrawExtra(IEntryBinding entry, EditableGuiContext context)
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

            int newIndex;
            UnityGui.BeginHorizontal();
            try
            {
                UnityGui.Space(context.GetEntryLabelWidth(context.SelectedGroupKey));

                UnityGui.BeginVertical(UnityGui.BoxStyle);
                try
                {
                    newIndex = UnityGui.SelectionGrid(currentIndex, names, 1, UnityGui.ExpandWidth(true));
                }
                finally
                {
                    UnityGui.EndVertical();
                }

                UnityGui.Space(context.TrailingActionWidth);
            }
            finally
            {
                UnityGui.EndHorizontal();
            }

            if (currentIndex != newIndex)
            {
                context.ExpandedEnumKey = string.Empty;
                context.ChangeSink.SetValue(entry, values.GetValue(mapIndexList[newIndex]));
            }
        }

        /// <summary>
        /// 获取可显示枚举值、其原始索引映射和显示名称；首次读取后按绑定实例缓存。
        /// 非枚举配置项返回三个 null。
        /// </summary>
        /// <param name="entry">要读取枚举元数据的条目绑定。</param>
        /// <returns>全部枚举值、可见项原始索引和可见项显示名称。</returns>
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

        /// <summary>
        /// 释放编辑器；缓存随编辑器实例一起等待回收。
        /// </summary>
        public void Dispose()
        {
        }
    }
}
