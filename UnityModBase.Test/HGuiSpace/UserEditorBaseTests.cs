using Moq;
using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.Test.HGuiSpace
{
    public class UserEditorBaseTests
    {
        [Fact]
        public void Draw_WhenUsersIsNull_ThrowsArgumentNullException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new TestUserEditor(unityGui.Object);
            var selectedKey = "selected";

            var exception = Assert.Throws<ArgumentNullException>(() =>
                editor.Draw(null, ref selectedKey, null));

            Assert.Equal("users", exception.ParamName);
            Assert.Equal("selected", selectedKey);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenUsersContainNoSelectableItem_ThrowsArgumentException()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            var editor = new TestUserEditor(unityGui.Object);
            var selectedKey = "selected";

            var exception = Assert.Throws<ArgumentException>(() =>
                editor.Draw(new UserContext[] { null }, ref selectedKey, null));

            Assert.Equal("users", exception.ParamName);
            Assert.Equal("selected", selectedKey);
            unityGui.VerifyNoOtherCalls();
        }

        [Fact]
        public void Draw_WhenSelectionChanges_UsesNonNullUsersAndReturnsNewKey()
        {
            using var firstUser = new UserContext("first", "First");
            using var secondUser = new UserContext("second", "Second");
            GUIStyle boxStyle = null;
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.Is<GUILayoutOption[]>(options => options.Length == 0)));
            unityGui.Setup(x => x.Space(4f));
            unityGui
                .Setup(x => x.Button(firstUser.Name, It.Is<GUILayoutOption[]>(options => options.Length == 0)))
                .Returns(false);
            unityGui
                .Setup(x => x.SelectionGrid(
                    0,
                    It.Is<string[]>(names => names.SequenceEqual(new[] { firstUser.Name, secondUser.Name })),
                    1,
                    It.Is<GUILayoutOption[]>(options => options.Length == 0)))
                .Returns(1);
            unityGui.Setup(x => x.EndVertical());
            var editor = new TestUserEditor(unityGui.Object)
            {
                IsExpanded = true
            };
            var selectedKey = firstUser.UserId;

            editor.Draw(new[] { firstUser, null, secondUser }, ref selectedKey, null);

            Assert.Equal(secondUser.UserId, selectedKey);
            Assert.False(editor.IsExpanded);
            unityGui.Verify(x => x.Space(4f), Times.Exactly(2));
            unityGui.Verify(x => x.EndVertical(), Times.Once);
        }

        [Fact]
        public void Draw_WhenButtonThrows_StillEndsVerticalAndPreservesException()
        {
            using var user = new UserContext("user", "User");
            GUIStyle boxStyle = null;
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.BoxStyle).Returns(boxStyle);
            unityGui.Setup(x => x.BeginVertical(boxStyle, It.IsAny<GUILayoutOption[]>()));
            unityGui.Setup(x => x.Space(4f));
            unityGui
                .Setup(x => x.Button(user.Name, It.IsAny<GUILayoutOption[]>()))
                .Throws(new InvalidOperationException("button failed"));
            unityGui.Setup(x => x.EndVertical());
            var editor = new TestUserEditor(unityGui.Object);
            var selectedKey = user.UserId;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                editor.Draw(new[] { user }, ref selectedKey, null));

            Assert.Equal("button failed", exception.Message);
            Assert.Equal(user.UserId, selectedKey);
            unityGui.Verify(x => x.EndVertical(), Times.Once);
        }

        private sealed class TestUserEditor : UserEditorBase
        {
            public TestUserEditor(IUnityGuiProvider unityGui)
                : base(null, unityGui)
            {
            }

            public override void SetStatusDirty(IUserContext context)
            {
            }
        }
    }
}
