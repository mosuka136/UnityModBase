using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace.Resource
{
    /// <summary>
    /// 可编辑 GUI 控件共享的固定文案。
    /// </summary>
    public static class TranslatorResource
    {
        /// <summary>开关开启状态文案。</summary>
        public static readonly Translator On = new Translator("开启", "On");

        /// <summary>开关关闭状态文案。</summary>
        public static readonly Translator Off = new Translator("关闭", "Off");

        /// <summary>条目修改成功提示前缀。</summary>
        public static readonly Translator Changed = new Translator("已修改：", "Changed: ");

        /// <summary>条目重置成功提示前缀。</summary>
        public static readonly Translator ResetDone = new Translator("已重置：", "Reset: ");

        /// <summary>滑动条元数据无效提示。</summary>
        public static readonly Translator InvalidSliderMetadata = new Translator("无效的滑动条元数据", "Invalid slider metadata");
    }
}
