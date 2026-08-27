using UnityModBase.HTranslatorSpace;
using CommonTranslatorResource = UnityModBase.HGuiSpace.Resource.TranslatorResource;
using TranslatorResource = UnityModBase.HConfigGUI.Resource.TranslatorResource;

namespace UnityModBase.Test.HConfigGUI.Resource
{
    public class TranslatorResourceTests
    {
        public static IEnumerable<object[]> ResourceTexts => new[]
        {
            new object[] { TranslatorResource.Title, "控制面板", "Control Panel" },
            new object[] { CommonTranslatorResource.On, "开启", "On" },
            new object[] { CommonTranslatorResource.Off, "关闭", "Off" },
            new object[] { TranslatorResource.Reset, "重置", "Reset" },
            new object[] { CommonTranslatorResource.Changed, "已修改：", "Changed: " },
            new object[] { CommonTranslatorResource.ResetDone, "已重置：", "Reset: " },
            new object[] { TranslatorResource.RecordHotkeyPopupTitle, "录制热键中", "Recording Hotkey" },
            new object[] { TranslatorResource.Record, "录制", "Record" },
            new object[] { TranslatorResource.Apply, "应用", "Apply" },
            new object[] { TranslatorResource.Cancel, "取消", "Cancel" },
            new object[] { TranslatorResource.Add, "增加", "Add" },
            new object[] { TranslatorResource.Remove, "移除", "Remove" },
            new object[] { TranslatorResource.Close, "关闭", "Close" },
            new object[] { CommonTranslatorResource.InvalidSliderMetadata, "无效的滑动条元数据", "Invalid slider metadata" },
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
