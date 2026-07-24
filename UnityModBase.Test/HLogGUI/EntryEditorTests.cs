using UnityModBase.HLogGUI;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using Moq;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace UnityModBase.Test.HLogGUI
{
    public class EntryEditorTests
    {
        [Fact]
        public void EntryEditor_WhenConstructed_StoresDependencies()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);

            // Act
            var editor = new EntryEditor(unityGuiMock.Object, unityServiceMock.Object);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
            Assert.Same(unityServiceMock.Object, editor.UnityService);
        }

        [Fact]
        public void Constructor_WhenUnityGuiIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new EntryEditor(null, unityServiceMock.Object));

            // Assert
            Assert.Equal("unityGui", exception.ParamName);
            unityServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenButtonIsNotClicked_UsesFirstNonEmptyLineAsButtonTextAndOriginalTextAsTooltip()
        {
            // Arrange
            const string text = "\r\nfirst line\nsecond line";
            var content = new GUIContent("first line", text);
            var style = CreateUninitializedGuiStyle();
            GUILayoutOption widthOption = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.BeginHorizontal());
            unityGuiMock.Setup(x => x.GetContent("first line", text)).Returns(content);
            unityGuiMock.Setup(x => x.Width(120f)).Returns(widthOption);
            unityGuiMock
                .Setup(x => x.Button(content, style, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == widthOption)))
                .Returns(false);
            unityGuiMock.Setup(x => x.EndHorizontal());
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(unityGuiMock.Object, unityServiceMock.Object);
            string copiedMessage = null;
            editor.OnEntryCopied += message => copiedMessage = message;

            // Act
            editor.Draw(text, style, 120f);

            // Assert
            unityGuiMock.Verify(x => x.BeginHorizontal(), Times.Once);
            unityGuiMock.Verify(x => x.GetContent("first line", text), Times.Once);
            unityGuiMock.Verify(x => x.Width(120f), Times.Once);
            unityGuiMock.Verify(x => x.Button(content, style, It.IsAny<GUILayoutOption[]>()), Times.Once);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
            unityGuiMock.VerifyNoOtherCalls();
            unityServiceMock.VerifyNoOtherCalls();
            Assert.Null(copiedMessage);
        }

        [Fact]
        public void Draw_WhenButtonIsClicked_CopiesOriginalTextAndRaisesCopiedEventForDisplayedLine()
        {
            // Arrange
            const string text = "first line\nsecond line";
            var originalDefaultLanguage = Translator.DefaultLanguage;
            Translator.DefaultLanguage = LanguageType.English;
            try
            {
                var content = new GUIContent("first line", text);
                var style = CreateUninitializedGuiStyle();
                GUILayoutOption widthOption = null;
                var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
                unityGuiMock.Setup(x => x.BeginHorizontal());
                unityGuiMock.Setup(x => x.GetContent("first line", text)).Returns(content);
                unityGuiMock.Setup(x => x.Width(80f)).Returns(widthOption);
                unityGuiMock
                    .Setup(x => x.Button(content, style, It.Is<GUILayoutOption[]>(options => options.Length == 1 && options[0] == widthOption)))
                    .Returns(true);
                unityGuiMock.Setup(x => x.EndHorizontal());

                var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
                unityServiceMock.Setup(x => x.ClipboardCopy(text));
                var editor = new EntryEditor(unityGuiMock.Object, unityServiceMock.Object);
                string copiedMessage = null;
                editor.OnEntryCopied += message => copiedMessage = message;

                // Act
                editor.Draw(text, style, 80f);

                // Assert
                unityServiceMock.Verify(x => x.ClipboardCopy(text), Times.Once);
                Assert.Equal("Copied:first line", copiedMessage);
                unityGuiMock.Verify(x => x.BeginHorizontal(), Times.Once);
                unityGuiMock.Verify(x => x.GetContent("first line", text), Times.Once);
                unityGuiMock.Verify(x => x.Width(80f), Times.Once);
                unityGuiMock.Verify(x => x.Button(content, style, It.IsAny<GUILayoutOption[]>()), Times.Once);
                unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
                unityGuiMock.VerifyNoOtherCalls();
                unityServiceMock.VerifyNoOtherCalls();
            }
            finally
            {
                Translator.DefaultLanguage = originalDefaultLanguage;
            }
        }

        [Fact]
        public void Draw_WhenContentCreationThrows_StillEndsHorizontalLayout()
        {
            const string text = "value";
            var style = CreateUninitializedGuiStyle();
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.Setup(x => x.BeginHorizontal());
            unityGuiMock.Setup(x => x.GetContent(text)).Throws(new InvalidOperationException("content failed"));
            unityGuiMock.Setup(x => x.EndHorizontal());
            var unityServiceMock = new Mock<IUnityProvider>(MockBehavior.Strict);
            var editor = new EntryEditor(unityGuiMock.Object, unityServiceMock.Object);

            var exception = Assert.Throws<InvalidOperationException>(() => editor.Draw(text, style, 80f));

            Assert.Equal("content failed", exception.Message);
            unityGuiMock.Verify(x => x.BeginHorizontal(), Times.Once);
            unityGuiMock.Verify(x => x.GetContent(text), Times.Once);
            unityGuiMock.Verify(x => x.EndHorizontal(), Times.Once);
            unityGuiMock.VerifyNoOtherCalls();
            unityServiceMock.VerifyNoOtherCalls();
        }

        private static GUIStyle CreateUninitializedGuiStyle()
        {
            var style = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(style);
            return style;
        }
    }
}
