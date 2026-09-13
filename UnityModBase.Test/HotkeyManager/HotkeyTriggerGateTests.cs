using Moq;
using UnityModBase.HotkeyManager;

namespace UnityModBase.Test.HotkeyManager
{
    public class HotkeyTriggerGateTests
    {
        [Fact]
        public void ShouldTrigger_WhenHotkeyIsNull_ReturnsFalseWithoutConsumingSilence()
        {
            var gate = new HotkeyTriggerGate();

            Assert.False(gate.ShouldTrigger(null, () => 10f));
            // 空热键的查询没有边沿，不应进入静默期；随后真实边沿立即可被接受。
            var hotkey = CreateHotkeyWithEdge(edge => true);
            Assert.True(gate.ShouldTrigger(hotkey, () => 10f));
        }

        [Fact]
        public void ShouldTrigger_WhenEdgeReportedInConsecutiveFrames_AcceptsOnlyOnce()
        {
            // 部分游戏输入配置会把一次物理按下重复报告为连续多帧边沿；只有首帧被接受。
            var gate = new HotkeyTriggerGate();
            var hotkey = CreateHotkeyWithEdge(edge => true);

            Assert.True(gate.ShouldTrigger(hotkey, () => 10f));
            Assert.False(gate.ShouldTrigger(hotkey, () => 10.02f));
            Assert.False(gate.ShouldTrigger(hotkey, () => 10.05f));
        }

        [Fact]
        public void ShouldTrigger_WhenEdgeReturnsAfterSilence_AcceptsAgain()
        {
            var gate = new HotkeyTriggerGate();
            var hotkey = CreateHotkeyWithEdge(edge => true);

            Assert.True(gate.ShouldTrigger(hotkey, () => 10f));
            Assert.False(gate.ShouldTrigger(hotkey, () => 10.05f));
            Assert.True(gate.ShouldTrigger(hotkey, () => 10.2f));
        }

        [Fact]
        public void ShouldTrigger_WhenNoEdge_DoesNotConsumeSilence()
        {
            var gate = new HotkeyTriggerGate();
            var hotkey = CreateHotkeyWithEdge(edge => false);

            Assert.False(gate.ShouldTrigger(hotkey, () => 10f));
            Assert.False(gate.ShouldTrigger(hotkey, () => 10.2f));

            // 无边沿的查询不改变门状态；边沿出现时立即接受。
            hotkey.Hotkeys[0].Chord = CreateEdgeChord(edge => true);
            Assert.True(gate.ShouldTrigger(hotkey, () => 10.21f));
        }

        /// <summary>
        /// 构造不依赖引擎输入状态的热键：组合边沿由回调按调用次数动态决定。
        /// </summary>
        private static Hotkey CreateHotkeyWithEdge(Func<int, bool> edgeByCallIndex)
        {
            var hotkey = new Hotkey();
            hotkey.Hotkeys.Add(new HotkeyChord(CreateEdgeChord(edgeByCallIndex), null));
            return hotkey;
        }

        private static IHotkeyChord CreateEdgeChord(Func<int, bool> edgeByCallIndex)
        {
            var chord = new Mock<IHotkeyChord>(MockBehavior.Strict);
            var calls = 0;
            chord.Setup(c => c.WasPressedThisFrame()).Returns(() => edgeByCallIndex(calls++));
            return chord.Object;
        }
    }
}
