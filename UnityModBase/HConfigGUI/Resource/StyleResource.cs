using UnityEngine;
using UnityModBase.HGuiSpace;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Resource
{
    /// <summary>
    /// 配置界面的 IMGUI 样式工厂。
    /// 样式按需创建并缓存，避免在每帧 OnGUI 中重复分配 GUIStyle 和纹理对象。
    /// 本类不提供显式释放入口，宿主应在窗口生命周期内复用实例，而不是逐帧创建。
    /// </summary>
    public class StyleResource : IStyleResource
    {
        /// <inheritdoc/>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建样式资源。样式和背景纹理要到对应属性首次访问时才会分配。
        /// </summary>
        /// <param name="unityGui">作为基础皮肤和绘制接口的 IMGUI 提供器。</param>
        public StyleResource(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        // 缓存属于当前资源实例，不随皮肤或屏幕状态自动失效；宿主应在其生命周期内复用同一实例。
        private GUIStyle _popupTitleStyle;
        private GUIStyle _sidebarEntryStyle;
        private GUIStyle _sidebarSelectedEntryStyle;
        private GUIStyle _tableTitleStyle;
        private GUIStyle _recordingHotkeyLabelStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _sliderThumbStyle;
        private GUIStyle _toastStyle;
        private GUIStyle _tooltipStyle;

        /// <summary>
        /// 获取模态弹窗标题样式。
        /// </summary>
        public GUIStyle PopupTitleStyle => _popupTitleStyle ?? (_popupTitleStyle = CreatePopupTitleStyle());
        /// <summary>
        /// 获取未选中侧栏分组按钮样式。
        /// </summary>
        public GUIStyle SidebarEntryStyle => _sidebarEntryStyle ?? (_sidebarEntryStyle = CreateSidebarEntryStyle());
        /// <summary>
        /// 获取当前选中侧栏分组按钮样式。
        /// </summary>
        public GUIStyle SidebarSelectedEntryStyle => _sidebarSelectedEntryStyle ?? (_sidebarSelectedEntryStyle = CreateSidebarSelectedEntryStyle());
        /// <summary>
        /// 获取配置分组标题样式。
        /// </summary>
        public GUIStyle TableTitleStyle => _tableTitleStyle ?? (_tableTitleStyle = CreateTableTitleStyle());
        /// <summary>
        /// 获取热键录制预览文本样式。
        /// </summary>
        public GUIStyle RecordingHotkeyLabelStyle => _recordingHotkeyLabelStyle ?? (_recordingHotkeyLabelStyle = CreateRecordingHotkeyLabelStyle());
        /// <summary>
        /// 获取水平滑条轨道样式。
        /// </summary>
        public GUIStyle SliderStyle => _sliderStyle ?? (_sliderStyle = CreateSliderStyle());
        /// <summary>
        /// 获取水平滑条滑块样式。
        /// </summary>
        public GUIStyle SliderThumbStyle => _sliderThumbStyle ?? (_sliderThumbStyle = CreateSliderThumbStyle());
        /// <inheritdoc/>
        public GUIStyle ToastStyle => _toastStyle ?? (_toastStyle = CreateToastStyle());
        /// <inheritdoc/>
        public GUIStyle TooltipStyle => _tooltipStyle ?? (_tooltipStyle = CreateTooltipStyle());

        private GUIStyle CreatePopupTitleStyle()
        {
            return new GUIStyle(UnityGui.LabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
        }

        private GUIStyle CreateSidebarEntryStyle()
        {
            return new GUIStyle(UnityGui.ButtonStyle)
            {
                fontStyle = FontStyle.Normal,
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 40f,
            };
        }

        private GUIStyle CreateSidebarSelectedEntryStyle()
        {
            return new GUIStyle(UnityGui.ButtonStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 40f,
            };
        }

        private GUIStyle CreateTableTitleStyle()
        {
            return new GUIStyle(UnityGui.LabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
        }

        private GUIStyle CreateRecordingHotkeyLabelStyle()
        {
            return new GUIStyle(UnityGui.LabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.BoldAndItalic,
                fontSize = 22,
            };
        }

        private GUIStyle CreateSliderStyle()
        {
            var baseStyle = UnityGui.HorizontalSliderStyle;
            return new GUIStyle(baseStyle)
            {
                margin = new RectOffset(baseStyle.margin.left, baseStyle.margin.right, 6, 6),
                fixedHeight = 18f
            };
        }

        private GUIStyle CreateSliderThumbStyle()
        {
            return new GUIStyle(UnityGui.HorizontalSliderThumbStyle)
            {
                margin = new RectOffset(0, 0, 2, 0),
                fixedWidth = 17f,
                fixedHeight = 13f
            };
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
