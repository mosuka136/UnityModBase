using Moq;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HGuiSpace.Editor;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.Test.HGuiSpace.Editor
{
    public class NodeSearchTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsActive_WhenQueryIsNullOrWhitespace_ReturnsFalse(string query)
        {
            Assert.False(NodeSearch.IsActive(query));
        }

        [Fact]
        public void IsActive_WhenQueryHasNonWhitespace_ReturnsTrue()
        {
            Assert.True(NodeSearch.IsActive(" Volume "));
        }

        [Fact]
        public void Matches_WhenNodeIsNull_ReturnsFalse()
        {
            Assert.False(NodeSearch.Matches(null, "volume"));
        }

        [Fact]
        public void Matches_WhenQueryIsInactive_ReturnsFalse()
        {
            var group = new GroupBinding("Volume", new Translator("音量", "Volume"));

            Assert.False(NodeSearch.Matches(group, "   "));
        }

        [Fact]
        public void ContainsMatch_WhenNodeIsNull_ReturnsFalse()
        {
            Assert.False(NodeSearch.ContainsMatch(null, "volume"));
            Assert.False(NodeSearch.ContainsMatch(null, "   "));
        }

        [Fact]
        public void ContainsMatch_WhenQueryIsInactive_ReturnsTrueForAnyNode()
        {
            var group = new GroupBinding("Unrelated", new Translator("无关", "Unrelated"));

            Assert.True(NodeSearch.ContainsMatch(group, null));
            Assert.True(NodeSearch.ContainsMatch(group, "   "));
        }

        [Fact]
        public void Matches_WhenKeyContainsQueryIgnoreCase_ReturnsTrue()
        {
            var entry = CreateNode("MasterVolume", "音量", "Loudness", "说明", "Hint");

            Assert.True(NodeSearch.Matches(entry, "volume"));
            Assert.True(NodeSearch.Matches(entry, "MASTER"));
        }

        [Fact]
        public void Matches_WhenChineseOrEnglishNameContainsQuery_ReturnsTrue()
        {
            var entry = CreateNode("Key", "主音量", "Master Volume", "说明", "Hint");

            Assert.True(NodeSearch.Matches(entry, "音量"));
            Assert.True(NodeSearch.Matches(entry, "master"));
        }

        [Fact]
        public void Matches_WhenChineseOrEnglishDescriptionContainsQuery_ReturnsTrue()
        {
            var entry = CreateNode("Key", "名称", "Name", "控制主音量", "Controls master volume");

            Assert.True(NodeSearch.Matches(entry, "主音量"));
            Assert.True(NodeSearch.Matches(entry, "VOLUME"));
        }

        [Fact]
        public void Matches_WhenNameAndDescriptionAreNull_StillMatchesKey()
        {
            var node = new Mock<INodeBinding>(MockBehavior.Strict);
            node.SetupGet(x => x.Key).Returns("Volume");
            node.SetupGet(x => x.Name).Returns((Translator)null);
            node.SetupGet(x => x.Description).Returns((Translator)null);

            Assert.True(NodeSearch.Matches(node.Object, "vol"));
            Assert.False(NodeSearch.Matches(node.Object, "missing"));
        }

        [Fact]
        public void Matches_WhenQueryHasSurroundingWhitespace_TrimsBeforeMatching()
        {
            var entry = CreateNode("Volume", "音量", "Volume", "说明", "Hint");

            Assert.True(NodeSearch.Matches(entry, "  volume  "));
            Assert.False(NodeSearch.Matches(entry, "  missing  "));
        }

        [Fact]
        public void Matches_WhenNothingContainsQuery_ReturnsFalse()
        {
            var entry = CreateNode("Key", "名称", "Name", "说明", "Hint");

            Assert.False(NodeSearch.Matches(entry, "volume"));
        }

        [Fact]
        public void ContainsMatch_WhenNestedEntryMatches_ReturnsTrueForAncestors()
        {
            var matching = CreateNode("Volume", "音量", "Volume", "", "");
            var nested = new GroupBinding("Nested", new Translator("嵌套", "Nested"), null, new[] { matching });
            var root = new GroupBinding("Root", new Translator("根", "Root"), null, new[] { nested });

            Assert.True(NodeSearch.ContainsMatch(root, "volume"));
            Assert.True(NodeSearch.ContainsMatch(nested, "volume"));
            Assert.True(NodeSearch.Matches(matching, "volume"));
        }

        [Fact]
        public void ContainsMatch_WhenOnlyUnrelatedSiblingMatches_ReturnsFalse()
        {
            var matching = CreateNode("Volume", "音量", "Volume", "", "");
            var otherGroup = new GroupBinding("Graphics", new Translator("画面", "Graphics"));
            var root = new GroupBinding(
                "Root",
                new Translator("根", "Root"),
                null,
                new INodeBinding[] { matching, otherGroup });

            Assert.False(NodeSearch.ContainsMatch(otherGroup, "volume"));
            Assert.True(NodeSearch.ContainsMatch(root, "volume"));
        }

        [Fact]
        public void ContainsMatch_WhenGroupNameMatches_ReturnsTrueWithoutMatchingChildren()
        {
            var child = CreateNode("Unrelated", "无关", "Unrelated", "", "");
            var group = new GroupBinding("Audio", new Translator("音频", "Audio"), null, new[] { child });

            Assert.True(NodeSearch.ContainsMatch(group, "audio"));
            Assert.False(NodeSearch.Matches(child, "audio"));
        }

        private static IEntryBinding CreateNode(
            string key,
            string chineseName,
            string englishName,
            string chineseDescription,
            string englishDescription)
        {
            var entry = new Mock<IEntryBinding>(MockBehavior.Strict);
            entry.SetupGet(x => x.Key).Returns(key);
            entry.SetupGet(x => x.Name).Returns(new Translator(chineseName, englishName));
            entry.SetupGet(x => x.Description).Returns(new Translator(chineseDescription, englishDescription));
            return entry.Object;
        }
    }
}
