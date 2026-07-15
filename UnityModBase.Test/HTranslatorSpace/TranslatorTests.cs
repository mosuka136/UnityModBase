using System.Collections;
using System.Reflection;
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

        [Fact]
        public void DefaultLanguage_WhenChanged_InvokesRemainingHandlersOnceAfterFailure()
        {
            var original = Translator.DefaultLanguage;
            var target = original == LanguageType.Chinese ? LanguageType.English : LanguageType.Chinese;
            var throwingHandlerCalled = false;
            var receivedCount = 0;
            LanguageType receivedLanguage = default;
            EventHandler<LanguageType> throwingHandler = (_, _) =>
            {
                throwingHandlerCalled = true;
                throw new InvalidOperationException("handler failure");
            };
            EventHandler<LanguageType> receivingHandler = (_, language) =>
            {
                receivedCount++;
                receivedLanguage = language;
            };
            Translator.OnDefaultLanguageChanged += throwingHandler;
            Translator.OnDefaultLanguageChanged += receivingHandler;

            try
            {
                Translator.DefaultLanguage = target;
                Translator.DefaultLanguage = target;

                Assert.True(throwingHandlerCalled);
                Assert.Equal(1, receivedCount);
                Assert.Equal(target, receivedLanguage);
            }
            finally
            {
                Translator.OnDefaultLanguageChanged -= throwingHandler;
                Translator.OnDefaultLanguageChanged -= receivingHandler;
                Translator.DefaultLanguage = original;
            }
        }

        [Fact]
        public void Dispose_ResetsLanguageAndClearsHandlers()
        {
            var eventField = typeof(Translator).GetField(
                nameof(Translator.OnDefaultLanguageChanged),
                BindingFlags.NonPublic | BindingFlags.Static);
            var languageField = typeof(Translator).GetField(
                "_defaultLanguage",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(eventField);
            Assert.NotNull(languageField);
            var originalHandlers = (EventHandler<LanguageType>)eventField.GetValue(null);
            var originalLanguage = (LanguageType)languageField.GetValue(null);
            var invocationCount = 0;

            try
            {
                eventField.SetValue(null, null);
                languageField.SetValue(null, LanguageType.Chinese);
                Translator.OnDefaultLanguageChanged += (_, _) => invocationCount++;

                Translator.Dispose();
                Assert.Equal(LanguageType.English, Translator.DefaultLanguage);

                Translator.DefaultLanguage = LanguageType.Chinese;

                Assert.Equal(LanguageType.Chinese, Translator.DefaultLanguage);
                Assert.Equal(0, invocationCount);
            }
            finally
            {
                eventField.SetValue(null, originalHandlers);
                languageField.SetValue(null, originalLanguage);
            }
        }
    }
}
