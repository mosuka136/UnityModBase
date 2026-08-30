using System;

namespace UnityModBase.HEnumHelper
{
    /// <summary>
    /// 控制枚举值是否在配置界面中显示。
    /// 被隐藏的值仍可被配置文件解析，只是不作为 GUI 选项展示。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class DisplayEnumAttribute : Attribute
    {
        /// <summary>
        /// 是否在枚举选择控件中显示该值。
        /// </summary>
        public bool IsDisplay { get; set; }

        /// <summary>
        /// 创建枚举值显示元数据。
        /// </summary>
        /// <param name="isDisplay">是否允许 GUI 把该枚举值作为选项展示，默认展示。</param>
        public DisplayEnumAttribute(bool isDisplay = true)
        {
            IsDisplay = isDisplay;
        }
    }
}
