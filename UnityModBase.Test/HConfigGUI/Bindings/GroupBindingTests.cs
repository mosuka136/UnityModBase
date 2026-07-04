using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HTranslatorSpace;
using Moq;
using System.Collections.Generic;

namespace UnityModBase.Test.HConfigGUI.Bindings
{
    public class GroupBindingTests
    {
        [Fact]
        public void Constructor_WithValues_AssignsProvidedReferencesAndChildren()
        {
            // Arrange
            var childMock = new Mock<INodeBinding>();
            IEnumerable<INodeBinding> children = new List<INodeBinding> { childMock.Object };
            var name = new Translator("名称", "Name");
            var description = new Translator("描述", "Description");

            // Act
            var result = new GroupBinding("Group", name, description, children);

            // Assert
            Assert.Equal("Group", result.Key);
            Assert.Same(name, result.Name);
            Assert.Same(description, result.Description);
            Assert.Collection(result.Children, child => Assert.Same(childMock.Object, child));
        }

        [Fact]
        public void Constructor_WithNullValues_UsesSafeDefaultsAndNoChildren()
        {
            // Act
            var result = new GroupBinding(null, null, null, null);

            // Assert
            Assert.Equal(string.Empty, result.Key);
            Assert.NotNull(result.Name);
            Assert.NotNull(result.Description);
            Assert.Empty(result.Children);
        }

        [Fact]
        public void Add_WithNullDuplicateOrSelfReference_IgnoresInvalidChildren()
        {
            // Arrange
            var group = new GroupBinding("Group", null, null);
            var childMock = new Mock<INodeBinding>();

            // Act
            group.Add(null);
            group.Add(childMock.Object);
            group.Add(childMock.Object);
            group.Add(group);

            // Assert
            Assert.Collection(group.Children, child => Assert.Same(childMock.Object, child));
        }

        [Fact]
        public void Add_WhenChildAlreadyContainsParent_DoesNotCreateCycle()
        {
            // Arrange
            var parent = new GroupBinding("Parent", null, null);
            var child = new GroupBinding("Child", null, null);
            parent.Add(child);

            // Act
            child.Add(parent);

            // Assert
            Assert.Collection(parent.Children, node => Assert.Same(child, node));
            Assert.Empty(child.Children);
        }

    }
}
