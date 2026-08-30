using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HLogGUI.Resource
{
    /// <summary>
    /// 日志界面的 IMGUI 样式工厂。
    /// 各样式属性按需创建并在资源实例内缓存，避免逐帧分配；缓存不会随 GUI 皮肤变化自动失效。
    /// 本类不提供显式释放入口，宿主应在窗口生命周期内复用实例，而不是逐帧创建。
    /// </summary>
    public sealed class StyleResource : IStyleResource
    {
        /// <inheritdoc/>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建日志样式资源。宿主应在窗口生命周期内复用实例，使惰性缓存生效。
        /// </summary>
        /// <param name="unityGui">作为基础皮肤和绘制接口的 IMGUI 提供器。</param>
        public StyleResource(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        // 列样式由表头和单元格共享；调用方若临时修改可变 GUIStyle 属性，必须在本次绘制后恢复。
        private GUIStyle _columnVisibilityToggleStyle;
        private GUIStyle _columnLogLevelToggleStyle;
        private GUIStyle _idButtonStyle;
        private GUIStyle _timestampButtonStyle;
        private GUIStyle _threadIdButtonStyle;
        private GUIStyle _frameButtonStyle;
        private GUIStyle _sceneButtonStyle;
        private GUIStyle _levelButtonStyle;
        private GUIStyle _messageButtonStyle;
        private GUIStyle _fileButtonStyle;
        private GUIStyle _lineButtonStyle;
        private GUIStyle _memberButtonStyle;
        private GUIStyle _exceptionButtonStyle;
        private GUIStyle _lastRepeatTimeButtonStyle;
        private GUIStyle _repeatCountButtonStyle;
        private GUIStyle _miscButtonStyle;
        private GUIStyle _toastStyle;
        private GUIStyle _tooltipStyle;

        /// <summary>获取列可见性开关样式。</summary>
        public GUIStyle ColumnVisibilityToggleStyle => _columnVisibilityToggleStyle ?? (_columnVisibilityToggleStyle = CreateColumnVisibilityToggleStyle());
        /// <summary>获取最低日志等级开关样式。</summary>
        public GUIStyle ColumnLogLevelToggleStyle => _columnLogLevelToggleStyle ?? (_columnLogLevelToggleStyle = CreateColumnLogLevelToggleStyle());
        /// <summary>获取日志 ID 列样式。</summary>
        public GUIStyle IdButtonStyle => _idButtonStyle ?? (_idButtonStyle = CreateIdButtonStyle());
        /// <summary>获取首次记录时间列样式。</summary>
        public GUIStyle TimestampButtonStyle => _timestampButtonStyle ?? (_timestampButtonStyle = CreateTimestampButtonStyle());
        /// <summary>获取线程 ID 列样式。</summary>
        public GUIStyle ThreadIdButtonStyle => _threadIdButtonStyle ?? (_threadIdButtonStyle = CreateThreadIdButtonStyle());
        /// <summary>获取 Unity 帧编号列样式。</summary>
        public GUIStyle FrameButtonStyle => _frameButtonStyle ?? (_frameButtonStyle = CreateFrameButtonStyle());
        /// <summary>获取场景名称列样式。</summary>
        public GUIStyle SceneButtonStyle => _sceneButtonStyle ?? (_sceneButtonStyle = CreateSceneButtonStyle());
        /// <summary>获取日志等级列样式。</summary>
        public GUIStyle LevelButtonStyle => _levelButtonStyle ?? (_levelButtonStyle = CreateLevelButtonStyle());
        /// <summary>获取日志消息列样式。</summary>
        public GUIStyle MessageButtonStyle => _messageButtonStyle ?? (_messageButtonStyle = CreateMessageButtonStyle());
        /// <summary>获取源文件列样式。</summary>
        public GUIStyle FileButtonStyle => _fileButtonStyle ?? (_fileButtonStyle = CreateFileButtonStyle());
        /// <summary>获取源文件行号列样式。</summary>
        public GUIStyle LineButtonStyle => _lineButtonStyle ?? (_lineButtonStyle = CreateLineButtonStyle());
        /// <summary>获取调用方成员列样式。</summary>
        public GUIStyle MemberButtonStyle => _memberButtonStyle ?? (_memberButtonStyle = CreateMemberButtonStyle());
        /// <summary>获取异常列样式。</summary>
        public GUIStyle ExceptionButtonStyle => _exceptionButtonStyle ?? (_exceptionButtonStyle = CreateExceptionButtonStyle());
        /// <summary>获取最后重复时间列样式。</summary>
        public GUIStyle LastRepeatTimeButtonStyle => _lastRepeatTimeButtonStyle ?? (_lastRepeatTimeButtonStyle = CreateLastRepeatTimeButtonStyle());
        /// <summary>获取累计出现次数列样式。</summary>
        public GUIStyle RepeatCountButtonStyle => _repeatCountButtonStyle ?? (_repeatCountButtonStyle = CreateRepeatCountButtonStyle());
        /// <summary>获取逐行操作列样式。</summary>
        public GUIStyle MiscButtonStyle => _miscButtonStyle ?? (_miscButtonStyle = CreateMiscButtonStyle());
        /// <inheritdoc/>
        public GUIStyle ToastStyle => _toastStyle ?? (_toastStyle = CreateToastStyle());
        /// <inheritdoc/>
        public GUIStyle TooltipStyle => _tooltipStyle ?? (_tooltipStyle = CreateTooltipStyle());

        /// <summary>
        /// 创建新的列可见性开关样式。通常应通过 <see cref="ColumnVisibilityToggleStyle"/> 使用缓存实例。
        /// </summary>
        /// <returns>基于当前 GUI 皮肤的新样式实例。</returns>
        public GUIStyle CreateColumnVisibilityToggleStyle()
        {
            return new GUIStyle(UnityGui.ToggleStyle)
            {
                fontSize = 14,
            };
        }

        /// <summary>
        /// 创建新的日志等级开关样式。通常应通过 <see cref="ColumnLogLevelToggleStyle"/> 使用缓存实例。
        /// </summary>
        /// <returns>基于当前 GUI 皮肤的新样式实例。</returns>
        public GUIStyle CreateColumnLogLevelToggleStyle()
        {
            return new GUIStyle(UnityGui.ToggleStyle)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                stretchWidth = false
            };
        }

        /// <summary>
        /// 创建新的 ID 列样式。通常应通过 <see cref="IdButtonStyle"/> 使用缓存实例。
        /// </summary>
        /// <returns>具有 ID 列对齐和颜色设置的新样式实例。</returns>
        public GUIStyle CreateIdButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.82f, 0.82f, 0.78f));
        }

        private GUIStyle CreateTimestampButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.38f, 0.76f, 0.94f));
        }

        private GUIStyle CreateThreadIdButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.58f, 0.68f, 1f));
        }

        private GUIStyle CreateFrameButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.42f, 0.86f, 0.48f));
        }

        private GUIStyle CreateSceneButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleLeft, new Color(0.98f, 0.72f, 0.34f));
        }

        private GUIStyle CreateLevelButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(1f, 0.46f, 0.42f));
        }

        private GUIStyle CreateMessageButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleLeft, new Color(0.9f, 0.88f, 0.78f));
        }

        private GUIStyle CreateFileButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleLeft, new Color(0.36f, 0.86f, 0.8f));
        }

        private GUIStyle CreateLineButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.74f, 0.88f, 0.36f));
        }

        private GUIStyle CreateMemberButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleLeft, new Color(0.96f, 0.56f, 0.9f));
        }

        private GUIStyle CreateExceptionButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleLeft, new Color(1f, 0.36f, 0.58f));
        }

        private GUIStyle CreateLastRepeatTimeButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.68f, 0.58f, 1f));
        }

        private GUIStyle CreateRepeatCountButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.3f));
        }

        private GUIStyle CreateMiscButtonStyle()
        {
            return CreateColumnButtonStyle(TextAnchor.MiddleCenter, new Color(0.7f, 0.84f, 0.92f));
        }

        private GUIStyle CreateColumnButtonStyle(TextAnchor alignment, Color textColor)
        {
            var style = new GUIStyle(UnityGui.ButtonStyle)
            {
                fontStyle = FontStyle.Normal,
                fontSize = 14,
                alignment = alignment,
                clipping = TextClipping.Clip,
                wordWrap = false,
                stretchWidth = false
            };

            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            return style;
        }

        private Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            // 样式背景纹理只服务于运行时 GUI，不应随场景保存或显示在层级中。
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private GUIStyle CreateToastStyle()
        {
            var style = new GUIStyle(UnityGui.BoxStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 6, 6)
            };
            style.normal.background = CreateSolidTexture(new Color(0f, 0.3f, 0.25f, 1f));
            style.normal.textColor = Color.white;
            return style;
        }

        private GUIStyle CreateTooltipStyle()
        {
            var style = new GUIStyle(UnityGui.BoxStyle)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4)
            };
            style.normal.background = CreateSolidTexture(new Color(0.15f, 0.15f, 0.15f, 0.95f));
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
