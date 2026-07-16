using System;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace UnityModBase.HEnumHelper
{
    /// <summary>
    /// 枚举元数据读取工具。
    /// 用于读取 Description 和显示控制特性，并缓存反射结果以降低配置界面每帧绘制成本。
    /// </summary>
    public static partial class EnumHelper
    {
        // 枚举字段特性按“枚举类型 + 字段名 + 特性类型”缓存，避免配置 GUI 重复执行字段反射。
        private static readonly ConcurrentDictionary<(Type enumType, string enumName, Type attrType), Attribute> _enumValues = new ConcurrentDictionary<(Type, string, Type), Attribute>();

        /// <summary>
        /// 读取枚举值字段上直接声明的特性。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <typeparam name="TAttribute">要读取的特性类型。</typeparam>
        /// <param name="enumValue">枚举值；未定义值不会对应字段。</param>
        /// <returns>字段上的首个匹配特性；字段不存在或未标记时返回 <c>null</c>。</returns>
        public static TAttribute GetAttribute<TEnum, TAttribute>(TEnum enumValue) where TEnum : Enum where TAttribute : Attribute
        {
            return GetAttribute<TAttribute>(typeof(TEnum), enumValue);
        }

        /// <summary>
        /// 使用运行时枚举类型读取值字段上直接声明的特性。
        /// </summary>
        /// <typeparam name="TAttribute">要读取的特性类型。</typeparam>
        /// <param name="enumType">声明该值的枚举类型。</param>
        /// <param name="enumValue">枚举值；未定义值不会对应字段。</param>
        /// <returns>字段上的首个匹配特性；字段不存在或未标记时返回 <c>null</c>。</returns>
        public static TAttribute GetAttribute<TAttribute>(Type enumType, Enum enumValue) where TAttribute : Attribute
        {
            var key = (enumType, enumValue.ToString(), typeof(TAttribute));
            return _enumValues.GetOrAdd(key, k =>
            {
                var fieldInfo = k.enumType.GetField(k.enumName);
                return fieldInfo?.GetCustomAttributes(typeof(TAttribute), false) is TAttribute[] attribute && attribute.Length > 0 ? attribute[0] : null;
            }) as TAttribute;
        }

        /// <summary>
        /// 获取枚举值的 <see cref="DescriptionAttribute.Description"/>；未标记时回退为枚举值文本。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="value">要读取描述的枚举值。</param>
        /// <returns>声明的描述或枚举值文本。</returns>
        public static string GetDescription<TEnum>(TEnum value) where TEnum : Enum
        {
            return GetAttribute<TEnum, DescriptionAttribute>(value)?.Description ?? value.ToString();
        }

        /// <summary>
        /// 使用运行时枚举类型获取描述；未标记时回退为枚举值文本。
        /// </summary>
        /// <param name="enumType">声明该值的枚举类型。</param>
        /// <param name="value">要读取描述的枚举值。</param>
        /// <returns>声明的描述或枚举值文本。</returns>
        public static string GetDescription(Type enumType, Enum value)
        {
            return GetAttribute<DescriptionAttribute>(enumType, value)?.Description ?? value.ToString();
        }

        /// <summary>
        /// 判断枚举值是否应作为 GUI 选项展示；未标记 <see cref="DisplayEnumAttribute"/> 时默认展示。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="value">要检查的枚举值。</param>
        /// <returns>应展示或没有显式隐藏标记时为 <c>true</c>。</returns>
        public static bool IsDisplay<TEnum>(TEnum value) where TEnum : Enum
        {
            return GetAttribute<TEnum, DisplayEnumAttribute>(value)?.IsDisplay ?? true;
        }

        /// <summary>
        /// 使用运行时枚举类型判断值是否应作为 GUI 选项展示；未标记时默认展示。
        /// </summary>
        /// <param name="enumType">声明该值的枚举类型。</param>
        /// <param name="value">要检查的枚举值。</param>
        /// <returns>应展示或没有显式隐藏标记时为 <c>true</c>。</returns>
        public static bool IsDisplay(Type enumType, Enum value)
        {
            return GetAttribute<DisplayEnumAttribute>(enumType, value)?.IsDisplay ?? true;
        }
    }
}
