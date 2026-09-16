using UnityModBase.HGuiSpace.Editor;

namespace UnityModBase.Test.HGuiSpace.Editor
{
    public class PinyinTextTests
    {
        [Theory]
        [InlineData('音', true)]
        [InlineData('量', true)]
        [InlineData('〇', true)]
        [InlineData('A', false)]
        [InlineData('1', false)]
        [InlineData(' ', false)]
        public void IsChinese_WhenCharacterIsVarious_ReturnsExpected(char character, bool expected)
        {
            Assert.Equal(expected, PinyinText.IsChinese(character));
        }

        [Fact]
        public void IsChinese_WhenCharacterIsAtTableBoundary_ReturnsExpected()
        {
            Assert.True(PinyinText.IsChinese(PinyinData.MIN_VALUE));
            Assert.False(PinyinText.IsChinese((char)(PinyinData.MIN_VALUE - 1)));
            Assert.True(PinyinText.IsChinese(PinyinData.MAX_VALUE));
            Assert.False(PinyinText.IsChinese((char)(PinyinData.MAX_VALUE + 1)));
        }

        [Fact]
        public void GetPinyin_WhenCharacterIsHan_ReturnsLowercaseSyllable()
        {
            Assert.Equal("yin", PinyinText.GetPinyin('音'));
            Assert.Equal("liang", PinyinText.GetPinyin('量'));
            Assert.Equal("ling", PinyinText.GetPinyin('〇'));
        }

        [Fact]
        public void GetPinyin_WhenCharacterIsNotHan_ReturnsNull()
        {
            Assert.Null(PinyinText.GetPinyin('A'));
            Assert.Null(PinyinText.GetPinyin('1'));
        }

        [Fact]
        public void GetPinyin_WhenCharacterIsAtTableBoundary_ReturnsSyllableOrNull()
        {
            Assert.Equal("yi", PinyinText.GetPinyin(PinyinData.MIN_VALUE));
            Assert.Null(PinyinText.GetPinyin((char)(PinyinData.MIN_VALUE - 1)));
            // 末位汉字“龥”Unihan 读音为 yù，但内置 TinyPinyin 音节表（与上游一致）映射为 yue，搜索以该表为准。
            Assert.Equal("yue", PinyinText.GetPinyin(PinyinData.MAX_VALUE));
            Assert.Null(PinyinText.GetPinyin((char)(PinyinData.MAX_VALUE + 1)));
        }

        [Fact]
        public void GetFullAndInitials_WhenTextIsChinese_ConcatenatesSyllables()
        {
            Assert.Equal("yinliang", PinyinText.GetFull("音量"));
            Assert.Equal("yl", PinyinText.GetInitials("音量"));
        }

        [Fact]
        public void GetFullAndInitials_WhenTextMixesHanAndLatin_IgnoresNonHan()
        {
            Assert.Equal("yinliang", PinyinText.GetFull("音量Volume"));
            Assert.Equal("yl", PinyinText.GetInitials("音量Volume"));
        }

        [Fact]
        public void GetFullAndInitials_WhenTextHasNoHan_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, PinyinText.GetFull("Volume"));
            Assert.Equal(string.Empty, PinyinText.GetInitials("Volume"));
            Assert.Equal(string.Empty, PinyinText.GetFull(null));
            Assert.Equal(string.Empty, PinyinText.GetInitials(null));
        }

        [Theory]
        [InlineData("音量", "yinliang")]
        [InlineData("音量", "YIN")]
        [InlineData("音量", "YINLIANG")]
        [InlineData("音量", "liang")]
        [InlineData("音量", "yl")]
        [InlineData("音量", "YL")]
        [InlineData("音量", "y")]
        [InlineData("音量", "yin liang")]
        [InlineData("主音量", "zyl")]
        [InlineData("开启", "kaiqi")]
        [InlineData("开启", "kq")]
        [InlineData("〇", "ling")]
        [InlineData("〇", "l")]
        public void Contains_WhenFullPinyinOrInitialsIncludeQuery_ReturnsTrue(string text, string query)
        {
            Assert.True(PinyinText.Contains(text, query));
        }

        [Theory]
        [InlineData("音量", "volume")]
        [InlineData("名称", "yinliang")]
        [InlineData("Volume", "yin")]
        [InlineData("", "yin")]
        [InlineData("音量", "")]
        [InlineData(null, "yin")]
        [InlineData("音量", "音")]
        [InlineData("音量", "音量")]
        public void Contains_WhenQueryDoesNotMatchPinyin_ReturnsFalse(string text, string query)
        {
            Assert.False(PinyinText.Contains(text, query));
        }

        [Fact]
        public void Contains_WhenQueryIsWhitespaceOnly_ReturnsFalse()
        {
            Assert.False(PinyinText.Contains("音量", " "));
            Assert.False(PinyinText.Contains("音量", "   "));
        }
    }
}
