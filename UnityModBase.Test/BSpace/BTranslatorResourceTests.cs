using UnityModBase.BSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.BSpace
{
    public class BTranslatorResourceTests
    {
        [Fact]
        public void UserName_HasExpectedChineseAndEnglishText()
        {
            Translator userName = BTranslatorResource.UserName;

            Assert.Equal("Unity 模组基础库", userName.Chinese);
            Assert.Equal("UnityModBase", userName.English);
        }
    }
}
