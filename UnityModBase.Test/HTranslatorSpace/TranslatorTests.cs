using System.Collections;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HTranslatorSpace
{
    public class TranslatorTests
    {
        [Fact]
        public void Constructor_DefaultAndExplicitValues_ArePreserved()
        {
            var empty = new Translator();
            var translated = new Translator("中文", "English");

            Assert.Equal(string.Empty, empty.Chinese);
            Assert.Equal(string.Empty, empty.English);
            Assert.Equal("中文", translated.Chinese);
            Assert.Equal("English", translated.English);
        }

        [Theory]
        [InlineData(LanguageType.Chinese, "中文")]
        [InlineData(LanguageType.English, "English")]
        [InlineData(LanguageType.None, "English")]
        public void SelectedLanguage_DeterminesDefaultText(LanguageType language, string expected)
        {
            var translator = new Translator("中文", "English")
            {
                LanguageType = language
            };

            Assert.Equal(expected, translator.Default);
            Assert.Equal(expected, translator.ToString());
            Assert.Equal(expected, (string)translator);
        }

        [Theory]
        [InlineData(LanguageType.Chinese, "中文")]
        [InlineData(LanguageType.English, "English")]
        [InlineData(LanguageType.None, "English")]
        [InlineData(LanguageType.Default, "English")]
        public void DefaultSelection_FollowsGlobalLanguage(LanguageType globalLanguage, string expected)
        {
            var original = Translator.DefaultLanguage;
            try
            {
                Translator.DefaultLanguage = globalLanguage;
                var translator = new Translator("中文", "English");

                Assert.Equal(expected, translator.Default);
            }
            finally
            {
                Translator.DefaultLanguage = original;
            }
        }

        [Fact]
        public void GenericEnumerator_ReturnsChineseThenEnglish()
        {
            var translator = new Translator("中文", "English");

            Assert.Equal(new[] { "中文", "English" }, translator);
        }

        [Fact]
        public void NonGenericEnumerator_ReturnsChineseThenEnglish()
        {
            IEnumerable translator = new Translator("中文", "English");

            Assert.Equal(new object[] { "中文", "English" }, translator.Cast<object>());
        }
    }
}
