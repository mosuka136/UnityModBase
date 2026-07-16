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
        /// <summary>
        /// 根据配置管理器类型和配置键读取控件元数据。
        /// 当前仅识别 <see cref="ConfigSliderAttribute"/>；配置项为 null 或没有受支持特性时返回 null。
        /// </summary>
        /// <param name="classType">声明配置成员特性的类型。</param>
        /// <param name="entry">待查询配置项。</param>
        /// <returns>解析出的元数据，或 null。</returns>
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

        /// <summary>
        /// 尝试读取控件元数据。反射或特性读取中的任何异常都会被降级为“无元数据”，以避免单个声明阻断整个配置界面。
        /// </summary>
        /// <param name="classType">声明配置成员特性的类型。</param>
        /// <param name="entry">待查询配置项。</param>
        /// <param name="metadata">成功时为解析出的元数据，失败时为 null。</param>
        /// <returns>成功获得非 null 元数据时返回 true。</returns>
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
