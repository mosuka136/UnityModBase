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

        /// <summary>集合条目为空时的摘要文案；参数为元素数。</summary>
        public static readonly Translator CollectionCount = new Translator("{0} 项", "{0} items");

        /// <summary>集合条目摘要文案；参数为元素数和元素预览。</summary>
        public static readonly Translator CollectionSummary = new Translator("{0} 项：{1}", "{0} items: {1}");

        /// <summary>集合元素添加按钮文案。</summary>
        public static readonly Translator CollectionAdd = new Translator("增加", "Add");

        /// <summary>集合元素移除按钮文案。</summary>
        public static readonly Translator CollectionRemove = new Translator("移除", "Remove");

        /// <summary>集合分页上一页按钮文案。</summary>
        public static readonly Translator CollectionPreviousPage = new Translator("上一页", "Previous");

        /// <summary>集合分页下一页按钮文案。</summary>
        public static readonly Translator CollectionNextPage = new Translator("下一页", "Next");

        /// <summary>集合分页页码指示文案；参数为当前页码（从 1 起）和总页数。</summary>
        public static readonly Translator CollectionPageIndicator = new Translator("第 {0}/{1} 页", "Page {0}/{1}");
    }
}
