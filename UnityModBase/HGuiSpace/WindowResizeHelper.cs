using System;
using UnityEngine;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 标识窗口上被抓住用于拉伸的边或角；组合值表示角（同时命中两条边）。
    /// </summary>
    [Flags]
    public enum WindowResizeEdge
    {
        /// <summary>未命中可拉伸区域。</summary>
        None = 0,
        /// <summary>左边缘。</summary>
        Left = 1,
        /// <summary>右边缘。</summary>
        Right = 2,
        /// <summary>上边缘。</summary>
        Top = 4,
        /// <summary>下边缘。</summary>
        Bottom = 8,
        /// <summary>全部边缘与角。</summary>
        All = Left | Right | Top | Bottom,
    }

    /// <summary>
    /// 拉伸开始瞬间鼠标抓取点到窗口四条边的屏幕距离。
    /// 拖动期间保持这些距离不变，窗口边缘跟随鼠标移动而不会在捕获瞬间跳动。
    /// </summary>
    public struct WindowResizeAnchors
    {
        /// <summary>抓取点到窗口左边缘的水平距离。</summary>
        public float Left { get; }
        /// <summary>抓取点到窗口上边缘的垂直距离。</summary>
        public float Top { get; }
        /// <summary>抓取点到窗口右边缘的水平距离。</summary>
        public float Right { get; }
        /// <summary>抓取点到窗口下边缘的垂直距离。</summary>
        public float Bottom { get; }

        /// <summary>
        /// 创建一组拉伸锚点。
        /// </summary>
        /// <param name="left">到左边缘的距离。</param>
        /// <param name="top">到上边缘的距离。</param>
        /// <param name="right">到右边缘的距离。</param>
        /// <param name="bottom">到下边缘的距离。</param>
        public WindowResizeAnchors(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    /// <summary>
    /// 提供窗口鼠标拉伸的纯几何计算：边缘命中测试、按锚点计算拖动后的新矩形，以及持久化矩形的规范化、校验与恢复。
    /// 全部方法不读写 IMGUI 状态，可在无渲染环境下单元测试。
    /// </summary>
    public static class WindowResizeHelper
    {
        /// <summary>边缘命中条带的宽度（窗口本地坐标，像素）。</summary>
        public const float EdgeHandleSize = 8f;
        /// <summary>角命中区域的边长（窗口本地坐标，像素）；角命中优先于边缘命中。</summary>
        public const float CornerHandleSize = 12f;
        /// <summary>恢复持久化位置时，窗口每个方向至少保留在屏幕内的可见长度（像素）。</summary>
        public const float MinVisibleMargin = 40f;

        /// <summary>
        /// 测试窗口本地坐标下的鼠标位置命中了哪条可拉伸边缘或角。
        /// 角在两个方向同时处于 <see cref="CornerHandleSize"/> 内时命中；否则在单方向处于 <see cref="EdgeHandleSize"/> 内时命中对应边缘。
        /// </summary>
        /// <param name="windowSize">窗口尺寸，即本地坐标的有效范围。</param>
        /// <param name="localMouse">相对窗口左上角的鼠标位置。</param>
        /// <param name="allowedEdges">允许拉伸的边缘集合。</param>
        /// <returns>过滤后的命中边缘组合；未命中、位于窗口外或被允许集合排除时返回 <see cref="WindowResizeEdge.None"/>。</returns>
        /// <remarks>
        /// 命中结果先取组合再与 <paramref name="allowedEdges"/> 相交；例如仅允许纵向拉伸时，
        /// 命中右下角会降级为仅命中下边缘，命中左右边缘则被完全排除。
        /// </remarks>
        public static WindowResizeEdge HitTest(Vector2 windowSize, Vector2 localMouse, WindowResizeEdge allowedEdges)
        {
            if (allowedEdges == WindowResizeEdge.None)
                return WindowResizeEdge.None;

            if (localMouse.x < 0f || localMouse.x > windowSize.x || localMouse.y < 0f || localMouse.y > windowSize.y)
                return WindowResizeEdge.None;

            var edge = WindowResizeEdge.None;

            if (localMouse.x < CornerHandleSize && localMouse.y < CornerHandleSize)
                edge = WindowResizeEdge.Left | WindowResizeEdge.Top;
            else if (localMouse.x > windowSize.x - CornerHandleSize && localMouse.y < CornerHandleSize)
                edge = WindowResizeEdge.Right | WindowResizeEdge.Top;
            else if (localMouse.x < CornerHandleSize && localMouse.y > windowSize.y - CornerHandleSize)
                edge = WindowResizeEdge.Left | WindowResizeEdge.Bottom;
            else if (localMouse.x > windowSize.x - CornerHandleSize && localMouse.y > windowSize.y - CornerHandleSize)
                edge = WindowResizeEdge.Right | WindowResizeEdge.Bottom;
            else
            {
                if (localMouse.x < EdgeHandleSize)
                    edge = WindowResizeEdge.Left;
                else if (localMouse.x > windowSize.x - EdgeHandleSize)
                    edge = WindowResizeEdge.Right;
                else if (localMouse.y < EdgeHandleSize)
                    edge = WindowResizeEdge.Top;
                else if (localMouse.y > windowSize.y - EdgeHandleSize)
                    edge = WindowResizeEdge.Bottom;
            }

            return edge & allowedEdges;
        }

        /// <summary>
        /// 计算屏幕鼠标位置到窗口四条边的锚点距离。
        /// </summary>
        /// <param name="windowRect">拉伸开始时的窗口矩形（屏幕坐标）。</param>
        /// <param name="screenMouse">拉伸开始时的鼠标屏幕位置。</param>
        public static WindowResizeAnchors GetGrabAnchors(Rect windowRect, Vector2 screenMouse)
        {
            return new WindowResizeAnchors(
                screenMouse.x - windowRect.xMin,
                screenMouse.y - windowRect.yMin,
                windowRect.xMax - screenMouse.x,
                windowRect.yMax - screenMouse.y);
        }

        /// <summary>
        /// 按拉伸边缘和当前屏幕鼠标位置计算新的窗口矩形。
        /// 被拖动边缘保持与鼠标的锚点距离；先施加最小尺寸约束，再把被拖动边缘钳制到屏幕内，
        /// 两者冲突（屏幕比最小尺寸还小）时优先保留最小尺寸，允许窗口越出屏幕。
        /// 未被 <paramref name="edge"/> 覆盖的边缘保持原位。
        /// </summary>
        /// <param name="windowRect">拖动前的窗口矩形（屏幕坐标）。</param>
        /// <param name="edge">本次拖动的边缘组合。</param>
        /// <param name="anchors">拉伸开始时记录的锚点距离。</param>
        /// <param name="screenMouse">当前鼠标屏幕位置。</param>
        /// <param name="minSize">最小窗口尺寸。</param>
        /// <param name="screenSize">屏幕尺寸。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="edge"/> 为 <see cref="WindowResizeEdge.None"/> 或含未定义的位。</exception>
        public static Rect Resize(
            Rect windowRect,
            WindowResizeEdge edge,
            WindowResizeAnchors anchors,
            Vector2 screenMouse,
            Vector2 minSize,
            Vector2 screenSize)
        {
            if (edge == WindowResizeEdge.None || (edge & ~WindowResizeEdge.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(edge));

            var left = windowRect.xMin;
            var top = windowRect.yMin;
            var right = windowRect.xMax;
            var bottom = windowRect.yMax;

            if ((edge & WindowResizeEdge.Left) != 0)
            {
                left = Mathf.Min(screenMouse.x - anchors.Left, right - minSize.x);
                left = Mathf.Max(left, 0f);
                if (right - left < minSize.x)
                    left = right - minSize.x;
            }

            if ((edge & WindowResizeEdge.Right) != 0)
            {
                right = Mathf.Max(screenMouse.x + anchors.Right, left + minSize.x);
                right = Mathf.Min(right, screenSize.x);
                if (right - left < minSize.x)
                    right = left + minSize.x;
            }

            if ((edge & WindowResizeEdge.Top) != 0)
            {
                top = Mathf.Min(screenMouse.y - anchors.Top, bottom - minSize.y);
                top = Mathf.Max(top, 0f);
                if (bottom - top < minSize.y)
                    top = bottom - minSize.y;
            }

            if ((edge & WindowResizeEdge.Bottom) != 0)
            {
                bottom = Mathf.Max(screenMouse.y + anchors.Bottom, top + minSize.y);
                bottom = Mathf.Min(bottom, screenSize.y);
                if (bottom - top < minSize.y)
                    bottom = top + minSize.y;
            }

            return Rect.MinMaxRect(left, top, right, bottom);
        }

        /// <summary>
        /// 把窗口位置规范化到完全位于屏幕内，供持久化写回使用；尺寸保持不变。
        /// 持久化以负数位置作为未设置哨兵，写盘前必须先经过本方法消除负坐标。
        /// </summary>
        /// <param name="windowRect">当前窗口矩形（屏幕坐标）。</param>
        /// <param name="screenSize">屏幕尺寸。</param>
        public static Rect NormalizeForPersist(Rect windowRect, Vector2 screenSize)
        {
            var x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, screenSize.x - windowRect.width));
            var y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, screenSize.y - windowRect.height));
            return new Rect(x, y, windowRect.width, windowRect.height);
        }

        /// <summary>
        /// 从持久化的四个浮点值构建恢复用的窗口矩形。
        /// 宽或高非正数视为未持久化尺寸，返回 false 由调用方沿用默认矩形；
        /// 位置为负数表示对应方向未持久化，该方向采用屏幕居中。
        /// </summary>
        /// <param name="x">持久化的窗口横坐标。</param>
        /// <param name="y">持久化的窗口纵坐标。</param>
        /// <param name="width">持久化的窗口宽度。</param>
        /// <param name="height">持久化的窗口高度。</param>
        /// <param name="minSize">最小窗口尺寸。</param>
        /// <param name="screenSize">屏幕尺寸。</param>
        /// <param name="rect">构建并钳制后的矩形；失败时为 <c>default(Rect)</c>。</param>
        /// <returns>尺寸已持久化且成功构建时为 <c>true</c>。</returns>
        /// <remarks>结果尺寸钳制到 [minSize, screenSize]，位置钳制到每个方向至少保留 <see cref="MinVisibleMargin"/> 在屏幕内。</remarks>
        public static bool TryBuildPersistedRect(
            float x,
            float y,
            float width,
            float height,
            Vector2 minSize,
            Vector2 screenSize,
            out Rect rect)
        {
            if (width <= 0f || height <= 0f)
            {
                rect = default;
                return false;
            }

            if (x < 0f)
                x = (screenSize.x - width) / 2f;
            if (y < 0f)
                y = (screenSize.y - height) / 2f;

            rect = ClampToScreen(new Rect(x, y, width, height), minSize, screenSize);
            return true;
        }

        /// <summary>
        /// 把窗口尺寸钳制到 [minSize, screenSize]，并把位置钳制到每个方向至少保留 <see cref="MinVisibleMargin"/> 在屏幕内。
        /// </summary>
        /// <param name="windowRect">待钳制的窗口矩形（屏幕坐标）。</param>
        /// <param name="minSize">最小窗口尺寸。</param>
        /// <param name="screenSize">屏幕尺寸。</param>
        public static Rect ClampToScreen(Rect windowRect, Vector2 minSize, Vector2 screenSize)
        {
            var width = Mathf.Clamp(windowRect.width, minSize.x, Mathf.Max(minSize.x, screenSize.x));
            var height = Mathf.Clamp(windowRect.height, minSize.y, Mathf.Max(minSize.y, screenSize.y));

            // 标题栏位于窗口顶部，纵坐标下限取 0 保证标题可见；横坐标允许部分越界，只需保留最小可见宽度。
            var xLower = MinVisibleMargin - width;
            var xUpper = screenSize.x - MinVisibleMargin;
            var x = Mathf.Clamp(windowRect.x, Mathf.Min(xLower, xUpper), Mathf.Max(xLower, xUpper));
            var y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, screenSize.y - MinVisibleMargin));

            return new Rect(x, y, width, height);
        }
    }
}
