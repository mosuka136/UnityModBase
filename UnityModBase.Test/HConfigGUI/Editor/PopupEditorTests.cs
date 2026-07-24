using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using Moq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    public class PopupEditorTests
    {
        [Fact]
        public void PopupEditor_WhenConstructed_InitializesDependenciesAndCentersPopupRect()
        {
            // Arrange
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.SetupGet(x => x.ScreenWidth).Returns(800f);
            unityGuiMock.SetupGet(x => x.ScreenHeight).Returns(600f);
            var styleResource = new StyleResource(unityGuiMock.Object);

            // Act
            var editor = new PopupEditor(unityGuiMock.Object, styleResource);

            // Assert
            Assert.Same(unityGuiMock.Object, editor.UnityGui);
            Assert.Same(styleResource, editor.StyleProvider);
            Assert.Equal(new Rect(300f, 255f, 200f, 90f), editor.PopupRect);
            unityGuiMock.VerifyGet(x => x.ScreenWidth, Times.Exactly(2));
            unityGuiMock.VerifyGet(x => x.ScreenHeight, Times.Exactly(2));
        }

        [Fact]
        public void DrawPopup_WhenCalled_SetsPopupStateDrawsBackdropAndUpdatesPopupRect()
        {
            // Arrange
            var originalColor = new Color(0.2f, 0.3f, 0.4f, 0.5f);
            var overlayColor = new Color(0f, 0f, 0f, 0.55f);
            var expectedPopupRect = new Rect(300f, 255f, 200f, 90f);
            var updatedPopupRect = new Rect(10f, 20f, 30f, 40f);
            var title = new Translator("标题", "Title");
            Action drawContentAction = () => { };
            Action closePopupAction = () => { };
            GUIStyle boxStyle = null;
            var unityGuiMock = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGuiMock.SetupGet(x => x.ScreenWidth).Returns(800f);
            unityGuiMock.SetupGet(x => x.ScreenHeight).Returns(600f);
            unityGuiMock.SetupProperty(x => x.Color, originalColor);
            unityGuiMock.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGuiMock
                .Setup(x => x.Box(new Rect(0f, 0f, 800f, 600f), string.Empty));
            unityGuiMock
                .Setup(x => x.ModalWindow(It.IsAny<int>(), expectedPopupRect, It.IsAny<GUI.WindowFunction>(), string.Empty, boxStyle))
                .Returns(updatedPopupRect);
            var styleResource = new StyleResource(unityGuiMock.Object);
            var guiStateStore = new GuiStateStore();
            guiStateStore.Popup.Title = title;
            guiStateStore.Popup.DrawAction = drawContentAction;
            guiStateStore.Popup.CloseAction = closePopupAction;
            var editor = new PopupEditor(unityGuiMock.Object, styleResource);

            // Act
            editor.DrawPopup(guiStateStore);

            // Assert
            Assert.Equal(originalColor, unityGuiMock.Object.Color);
            Assert.Equal(updatedPopupRect, editor.PopupRect);
            unityGuiMock.VerifySet(x => x.Color = overlayColor, Times.Once);
            unityGuiMock.VerifySet(x => x.Color = originalColor, Times.Once);
            unityGuiMock.Verify(x => x.Box(new Rect(0f, 0f, 800f, 600f), string.Empty), Times.Once);
            unityGuiMock.Verify(x => x.ModalWindow(It.IsAny<int>(), expectedPopupRect, It.IsAny<GUI.WindowFunction>(), string.Empty, boxStyle), Times.Once);
        }

        [Fact]
        public void DrawPopupWindow_WhenContentThrows_StillEndsVerticalAndPreservesException()
        {
            var title = new Translator("标题", "Title");
            var titleStyle = (GUIStyle)RuntimeHelpers.GetUninitializedObject(typeof(GUIStyle));
            GC.SuppressFinalize(titleStyle);
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.ScreenWidth).Returns(800f);
            unityGui.SetupGet(x => x.ScreenHeight).Returns(600f);
            unityGui.Setup(x => x.BeginVertical());
            unityGui.Setup(x => x.ExpandWidth(true)).Returns((GUILayoutOption)null);
            unityGui.Setup(x => x.Label(title, titleStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.EndVertical());
            var styleResource = new StyleResource(unityGui.Object);
            var popupTitleStyleField = typeof(StyleResource).GetField(
                "_popupTitleStyle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(popupTitleStyleField);
            popupTitleStyleField.SetValue(styleResource, titleStyle);
            var context = new GuiContext();
            context.Popup.Title = title;
            context.Popup.DrawAction = () => throw new InvalidOperationException("content failed");
            var editor = new PopupEditor(unityGui.Object, styleResource);
            var drawPopupWindowMethod = typeof(PopupEditor).GetMethod(
                "DrawPopupWindow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(drawPopupWindowMethod);
            var drawPopupWindow = (Action<int, GuiContext>)drawPopupWindowMethod.CreateDelegate(
                typeof(Action<int, GuiContext>),
                editor);

            var exception = Assert.Throws<InvalidOperationException>(() => drawPopupWindow(1, context));

            Assert.Equal("content failed", exception.Message);
            unityGui.Verify(x => x.EndVertical(), Times.Once);
        }
    }
}
