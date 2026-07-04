using Moq;
using UnityModBase.HConfigGUI.Bindings;

namespace UnityModBase.Test.HConfigGUI.Bindings
{
    public class GroupBindingCollectionTests
    {
        [Fact]
        public void Children_WhenChildIsAddedAfterViewIsCaptured_ReflectsAddedChild()
        {
            // Arrange
            var group = new GroupBinding("Group", null, null);
            var children = group.Children;
            var childMock = new Mock<INodeBinding>();

            // Act
            group.Add(childMock.Object);

            // Assert
            Assert.Same(children, group.Children);
            Assert.Collection(children, child => Assert.Same(childMock.Object, child));
        }
    }
}
