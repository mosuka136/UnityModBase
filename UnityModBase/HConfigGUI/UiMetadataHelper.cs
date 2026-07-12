using System;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 从配置声明中提取 GUI 元数据。
    /// 通过 <see cref="ConfigSliderAttribute"/> 把数值配置项映射为滑条控件。
    /// </summary>
    public static class UiMetadataHelper
    {
        public static IUiMetadata GetMetadata(Type classType, IConfigEntry entry)
        {
            if (entry == null)
                return null;

            var sliderInfo = ClassHelper.GetSliderInfo(classType, entry.Key);
            if (sliderInfo.HasValue)
            {
                return new UiSliderMetadata(sliderInfo.Value.Min, sliderInfo.Value.Max, sliderInfo.Value.Step);
            }
            return null;
        }

        public static bool TryGetMetadata(Type classType, IConfigEntry entry, out IUiMetadata metadata)
        {
            try
            {
                metadata = GetMetadata(classType, entry);
                return metadata != null;
            }
            catch
            {
                metadata = null;
                return false;
            }
        }
    }
}
