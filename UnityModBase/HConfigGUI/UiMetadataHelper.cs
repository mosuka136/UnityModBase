using System;
using System.Linq;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 从配置声明中提取 GUI 元数据。
    /// 把单个 <see cref="ConfigSliderAttribute"/> 映射为滑条元数据；存在 <see cref="ConfigGuiAttribute"/>
    /// 组合标记时，按各滑条声明指定的槽位索引聚合为 <see cref="UiConfigMetadata"/>。
    /// </summary>
    public static class UiMetadataHelper
    {
        /// <summary>
        /// 根据配置管理器类型和配置键读取控件元数据。
        /// 识别 <see cref="ConfigSliderAttribute"/> 与 <see cref="ConfigGuiAttribute"/>；配置项没有受支持特性时返回 <c>null</c>。
        /// 无组合标记时仅转换属性上的第一个特性，而反射不保证多个特性间的顺序，声明多个受支持特性时结果不确定；
        /// 有组合标记时按各子声明指定的槽位索引聚合，没有子声明提供有效索引的槽位保持 <c>null</c>。
        /// 反射读取中的异常会记录日志并降级为“无元数据”，避免单个声明阻断整个配置界面；参数校验异常不受此降级保护。
        /// </summary>
        /// <param name="classType">声明配置成员特性的类型。</param>
        /// <param name="entry">待查询配置项。</param>
        /// <returns>解析出的元数据；无受支持特性或反射失败时为 <c>null</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="classType"/> 或 <paramref name="entry"/> 为 <c>null</c>。</exception>
        public static IUiMetadata GetMetadata(Type classType, IConfigEntry entry)
        {
            if (classType == null)
                throw new ArgumentNullException(nameof(classType));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            try
            {
                var attributes = ClassHelper.GetEntryDeclarationAttributes(classType, entry);
                if (attributes == null)
                    return null;

                var multipleMetadata = attributes.FirstOrDefault(a => a is ConfigGuiAttribute) as ConfigGuiAttribute;
                if (multipleMetadata == null)
                    return GetMetadataFromAttribute(attributes.FirstOrDefault(a => a is IConfigGuiAttribute), out _);

                var metadataArray = new IUiMetadata[multipleMetadata.Count];
                foreach (var attribute in attributes)
                {
                    var metadata = GetMetadataFromAttribute(attribute, out var index);
                    if (metadata != null && index >= 0)
                        metadataArray[index] = metadata;
                }

                return new UiConfigMetadata(metadataArray);
            }
            catch (Exception ex)
            {
                BLog.Error($"Failed to get UI metadata for {classType.FullName}.{entry.Key}.", ex);
                return null;
            }
        }

        /// <summary>
        /// 把配置属性上的单个声明特性转换为元数据对象。
        /// 当前仅识别 <see cref="ConfigSliderAttribute"/>；其余类型（含 <c>null</c> 与 <see cref="ConfigGuiAttribute"/> 标记本身）返回 <c>null</c>，
        /// 因此组合展开时未识别的声明不占用槽位，对应槽位保持 <c>null</c>。
        /// </summary>
        /// <param name="attribute">配置属性上直接声明的特性，可为 <c>null</c>。</param>
        /// <param name="index">输出该声明指定的组合槽位下标；未通过带索引构造函数声明时为 <c>-1</c>，组合展开路径会跳过此类声明。</param>
        /// <returns>对应的元数据对象；特性类型不受支持时为 <c>null</c>。</returns>
        private static IUiMetadata GetMetadataFromAttribute(Attribute attribute, out int index)
        {
            index = -1;

            switch (attribute)
            {
                case ConfigSliderAttribute slider:
                    index = slider.Index;
                    return new UiSliderMetadata(slider.Min, slider.Max, slider.Step);
                default:
                    return null;
            }
        }
    }
}
