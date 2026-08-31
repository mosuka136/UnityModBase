using UnityEngine;

namespace UnityModBase.HProvider
{
    /// <summary>
    /// Unity IMGUI/GUILayout 的适配层。
    /// 配置界面通过该类型访问 GUI API，主要目的是把渲染代码与 Unity 静态 API 隔开，并统一空样式回退策略。
    /// </summary>
    public sealed class UnityGuiProvider : IUnityGuiProvider
    {
        /// <inheritdoc />
        public string Tooltip => GUI.tooltip;

        /// <inheritdoc />
        public GUIStyle LabelStyle => GUI.skin.label;

        /// <inheritdoc />
        public GUIStyle ToggleStyle => GUI.skin.toggle;

        /// <inheritdoc />
        public GUIStyle ButtonStyle => GUI.skin.button;

        /// <inheritdoc />
        public GUIStyle TextFieldStyle => GUI.skin.textField;

        /// <inheritdoc />
        public GUIStyle HorizontalSliderStyle => GUI.skin.horizontalSlider;

        /// <inheritdoc />
        public GUIStyle HorizontalSliderThumbStyle => GUI.skin.horizontalSliderThumb;

        /// <inheritdoc />
        public GUIStyle BoxStyle => GUI.skin.box;

        /// <inheritdoc />
        public Color Color
        {
            get => GUI.color;
            set => GUI.color = value;
        }
        /// <inheritdoc />
        public float ScreenWidth => Screen.width;

        /// <inheritdoc />
        public float ScreenHeight => Screen.height;

        /// <summary>
        /// 共享的 IMGUI API 适配器实例。
        /// </summary>
        public static UnityGuiProvider Instance { get; } = new UnityGuiProvider();
        private UnityGuiProvider() { }

        /// <inheritdoc />
        public Rect ModalWindow(int id, Rect clientRect, GUI.WindowFunction func, string title, GUIStyle style)
        {
            return GUI.ModalWindow(id, clientRect, func, title, style ?? GUIStyle.none);
        }

        /// <inheritdoc />
        public void BeginArea(Rect screenRect)
        {
            GUILayout.BeginArea(screenRect);
        }

        /// <inheritdoc />
        public void EndArea()
        {
            GUILayout.EndArea();
        }

        /// <inheritdoc />
        public void BeginHorizontal(params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
        }

        /// <inheritdoc />
        public void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public void EndHorizontal()
        {
            GUILayout.EndHorizontal();
        }

        /// <inheritdoc />
        public Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options)
        {
            return GUILayout.BeginScrollView(scrollPosition, options);
        }

        /// <inheritdoc />
        public void EndScrollView()
        {
            GUILayout.EndScrollView();
        }

        /// <inheritdoc />
        public void BeginVertical()
        {
            GUILayout.BeginVertical();
        }

        /// <inheritdoc />
        public void BeginVertical(GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public void EndVertical()
        {
            GUILayout.EndVertical();
        }

        /// <inheritdoc />
        public void Space(float pixels)
        {
            GUILayout.Space(pixels);
        }

        /// <inheritdoc />
        public void FlexibleSpace()
        {
            GUILayout.FlexibleSpace();
        }

        /// <inheritdoc />
        public void Label(string text, params GUILayoutOption[] options)
        {
            GUILayout.Label(text, options);
        }

        /// <inheritdoc />
        public void Label(GUIContent content, params GUILayoutOption[] options)
        {
            GUILayout.Label(content, options);
        }

        /// <inheritdoc />
        public void Label(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.Label(text, style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.Label(content, style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public void Label(Rect position, GUIContent content, GUIStyle style)
        {
            GUI.Label(position, content, style ?? GUIStyle.none);
        }

        /// <inheritdoc />
        public bool Toggle(bool value, string text, params GUILayoutOption[] options)
        {
            return GUILayout.Toggle(value, text, options);
        }

        /// <inheritdoc />
        public bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            return GUILayout.Toggle(value, text, style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options)
        {
            return GUILayout.Toggle(value, content, options);
        }

        /// <inheritdoc />
        public string TextField(string text, params GUILayoutOption[] options)
        {
            return GUILayout.TextField(text, options);
        }

        /// <inheritdoc />
        public void SetLastControlTooltip(string tooltip)
        {
            if (string.IsNullOrEmpty(tooltip))
                return;

            // IMGUI 没有给已绘制控件补设提示的 API，只能在控件矩形上覆盖一个空文本、
            // GUIStyle.none 的标签：不占布局也不绘制内容，仅让悬停命中提示；
            // 因此必须在目标控件绘制后立即调用，GetLastRect 才指向该控件。
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        /// <inheritdoc />
        public bool Button(string text, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, options);
        }

        /// <inheritdoc />
        public bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            return GUILayout.Button(content, style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public bool Button(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, style ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public float HorizontalSlider(float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb, params GUILayoutOption[] options)
        {
            return GUILayout.HorizontalSlider(value, leftValue, rightValue, slider ?? GUIStyle.none, thumb ?? GUIStyle.none, options);
        }

        /// <inheritdoc />
        public int SelectionGrid(int selected, string[] texts, int xCount, params GUILayoutOption[] options)
        {
            return GUILayout.SelectionGrid(selected, texts, xCount, options);
        }

        /// <inheritdoc />
        public void Box(Rect position, string text)
        {
            GUI.Box(position, text);
        }

        /// <inheritdoc />
        public GUILayoutOption Width(float width)
        {
            return GUILayout.Width(width);
        }

        /// <inheritdoc />
        public GUILayoutOption MinWidth(float width)
        {
            return GUILayout.MinWidth(width);
        }

        /// <inheritdoc />
        public GUILayoutOption ExpandWidth(bool expand)
        {
            return GUILayout.ExpandWidth(expand);
        }

        /// <inheritdoc />
        public Rect GetRect(float x, float y, float width, float height)
        {
            return new Rect(x, y, width, height);
        }

        /// <inheritdoc />
        public Color GetColor(float r, float g, float b, float a)
        {
            return new Color(r, g, b, a);
        }

        /// <inheritdoc />
        public GUIContent GetContent(string content)
        {
            return new GUIContent(content);
        }

        /// <inheritdoc />
        public GUIContent GetContent(string content, string tooltip)
        {
            return new GUIContent(content, tooltip);
        }
    }
}
