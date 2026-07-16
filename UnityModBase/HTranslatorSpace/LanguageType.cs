using System.ComponentModel;
using UnityModBase.HEnumHelper;

namespace UnityModBase.HTranslatorSpace
{
    /// <summary>
    /// 插件 GUI 和配置注释使用的语言枚举。
    /// None 与 Default 是内部控制值，不在配置界面中作为可选语言展示。
    /// </summary>
    public enum LanguageType
    {
        /// <summary>
        /// 未指定语言的控制值；翻译解析时回退为英文，GUI 中隐藏。
        /// </summary>
        [DisplayEnum(false)]
        None,

        /// <summary>
        /// 实例跟随全局默认语言的控制值，GUI 中隐藏。
        /// </summary>
        [DisplayEnum(false)]
        Default,

        /// <summary>
        /// 简体中文。
        /// </summary>
        [Description("简体中文")]
        Chinese,

        /// <summary>
        /// 英文。
        /// </summary>
        [Description("English")]
        English,
    }
}
