using UnityModBase.HConfigSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using Moq;
using UnityModBase.HEntrySpace;

namespace UnityModBase.Test.HotkeyManager
{
    public class HotkeyTests
    {
        public HotkeyTests()
        {
            Hotkey.GlobalValid = true;
        }

        [Fact]
        public void Constructor_Default_InitializesEmptyHotkeysList()
        {
            var hotkey = new Hotkey();

            Assert.NotNull(hotkey.Hotkeys);
            Assert.Empty(hotkey.Hotkeys);
        }

        [Fact]
        public void Constructor_Default_UsesSharedDefaultUnityProvider()
        {
            var firstHotkey = new Hotkey();
            var secondHotkey = new Hotkey();

            Assert.NotNull(firstHotkey.UnityService);
            Assert.Same(firstHotkey.UnityService, secondHotkey.UnityService);
        }

        [Fact]
        public void Constructor_WithHotkeyStringAndUnityProvider_ValidStringParsesHotkeys()
        {
            var unityService = UnityProvider.Instance;

            var hotkey = new Hotkey("Ctrl+A", unityService);

            Assert.Same(unityService, hotkey.UnityService);
            Assert.Single(hotkey.Hotkeys);
            Assert.Equal("Ctrl+A", hotkey.ToString());
        }

        [Fact]
        public void Constructor_WithHotkeyStringAndUnityProvider_InvalidStringThrowsArgumentException()
        {
            var unityService = UnityProvider.Instance;

            var exception = Assert.Throws<ArgumentException>(() => new Hotkey("InvalidKey", unityService));

            Assert.Equal("Invalid hotkey string: InvalidKey", exception.Message);
        }

        // 注：原 Constructor_WithSourceHotkeyAndUnityProvider_* 三个测试覆盖的 Hotkey(Hotkey, UnityProvider)
        // 拷贝构造函数在本次重构中随该公开构造函数一并移除；等价的深拷贝行为由 Clone 覆盖，
        // 因此删除这些构造函数测试，下方用 Clone_* 直接验证保留下来的克隆入口。

        [Fact]
        public void Clone_ProducesIndependentDeepCopyWithSameChordsAndUnityService()
        {
            var source = new Hotkey("Ctrl+A,Shift+B", UnityProvider.Instance);

            var cloned = source.Clone();

            Assert.Equal(source.ToString(), cloned.ToString());
            Assert.Equal(source.Count, cloned.Count);
            Assert.Same(source.UnityService, cloned.UnityService);
            Assert.NotSame(source.Hotkeys, cloned.Hotkeys);
            Assert.NotSame(source.Hotkeys[0], cloned.Hotkeys[0]);
            Assert.NotSame(source.Hotkeys[1], cloned.Hotkeys[1]);
        }

        [Fact]
        public void Clone_ChangingSourceDoesNotAffectClone()
        {
            var source = new Hotkey("Ctrl+A", UnityProvider.Instance);
            var cloned = source.Clone();

            source.Hotkeys[0].Clear();

            Assert.Equal("Ctrl+A", cloned.ToString());
            Assert.Empty(source.ToString());
        }

        [Fact]
        public void Count_WithEmptyHotkeys_ReturnsZero()
        {
            var hotkey = new Hotkey();

            Assert.Equal(0, hotkey.Count);
        }

        // 重构后 TryParse 成为静态方法，且空白分段不再被跳过，而是使整次解析失败。
        // 该用例由 TryParse_WithWhitespaceSegment_ReturnsFalse 覆盖新行为，原“跳过空白”语义已不存在。
        [Fact]
        public void TryParse_WithWhitespaceSegment_ReturnsFalse()
        {
            var result = Hotkey.TryParse("Ctrl+A,   ,Shift+B", UnityProvider.Instance, out var hotkey);

            Assert.False(result);
            Assert.Null(hotkey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("InvalidKey")]
        public void TryParse_WhenInputInvalid_ReturnsFalse(string text)
        {
            var result = Hotkey.TryParse(text, UnityProvider.Instance, out var hotkey);

            Assert.False(result);
            Assert.Null(hotkey);
        }

        [Fact]
        public void TryParse_WithValidInput_ReturnsTrueAndProducesNewHotkey()
        {
            var unityService = UnityProvider.Instance;

            var result = Hotkey.TryParse("Ctrl+A,Shift+B", unityService, out var hotkey);

            Assert.True(result);
            Assert.NotNull(hotkey);
            Assert.Same(unityService, hotkey.UnityService);
            Assert.Equal(2, hotkey.Hotkeys.Count);
            Assert.Equal("Ctrl+A,Shift+B", hotkey.ToString());
        }

        [Fact]
        public void TryParse_WithNullUnityService_ReturnsFalse()
        {
            var result = Hotkey.TryParse("Ctrl+A", null, out var hotkey);

            Assert.False(result);
            Assert.Null(hotkey);
        }

        [Fact]
        public void WasPressedThisFrame_WhenHotkeyInvalid_ReturnsFalse()
        {
            var innerChord = new Mock<IHotkeyChord>(MockBehavior.Strict);
            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(new HotkeyChord(innerChord.Object, UnityProvider.Instance));
            hotkey.Valid = false;

            var result = hotkey.WasPressedThisFrame();

            Assert.False(result);
            innerChord.Verify(x => x.WasPressedThisFrame(), Times.Never);
        }

        [Fact]
        public void WasPressedThisFrame_WhenSecondChordPressed_ReturnsTrue()
        {
            var unityService = UnityProvider.Instance;
            var firstChord = new Mock<IHotkeyChord>(MockBehavior.Strict);
            var secondChord = new Mock<IHotkeyChord>(MockBehavior.Strict);
            firstChord.Setup(x => x.WasPressedThisFrame()).Returns(false);
            secondChord.Setup(x => x.WasPressedThisFrame()).Returns(true);

            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(new HotkeyChord(firstChord.Object, unityService));
            hotkey.Hotkeys.Add(new HotkeyChord(secondChord.Object, unityService));

            var result = hotkey.WasPressedThisFrame();

            Assert.True(result);
            firstChord.Verify(x => x.WasPressedThisFrame(), Times.Once);
            secondChord.Verify(x => x.WasPressedThisFrame(), Times.Once);
        }

        [Fact]
        public void Add_NullChord_DoesNotAdd()
        {
            var hotkey = new Hotkey();

            hotkey.Add(null);

            Assert.Empty(hotkey.Hotkeys);
        }

        [Fact]
        public void Add_ExistingChord_DoesNotAddDuplicateReference()
        {
            var unityService = UnityProvider.Instance;
            var chord = new HotkeyChord(new Mock<IHotkeyChord>().Object, unityService);
            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(chord);

            hotkey.Add(chord);

            Assert.Single(hotkey.Hotkeys);
        }

        [Fact]
        public void Remove_NullChord_DoesNothing()
        {
            var unityService = UnityProvider.Instance;
            var chord = new HotkeyChord(new Mock<IHotkeyChord>().Object, unityService);
            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(chord);

            hotkey.Remove(null);

            Assert.Single(hotkey.Hotkeys);
            Assert.Same(chord, hotkey.Hotkeys[0]);
        }

        [Fact]
        public void RemoveInvalidHotkey_RemovesNullAndInvalidEntries()
        {
            var unityService = UnityProvider.Instance;
            var validResult = HotkeyChord.TryParse("Ctrl+A", unityService);
            Assert.True(validResult.Success);

            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(null);
            hotkey.Hotkeys.Add(new HotkeyChord(unityService));
            hotkey.Hotkeys.Add(validResult.Value);

            hotkey.RemoveInvalidHotkey();

            Assert.Single(hotkey.Hotkeys);
            Assert.Equal("Ctrl+A", hotkey.Hotkeys[0].ToString());
        }

        [Fact]
        public void HasSameHotkey_WhenOrderDiffers_ReturnsTrue()
        {
            var first = new Hotkey("Ctrl+A,Shift+B", UnityProvider.Instance);
            var second = new Hotkey("Shift+B,Ctrl+A", UnityProvider.Instance);

            var result = first.HasSameHotkey(second);

            Assert.True(result);
        }

        [Fact]
        public void HasSameHotkey_WhenOtherIsNull_ReturnsFalse()
        {
            var hotkey = new Hotkey();

            Assert.False(hotkey.HasSameHotkey(null));
        }

        // 重构后 Decode 不再返回 this，而是返回通过 TryParse 构造的独立 Hotkey 实例。
        [Fact]
        public void Decode_ValidContent_ReturnsSuccessWithParsedHotkey()
        {
            var result = new Hotkey().Decode("Ctrl+A");

            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Equal("Ctrl+A", result.Value.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("InvalidKey")]
        public void Decode_InvalidContent_ReturnsFailure(string content)
        {
            var result = new Hotkey().Decode(content);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithHotkeyStringAndUnityProvider_EmptyOrWhitespaceStringThrowsArgumentException(string hotkeyText)
        {
            var unityService = UnityProvider.Instance;

            var exception = Assert.Throws<ArgumentException>(() => new Hotkey(hotkeyText, unityService));

            Assert.Equal("Hotkey string cannot be null or whitespace.", exception.Message);
        }

        // 注：原 Constructor_WithUnityProvider_*、Constructor_WithUnityProviderAndHotkeys_* 测试覆盖的
        // Hotkey(UnityProvider) 与 Hotkey(UnityProvider, params HotkeyChord[]) 构造函数已随本次重构移除。
        // 空热键行为由默认构造函数覆盖，按组合构造热键的能力由可变 Hotkeys 列表与 Add 方法覆盖，故删除上述测试。

        [Fact]
        public void Add_NewChord_AddsChordToHotkeys()
        {
            var unityService = UnityProvider.Instance;
            var chord = HotkeyChord.TryParse("Ctrl+A", unityService).Value;
            var hotkey = new Hotkey();

            hotkey.Add(chord);

            Assert.Single(hotkey.Hotkeys);
            Assert.Same(chord, hotkey.Hotkeys[0]);
        }

        [Fact]
        public void Remove_ExistingChord_RemovesChordFromHotkeys()
        {
            var unityService = UnityProvider.Instance;
            var chord = HotkeyChord.TryParse("Ctrl+A", unityService).Value;
            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(chord);

            hotkey.Remove(chord);

            Assert.Empty(hotkey.Hotkeys);
        }

        // Hotkey 通过共享 IEntryValue 契约，基于 HasSameHotkey 比较内容。
        [Fact]
        public void Equals_WhenOtherIsNull_ReturnsFalse()
        {
            var hotkey = new Hotkey("Ctrl+A", UnityProvider.Instance);

            Assert.False(hotkey.Equals(null));
        }

        [Fact]
        public void Equals_WhenOtherIsDifferentType_ReturnsFalse()
        {
            var hotkey = new Hotkey("Ctrl+A", UnityProvider.Instance);

            Assert.False(hotkey.Equals(new OtherConfigValue()));
        }

        [Fact]
        public void Equals_WhenChordsMatchRegardlessOfOrder_ReturnsTrue()
        {
            var first = new Hotkey("Ctrl+A,Shift+B", UnityProvider.Instance);
            var second = new Hotkey("Shift+B,Ctrl+A", UnityProvider.Instance);

            Assert.True(first.Equals(second));
        }

        [Fact]
        public void Equals_WhenChordsDiffer_ReturnsFalse()
        {
            var first = new Hotkey("Ctrl+A", UnityProvider.Instance);
            var second = new Hotkey("Shift+B", UnityProvider.Instance);

            Assert.False(first.Equals(second));
        }

        private sealed class OtherConfigValue : IConfigEntryValue
        {
            public ConfigFileResult<string> Encode() => "x";
            public ConfigFileResult<object> Decode(string content) => new object();
            public ConfigFileResult<string> EncodeValueType() => "Other";
            public bool Equals(IEntryValue other) => ReferenceEquals(this, other);
        }
    }
}
