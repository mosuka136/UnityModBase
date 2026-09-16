using UnityModBase.HGuiSpace.Resource;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Resource
{
    /// <summary>
    /// 校验 GUI 静态文案资源的双语字面值。部分编辑器测试按双语文面匹配分页等按钮、摘要断言依赖具体文案格式，
    /// 此处集中固化预期值，文案修改时需同步显式更新，防止无意变更破坏既有断言。
    /// </summary>
    public class TranslatorResourceTests
    {
        public static IEnumerable<object[]> ResourceTexts => new[]
        {
            new object[] { TranslatorResource.CollectionCount, "{0} 项", "{0} items" },
            new object[] { TranslatorResource.CollectionSummary, "{0} 项：{1}", "{0} items: {1}" },
            new object[] { TranslatorResource.CollectionAdd, "增加", "Add" },
            new object[] { TranslatorResource.CollectionRemove, "移除", "Remove" },
            new object[] { TranslatorResource.CollectionPreviousPage, "上一页", "Previous" },
            new object[] { TranslatorResource.CollectionNextPage, "下一页", "Next" },
            new object[] { TranslatorResource.CollectionPageIndicator, "第 {0}/{1} 页", "Page {0}/{1}" },
            new object[] { TranslatorResource.Search, "搜索", "Search" },
            new object[] { TranslatorResource.SearchClear, "清除", "Clear" },
            new object[] { TranslatorResource.SearchNoResults, "没有匹配的条目", "No matching entries" },
        };

        [Theory]
        [MemberData(nameof(ResourceTexts))]
        public void StaticTranslatorResource_HasExpectedChineseAndEnglishText(
            Translator translator,
            string expectedChinese,
            string expectedEnglish)
        {
            // Assert
            Assert.Equal(expectedChinese, translator.Chinese);
            Assert.Equal(expectedEnglish, translator.English);
        }
    }
}
