using UnityEngine;

namespace UnityModBase.HProvider
{
    /// <summary>
    /// 抽象 Unity IMGUI 与 GUILayout 的即时模式绘制 API。
    /// 所有绘制和状态读取应发生在有效的 <c>OnGUI</c> 调用链内；Begin/End 方法必须由调用方成对、同层级使用。
    /// </summary>
    public interface IUnityGuiProvider
    {
        /// <summary>
        /// 当前鼠标悬停控件设置的提示文本。
        /// </summary>
        string Tooltip { get; }

        /// <summary>
        /// 当前 GUI 皮肤的标签样式。
        /// </summary>
        GUIStyle LabelStyle { get; }

        /// <summary>
        /// 当前 GUI 皮肤的开关样式。
        /// </summary>
        GUIStyle ToggleStyle { get; }

        /// <summary>
        /// 当前 GUI 皮肤的按钮样式。
        /// </summary>
        GUIStyle ButtonStyle { get; }

        /// <summary>
        /// 当前 GUI 皮肤的文本框样式。
        /// </summary>
        GUIStyle TextFieldStyle { get; }

        /// <summary>
        /// 当前 GUI 皮肤的水平滑条轨道样式。
        /// </summary>
        GUIStyle HorizontalSliderStyle { get; }

        /// <summary>
        /// 当前 GUI 皮肤的水平滑条滑块样式。
        /// </summary>
        GUIStyle HorizontalSliderThumbStyle { get; }

        /// <summary>
        /// 当前 GUI 皮肤的容器框样式。
        /// </summary>
        GUIStyle BoxStyle { get; }

        /// <summary>
        /// 当前 GUI 的全局色调。修改后会影响后续控件，调用方负责在作用域结束时恢复原值。
        /// </summary>
        Color Color { get; set; }

        /// <summary>
        /// 当前屏幕宽度，单位为像素。
        /// </summary>
        float ScreenWidth { get; }

        /// <summary>
        /// 当前屏幕高度，单位为像素。
        /// </summary>
        float ScreenHeight { get; }

        /// <summary>
        /// 绘制阻止其他窗口接收输入的模态窗口。
        /// </summary>
        /// <param name="id">窗口唯一标识。</param>
        /// <param name="clientRect">窗口初始屏幕矩形。</param>
        /// <param name="func">绘制窗口内容的回调。</param>
        /// <param name="title">窗口标题。</param>
        /// <param name="style">窗口样式。</param>
        /// <returns>Unity 处理拖动等交互后的窗口矩形。</returns>
        Rect ModalWindow(int id, Rect clientRect, GUI.WindowFunction func, string title, GUIStyle style);

        /// <summary>
        /// 开始在指定屏幕矩形内绘制布局控件，后续必须调用 <see cref="EndArea"/>。
        /// </summary>
        /// <param name="screenRect">区域的屏幕坐标矩形。</param>
        void BeginArea(Rect screenRect);

        /// <summary>
        /// 结束最近开始的布局区域。
        /// </summary>
        void EndArea();

        /// <summary>
        /// 开始水平布局组，后续必须调用 <see cref="EndHorizontal"/>。
        /// </summary>
        /// <param name="options">布局约束。</param>
        void BeginHorizontal(params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定样式开始水平布局组，后续必须调用 <see cref="EndHorizontal"/>。
        /// </summary>
        /// <param name="style">布局组样式。</param>
        /// <param name="options">布局约束。</param>
        void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 结束最近开始的水平布局组。
        /// </summary>
        void EndHorizontal();

        /// <summary>
        /// 开始滚动视图，后续必须调用 <see cref="EndScrollView"/>。
        /// </summary>
        /// <param name="scrollPosition">当前滚动偏移，单位为像素。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前 IMGUI 事件后的滚动偏移。</returns>
        Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options);

        /// <summary>
        /// 结束最近开始的滚动视图。
        /// </summary>
        void EndScrollView();

        /// <summary>
        /// 开始使用默认样式的垂直布局组，后续必须调用 <see cref="EndVertical"/>。
        /// </summary>
        void BeginVertical();

        /// <summary>
        /// 使用指定样式开始垂直布局组，后续必须调用 <see cref="EndVertical"/>。
        /// </summary>
        /// <param name="style">布局组样式。</param>
        /// <param name="options">布局约束。</param>
        void BeginVertical(GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 结束最近开始的垂直布局组。
        /// </summary>
        void EndVertical();

        /// <summary>
        /// 在当前布局方向插入固定像素间距。
        /// </summary>
        /// <param name="pixels">间距大小，单位为像素。</param>
        void Space(float pixels);

        /// <summary>
        /// 插入可扩展空白以占用当前布局组的剩余空间。
        /// </summary>
        void FlexibleSpace();

        /// <summary>
        /// 使用当前皮肤绘制文本标签。
        /// </summary>
        /// <param name="text">标签文本。</param>
        /// <param name="options">布局约束。</param>
        void Label(string text, params GUILayoutOption[] options);

        /// <summary>
        /// 使用当前皮肤绘制可含图像或提示的标签内容。
        /// </summary>
        /// <param name="content">标签内容。</param>
        /// <param name="options">布局约束。</param>
        void Label(GUIContent content, params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定样式绘制文本标签。
        /// </summary>
        /// <param name="text">标签文本。</param>
        /// <param name="style">标签样式。</param>
        /// <param name="options">布局约束。</param>
        void Label(string text, GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定样式绘制标签内容。
        /// </summary>
        /// <param name="content">标签内容。</param>
        /// <param name="style">标签样式。</param>
        /// <param name="options">布局约束。</param>
        void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 在固定矩形中使用指定样式绘制标签，不参与自动布局。
        /// </summary>
        /// <param name="position">GUI 坐标矩形。</param>
        /// <param name="content">标签内容。</param>
        /// <param name="style">标签样式。</param>
        void Label(Rect position, GUIContent content, GUIStyle style);

        /// <summary>
        /// 使用当前皮肤绘制开关控件。
        /// </summary>
        /// <param name="value">当前开关值。</param>
        /// <param name="text">显示文本。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前事件后的开关值。</returns>
        bool Toggle(bool value, string text, params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定样式绘制开关控件。
        /// </summary>
        /// <param name="value">当前开关值。</param>
        /// <param name="text">显示文本。</param>
        /// <param name="style">开关样式。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前事件后的开关值。</returns>
        bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 使用当前皮肤绘制可含图像或提示的开关控件。
        /// </summary>
        /// <param name="value">当前开关值。</param>
        /// <param name="content">显示内容和悬停提示。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前事件后的开关值。</returns>
        bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options);

        /// <summary>
        /// 绘制单行文本输入框。
        /// </summary>
        /// <param name="text">当前文本。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前事件后的文本。</returns>
        string TextField(string text, params GUILayoutOption[] options);

        /// <summary>
        /// 在刚绘制的布局控件矩形上设置悬停提示，不改变其布局或交互行为。
        /// </summary>
        /// <param name="tooltip">悬停提示；为空时不绘制提示区域。</param>
        /// <remarks>
        /// 基于最近一个布局控件的矩形实现，必须在目标控件绘制后、绘制其他控件前调用，
        /// 否则提示会落到后续控件上。
        /// </remarks>
        void SetLastControlTooltip(string tooltip);

        /// <summary>
        /// 使用当前皮肤绘制文本按钮。
        /// </summary>
        /// <param name="text">按钮文本。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>按钮在当前事件中被激活时为 <c>true</c>。</returns>
        bool Button(string text, params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定样式绘制内容按钮。
        /// </summary>
        /// <param name="content">按钮内容。</param>
        /// <param name="style">按钮样式。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>按钮在当前事件中被激活时为 <c>true</c>。</returns>
        bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定样式绘制文本按钮。
        /// </summary>
        /// <param name="text">按钮文本。</param>
        /// <param name="style">按钮样式。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>按钮在当前事件中被激活时为 <c>true</c>。</returns>
        bool Button(string text, GUIStyle style, params GUILayoutOption[] options);

        /// <summary>
        /// 使用指定轨道和滑块样式绘制水平滑条。
        /// </summary>
        /// <param name="value">当前值。</param>
        /// <param name="leftValue">左端值。</param>
        /// <param name="rightValue">右端值。</param>
        /// <param name="slider">轨道样式。</param>
        /// <param name="thumb">滑块样式。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前事件后的值。</returns>
        float HorizontalSlider(float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb, params GUILayoutOption[] options);

        /// <summary>
        /// 绘制固定列数的单选网格。
        /// </summary>
        /// <param name="selected">当前选中项索引。</param>
        /// <param name="texts">各项显示文本。</param>
        /// <param name="xCount">每行列数。</param>
        /// <param name="options">布局约束。</param>
        /// <returns>处理当前事件后的选中项索引。</returns>
        int SelectionGrid(int selected, string[] texts, int xCount, params GUILayoutOption[] options);

        /// <summary>
        /// 在固定矩形中绘制容器框，不参与自动布局。
        /// </summary>
        /// <param name="position">GUI 坐标矩形。</param>
        /// <param name="text">框内文本。</param>
        void Box(Rect position, string text);

        /// <summary>
        /// 创建固定宽度布局约束。
        /// </summary>
        /// <param name="width">宽度，单位为像素。</param>
        /// <returns>可传给 GUILayout 方法的约束。</returns>
        GUILayoutOption Width(float width);

        /// <summary>
        /// 创建最小宽度布局约束。
        /// </summary>
        /// <param name="width">最小宽度，单位为像素。</param>
        /// <returns>可传给 GUILayout 方法的约束。</returns>
        GUILayoutOption MinWidth(float width);

        /// <summary>
        /// 创建是否占用额外水平空间的布局约束。
        /// </summary>
        /// <param name="expand">是否扩展宽度。</param>
        /// <returns>可传给 GUILayout 方法的约束。</returns>
        GUILayoutOption ExpandWidth(bool expand);

        /// <summary>
        /// 创建 GUI 坐标矩形。
        /// </summary>
        /// <param name="x">左上角横坐标。</param>
        /// <param name="y">左上角纵坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <returns>包含指定位置和尺寸的矩形。</returns>
        Rect GetRect(float x, float y, float width, float height);

        /// <summary>
        /// 创建 RGBA 颜色值。
        /// </summary>
        /// <param name="r">红色分量。</param>
        /// <param name="g">绿色分量。</param>
        /// <param name="b">蓝色分量。</param>
        /// <param name="a">透明度分量。</param>
        /// <returns>颜色值。</returns>
        Color GetColor(float r, float g, float b, float a);

        /// <summary>
        /// 创建只含文本的 GUI 内容。
        /// </summary>
        /// <param name="content">显示文本。</param>
        /// <returns>新的 GUI 内容对象。</returns>
        GUIContent GetContent(string content);

        /// <summary>
        /// 创建包含文本和悬停提示的 GUI 内容。
        /// </summary>
        /// <param name="content">显示文本。</param>
        /// <param name="tooltip">悬停提示。</param>
        /// <returns>新的 GUI 内容对象。</returns>
        GUIContent GetContent(string content, string tooltip);
    }
}
