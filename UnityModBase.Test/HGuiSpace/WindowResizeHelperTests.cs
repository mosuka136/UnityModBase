using System;
using System.Reflection;
using Moq;
using UnityModBase.HConfigSpace;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HProvider;
using UnityEngine;
using ConfigGuiHost = UnityModBase.HConfigGUI.GuiHost;
using LogGuiHost = UnityModBase.HLogGUI.GuiHost;

namespace UnityModBase.Test.HGuiSpace
{
    public class WindowResizeHelperTests
    {
        private static readonly Vector2 Screen = new Vector2(1920f, 1080f);
        private static readonly Vector2 MinSize = new Vector2(280f, 180f);
        private static readonly Vector2 WindowSize = new Vector2(400f, 300f);

        // ---- HitTest ----

        [Fact]
        public void HitTest_WhenMouseInCorner_ReturnsCombinedEdges()
        {
            Assert.Equal(WindowResizeEdge.Left | WindowResizeEdge.Top, WindowResizeHelper.HitTest(WindowSize, new Vector2(5f, 5f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.Right | WindowResizeEdge.Top, WindowResizeHelper.HitTest(WindowSize, new Vector2(395f, 5f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.Left | WindowResizeEdge.Bottom, WindowResizeHelper.HitTest(WindowSize, new Vector2(5f, 295f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.Right | WindowResizeEdge.Bottom, WindowResizeHelper.HitTest(WindowSize, new Vector2(395f, 295f), WindowResizeEdge.All));
        }

        [Fact]
        public void HitTest_WhenMouseOnEdgeStrip_ReturnsSingleEdge()
        {
            Assert.Equal(WindowResizeEdge.Left, WindowResizeHelper.HitTest(WindowSize, new Vector2(3f, 150f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.Right, WindowResizeHelper.HitTest(WindowSize, new Vector2(397f, 150f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.Top, WindowResizeHelper.HitTest(WindowSize, new Vector2(200f, 3f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.Bottom, WindowResizeHelper.HitTest(WindowSize, new Vector2(200f, 297f), WindowResizeEdge.All));
        }

        [Fact]
        public void HitTest_WhenMouseBetweenStripAndCorner_ReturnsNone()
        {
            // x=10 超出 8px 边缘条带，但 y 处于中部不构成角命中。
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(10f, 150f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(200f, 200f), WindowResizeEdge.All));
        }

        [Fact]
        public void HitTest_WhenMouseOutsideWindow_ReturnsNone()
        {
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(-1f, 150f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(401f, 150f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(200f, -1f), WindowResizeEdge.All));
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(200f, 301f), WindowResizeEdge.All));
        }

        [Fact]
        public void HitTest_FiltersResultByAllowedEdges()
        {
            var verticalOnly = WindowResizeEdge.Top | WindowResizeEdge.Bottom;

            // 右下角在仅纵向时降级为下边缘；左右边缘被完全排除。
            Assert.Equal(WindowResizeEdge.Bottom, WindowResizeHelper.HitTest(WindowSize, new Vector2(395f, 295f), verticalOnly));
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(3f, 150f), verticalOnly));
            Assert.Equal(WindowResizeEdge.None, WindowResizeHelper.HitTest(WindowSize, new Vector2(3f, 3f), WindowResizeEdge.None));
        }

        // ---- GetGrabAnchors / Resize ----

        [Fact]
        public void Resize_BottomRightCorner_FollowsMouseWithoutJump()
        {
            var window = new Rect(100f, 50f, 400f, 300f);
            var grab = new Vector2(490f, 340f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, grab);
            Assert.Equal(390f, anchors.Left);
            Assert.Equal(290f, anchors.Top);
            Assert.Equal(10f, anchors.Right);
            Assert.Equal(10f, anchors.Bottom);

            var result = WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.Right | WindowResizeEdge.Bottom,
                anchors,
                new Vector2(600f, 450f),
                MinSize,
                Screen);

            Assert.Equal(100f, result.xMin);
            Assert.Equal(50f, result.yMin);
            Assert.Equal(610f, result.xMax);
            Assert.Equal(460f, result.yMax);
        }

        [Fact]
        public void Resize_LeftEdge_MovesLeftEdgeOnly()
        {
            var window = new Rect(100f, 50f, 400f, 300f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, new Vector2(105f, 200f));

            var result = WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.Left,
                anchors,
                new Vector2(55f, 260f),
                MinSize,
                Screen);

            Assert.Equal(50f, result.xMin);
            Assert.Equal(500f, result.xMax);
            Assert.Equal(450f, result.width);
            Assert.Equal(50f, result.yMin);
            Assert.Equal(350f, result.yMax);
        }

        [Fact]
        public void Resize_LeftEdgeBeyondMinWidth_ClampsToMinSize()
        {
            var window = new Rect(100f, 50f, 400f, 300f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, new Vector2(105f, 200f));

            var result = WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.Left,
                anchors,
                new Vector2(400f, 200f),
                MinSize,
                Screen);

            Assert.Equal(220f, result.xMin);
            Assert.Equal(500f, result.xMax);
            Assert.Equal(280f, result.width);
        }

        [Fact]
        public void Resize_RightEdgeBeyondScreen_ClampsToScreen()
        {
            var window = new Rect(100f, 50f, 400f, 300f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, new Vector2(495f, 200f));

            var result = WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.Right,
                anchors,
                new Vector2(3000f, 200f),
                MinSize,
                Screen);

            Assert.Equal(1920f, result.xMax);
        }

        [Fact]
        public void Resize_BottomEdgeBeyondMinHeight_ClampsToMinSize()
        {
            var window = new Rect(100f, 50f, 400f, 300f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, new Vector2(300f, 345f));

            var result = WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.Bottom,
                anchors,
                new Vector2(300f, 100f),
                MinSize,
                Screen);

            Assert.Equal(230f, result.yMax);
            Assert.Equal(180f, result.height);
        }

        [Fact]
        public void Resize_WhenScreenSmallerThanMin_KeepsMinSizeBeyondScreen()
        {
            var window = new Rect(0f, 0f, 300f, 200f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, new Vector2(295f, 100f));
            var tinyScreen = new Vector2(200f, 150f);

            var result = WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.Right,
                anchors,
                new Vector2(260f, 100f),
                MinSize,
                tinyScreen);

            Assert.Equal(280f, result.width);
            Assert.Equal(280f, result.xMax);
        }

        [Fact]
        public void Resize_WhenEdgeIsNoneOrUndefined_Throws()
        {
            var window = new Rect(100f, 50f, 400f, 300f);
            var anchors = WindowResizeHelper.GetGrabAnchors(window, new Vector2(105f, 200f));

            Assert.Throws<ArgumentOutOfRangeException>(() => WindowResizeHelper.Resize(
                window,
                WindowResizeEdge.None,
                anchors,
                new Vector2(200f, 200f),
                MinSize,
                Screen));
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowResizeHelper.Resize(
                window,
                (WindowResizeEdge)16,
                anchors,
                new Vector2(200f, 200f),
                MinSize,
                Screen));
        }

        // ---- 持久化构建与规范化 ----

        [Fact]
        public void TryBuildPersistedRect_WhenValuesValid_ReturnsRectUnchanged()
        {
            var success = WindowResizeHelper.TryBuildPersistedRect(100f, 50f, 500f, 400f, MinSize, Screen, out var rect);

            Assert.True(success);
            Assert.Equal(100f, rect.x);
            Assert.Equal(50f, rect.y);
            Assert.Equal(500f, rect.width);
            Assert.Equal(400f, rect.height);
        }

        [Fact]
        public void TryBuildPersistedRect_WhenSizeNotPersisted_ReturnsFalse()
        {
            Assert.False(WindowResizeHelper.TryBuildPersistedRect(100f, 50f, 0f, 400f, MinSize, Screen, out _));
            Assert.False(WindowResizeHelper.TryBuildPersistedRect(100f, 50f, 500f, -1f, MinSize, Screen, out _));
        }

        [Fact]
        public void TryBuildPersistedRect_WhenPositionNotPersisted_CentersThatAxis()
        {
            var success = WindowResizeHelper.TryBuildPersistedRect(-1f, -1f, 500f, 400f, MinSize, Screen, out var rect);

            Assert.True(success);
            Assert.Equal((1920f - 500f) / 2f, rect.x);
            Assert.Equal((1080f - 400f) / 2f, rect.y);
        }

        [Fact]
        public void TryBuildPersistedRect_WhenSizeOutOfRange_ClampsToMinAndScreen()
        {
            var success = WindowResizeHelper.TryBuildPersistedRect(0f, 0f, 100f, 5000f, MinSize, Screen, out var rect);

            Assert.True(success);
            Assert.Equal(280f, rect.width);
            Assert.Equal(1080f, rect.height);
        }

        [Fact]
        public void TryBuildPersistedRect_WhenPositionOffscreen_ClampsToKeepVisible()
        {
            var success = WindowResizeHelper.TryBuildPersistedRect(5000f, 2000f, 500f, 400f, MinSize, Screen, out var rect);

            Assert.True(success);
            Assert.Equal(1920f - WindowResizeHelper.MinVisibleMargin, rect.x);
            Assert.Equal(1080f - WindowResizeHelper.MinVisibleMargin, rect.y);
        }

        [Fact]
        public void NormalizeForPersist_WhenWindowPartiallyOffscreen_BringsFullyInside()
        {
            var result = WindowResizeHelper.NormalizeForPersist(new Rect(-100f, -50f, 400f, 300f), Screen);
            Assert.Equal(0f, result.x);
            Assert.Equal(0f, result.y);

            result = WindowResizeHelper.NormalizeForPersist(new Rect(1800f, 1000f, 400f, 300f), Screen);
            Assert.Equal(1520f, result.x);
            Assert.Equal(780f, result.y);
        }

        [Fact]
        public void NormalizeForPersist_WhenWindowInsideScreen_KeepsPosition()
        {
            var result = WindowResizeHelper.NormalizeForPersist(new Rect(300f, 200f, 400f, 300f), Screen);
            Assert.Equal(300f, result.x);
            Assert.Equal(200f, result.y);
            Assert.Equal(400f, result.width);
            Assert.Equal(300f, result.height);
        }

        // ---- 宿主接线 ----

        [Fact]
        public void InitializeWindowRect_WhenNothingPersisted_KeepsDefaultCenteredRect()
        {
            var sut = CreateHost();
            InvokeInitialize(sut, CreateEntry(-1f), CreateEntry(-1f), CreateEntry(0f), CreateEntry(0f), 0.35f, 0.7f);

            Assert.Equal(624f, sut.WindowRect.x);
            Assert.Equal(162f, sut.WindowRect.y);
            Assert.Equal(672f, sut.WindowRect.width);
            Assert.Equal(756f, sut.WindowRect.height);
        }

        [Fact]
        public void InitializeWindowRect_WhenLayoutPersisted_RestoresClampedRect()
        {
            var sut = CreateHost();
            InvokeInitialize(sut, CreateEntry(100f), CreateEntry(50f), CreateEntry(5000f), CreateEntry(100f), 0.35f, 0.7f);

            Assert.Equal(100f, sut.WindowRect.x);
            Assert.Equal(50f, sut.WindowRect.y);
            Assert.Equal(1920f, sut.WindowRect.width);
            Assert.Equal(180f, sut.WindowRect.height);
        }

        [Fact]
        public void InitializeWindowRect_WhenEntryChangesLater_AppliesNewRect()
        {
            var sut = CreateHost();
            var xEntry = CreateEntry(100f);
            var yEntry = CreateEntry(50f);
            var widthEntry = CreateEntry(500f);
            var heightEntry = CreateEntry(400f);
            InvokeInitialize(sut, xEntry, yEntry, widthEntry, heightEntry);

            widthEntry.Value = 700f;

            Assert.Equal(700f, sut.WindowRect.width);
        }

        [Fact]
        public void InitializeWindowRect_WhenAnyEntryIsNull_ThrowsArgumentNullException()
        {
            var sut = CreateHost();

            var exception = Assert.Throws<TargetInvocationException>(
                () => InvokeInitialize(sut, null, CreateEntry(0f), CreateEntry(0f), CreateEntry(0f)));
            var argumentException = Assert.IsType<ArgumentNullException>(exception.InnerException);

            Assert.Equal("xEntry", argumentException.ParamName);
        }

        [Fact]
        public void SaveWindowLayout_WritesNormalizedRectToEntries()
        {
            var sut = CreateHost();
            var xEntry = CreateEntry(-1f);
            var yEntry = CreateEntry(-1f);
            var widthEntry = CreateEntry(0f);
            var heightEntry = CreateEntry(0f);
            InvokeInitialize(sut, xEntry, yEntry, widthEntry, heightEntry);
            SetProperty(sut, nameof(ConfigGuiHost.WindowRect), new Rect(-40f, -60f, 500f, 400f));

            InvokeNonPublic(sut, "SaveWindowLayout");

            Assert.Equal(0f, xEntry.Value);
            Assert.Equal(0f, yEntry.Value);
            Assert.Equal(500f, widthEntry.Value);
            Assert.Equal(400f, heightEntry.Value);
        }

        [Fact]
        public void SaveWindowLayout_WhenNothingChanged_SkipsWrite()
        {
            var sut = CreateHost();
            var xEntry = CreateEntry(100f);
            var yEntry = CreateEntry(50f);
            var widthEntry = CreateEntry(500f);
            var heightEntry = CreateEntry(400f);
            InvokeInitialize(sut, xEntry, yEntry, widthEntry, heightEntry);
            var changeCount = 0;
            xEntry.OnValueChangedBase += OnEntryChanged;
            yEntry.OnValueChangedBase += OnEntryChanged;
            widthEntry.OnValueChangedBase += OnEntryChanged;
            heightEntry.OnValueChangedBase += OnEntryChanged;

            InvokeNonPublic(sut, "SaveWindowLayout");

            // 恢复后的矩形已作为基线，等值保存不应写回条目或产生新的值变化事件。
            Assert.Equal(0, changeCount);
            Assert.Equal(100f, xEntry.Value);
            Assert.Equal(50f, yEntry.Value);
            Assert.Equal(500f, widthEntry.Value);
            Assert.Equal(400f, heightEntry.Value);

            void OnEntryChanged(object sender, EventArgs args)
            {
                changeCount++;
            }
        }

        [Fact]
        public void WindowRectEntrySubscription_AfterBaseOnDestroy_StopsApplyingChanges()
        {
            var sut = CreateHost();
            var xEntry = CreateEntry(100f);
            var yEntry = CreateEntry(50f);
            var widthEntry = CreateEntry(500f);
            var heightEntry = CreateEntry(400f);
            InvokeInitialize(sut, xEntry, yEntry, widthEntry, heightEntry);
            var before = sut.WindowRect;

            InvokeBaseOnDestroy(sut);
            widthEntry.Value = 900f;

            Assert.Equal(before.width, sut.WindowRect.width);
        }

        [Fact]
        public void Update_AfterWindowStopsMoving_SavesLayoutAfterDebounce()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.ScreenWidth).Returns(Screen.x);
            unityGui.SetupGet(x => x.ScreenHeight).Returns(Screen.y);
            var unityService = new Mock<IUnityProvider>(MockBehavior.Strict);
            unityService.SetupGet(x => x.UnscaledDeltaTime).Returns(0.25f);
            var sut = new ConfigGuiHost();
            SetProperty(sut, nameof(ConfigGuiHost.UnityGui), unityGui.Object);
            SetProperty(sut, nameof(ConfigGuiHost.UnityService), unityService.Object);
            var xEntry = CreateEntry(100f);
            var yEntry = CreateEntry(50f);
            var widthEntry = CreateEntry(500f);
            var heightEntry = CreateEntry(400f);
            InvokeInitialize(sut, xEntry, yEntry, widthEntry, heightEntry);

            // 模拟一次拖动产生的位置变化：写回矩形并标记脏。
            var apply = typeof(GuiHostBase).GetMethod(
                "ApplyReturnedWindowRect",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(apply);
            apply.Invoke(sut, new object[] { new Rect(100f, 50f, 500f, 400f), new Rect(140f, 90f, 500f, 400f) });

            // 停顿计时未满不写盘；期满后下一次 Update 写盘。
            InvokeNonPublic(sut, "Update");
            Assert.Equal(100f, xEntry.Value);
            InvokeNonPublic(sut, "Update");
            Assert.Equal(140f, xEntry.Value);
            Assert.Equal(90f, yEntry.Value);
            Assert.Equal(500f, widthEntry.Value);
            Assert.Equal(400f, heightEntry.Value);
        }

        [Fact]
        public void LogGuiHost_AllowedResizeEdges_IsVerticalOnly()
        {
            var sut = new LogGuiHost();
            var property = typeof(GuiHostBase).GetProperty("AllowedResizeEdges", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Equal(WindowResizeEdge.Top | WindowResizeEdge.Bottom, (WindowResizeEdge)property.GetValue(sut));
        }

        [Fact]
        public void GuiHostBase_MinWindowSize_DefaultsToCommonFloor()
        {
            var property = typeof(GuiHostBase).GetProperty("MinWindowSize", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Equal(new Vector2(280f, 180f), (Vector2)property.GetValue(CreateHost()));
        }

        private static ConfigGuiHost CreateHost()
        {
            var unityGui = new Mock<IUnityGuiProvider>(MockBehavior.Strict);
            unityGui.SetupGet(x => x.ScreenWidth).Returns(Screen.x);
            unityGui.SetupGet(x => x.ScreenHeight).Returns(Screen.y);

            var sut = new ConfigGuiHost();
            SetProperty(sut, nameof(ConfigGuiHost.UnityGui), unityGui.Object);
            return sut;
        }

        private static ConfigEntry<float> CreateEntry(float value)
        {
            return new ConfigEntry<float>(
                new ConfigFileEntry
                {
                    Key = $"WindowRect{Guid.NewGuid():N}",
                    Value = ConfigFileEntry.EncodeValue(value).Value,
                },
                value,
                new Translator(),
                new Translator());
        }

        private static void InvokeInitialize(
            ConfigGuiHost host,
            ConfigEntry<float> xEntry,
            ConfigEntry<float> yEntry,
            ConfigEntry<float> widthEntry,
            ConfigEntry<float> heightEntry,
            float widthRatio = 0f,
            float heightRatio = 0f)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo method;
            object[] arguments;

            if (widthRatio > 0f)
            {
                method = typeof(GuiHostBase).GetMethod(
                    "InitializeWindowRect",
                    flags,
                    null,
                    new[]
                    {
                        typeof(ConfigEntry<float>),
                        typeof(ConfigEntry<float>),
                        typeof(ConfigEntry<float>),
                        typeof(ConfigEntry<float>),
                        typeof(float),
                        typeof(float),
                    },
                    null);
                arguments = new object[] { xEntry, yEntry, widthEntry, heightEntry, widthRatio, heightRatio };
            }
            else
            {
                method = typeof(GuiHostBase).GetMethod(
                    "InitializeWindowRect",
                    flags,
                    null,
                    new[]
                    {
                        typeof(ConfigEntry<float>),
                        typeof(ConfigEntry<float>),
                        typeof(ConfigEntry<float>),
                        typeof(ConfigEntry<float>),
                    },
                    null);
                arguments = new object[] { xEntry, yEntry, widthEntry, heightEntry };
            }

            Assert.NotNull(method);
            method.Invoke(host, arguments);
        }

        private static void InvokeBaseOnDestroy(ConfigGuiHost host)
        {
            var method = typeof(GuiHostBase).GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(host, null);
        }

        private static void InvokeNonPublic(ConfigGuiHost host, string methodName)
        {
            var method = typeof(GuiHostBase).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(host, null);
        }

        private static void SetProperty(ConfigGuiHost host, string propertyName, object value)
        {
            var property = typeof(GuiHostBase).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            property.SetValue(host, value);
        }
    }
}
