using System.Runtime.CompilerServices;
using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HLogGUI;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HLogGUI
{
    public class UserEditorTests
    {
        [Fact]
        public void GuiContextSubscribed_WhenEntryIsCopied_ShowsCopiedMessage()
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
                unityServiceMock.SetupGet(x => x.RealtimeSinceStartup).Returns(10f);
                var editor = new UserEditor(
                    unityServiceMock.Object,
                    unityGuiMock.Object,
                    new StyleResource(unityGuiMock.Object));
                var guiContext = new GuiContext();
                var toastEditor = new ToastEditor(
                    unityServiceMock.Object,
                    unityGuiMock.Object,
                    new StyleResource(unityGuiMock.Object));
                guiContext.RegisterLogHandlers(new LogDatabase(null), editor);
                guiContext.SubscribeToastNotifications(toastEditor);

                // Act
                editor.GroupEditor.EntryEditor.Draw(text, style, 80f);

                // Assert
                Assert.Equal("Copied:first line", toastEditor.Message);
                Assert.Equal(12f, toastEditor.EndTime);
                unityServiceMock.Verify(x => x.ClipboardCopy(text), Times.Once);
                unityServiceMock.VerifyGet(x => x.RealtimeSinceStartup, Times.Once);
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
        public void GuiContextUnsubscribed_WhenEntryIsCopied_DoesNotShowCopiedMessage()
        {
            // Arrange
            const string text = "first line\nsecond line";
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
            var editor = new UserEditor(
                unityServiceMock.Object,
                unityGuiMock.Object,
                new StyleResource(unityGuiMock.Object));
            var guiContext = new GuiContext();
            var toastEditor = new ToastEditor(
                unityServiceMock.Object,
                unityGuiMock.Object,
                new StyleResource(unityGuiMock.Object));
            guiContext.RegisterLogHandlers(new LogDatabase(null), editor);
            guiContext.SubscribeToastNotifications(toastEditor);
            guiContext.UnsubscribeToastNotifications();

            // Act
            editor.GroupEditor.EntryEditor.Draw(text, style, 80f);

            // Assert
            Assert.Null(toastEditor.Message);
            Assert.Equal(0f, toastEditor.EndTime);
            unityServiceMock.Verify(x => x.ClipboardCopy(text), Times.Once);
            unityGuiMock.Verify(x => x.BeginHorizontal(), Times.Once);
            unityGuiMock.Verify(x => x.GetContent("first line", text), Times.Once);
            unityGuiMock.Verify(x => x.Width(80f), Times.Once);
            unityGuiMock.Verify(x => x.Button(content, style, It.IsAny<GUILayoutOption[]>()), Times.Once);
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
