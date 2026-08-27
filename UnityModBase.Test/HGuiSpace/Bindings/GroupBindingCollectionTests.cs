using Moq;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.Test.HGuiSpace.Bindings
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

        [Fact]
        public void Children_WhenCastToCollection_RejectsDirectMutation()
        {
            var group = new GroupBinding("Group", null, null);
            var childMock = new Mock<INodeBinding>();
            var children = Assert.IsAssignableFrom<ICollection<INodeBinding>>(group.Children);

            Assert.True(children.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => children.Add(childMock.Object));
            Assert.Empty(group.Children);
        }
    }
}
