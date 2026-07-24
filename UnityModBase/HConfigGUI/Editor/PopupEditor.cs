using System;
using UnityEngine;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor
{
    /// <summary>
    /// 绘制覆盖屏幕的配置模态窗口，并执行当前 <see cref="GuiContext.PopupState"/> 提供的主体和关闭回调。
    /// 本类只承载弹窗外壳与位置，不拥有弹窗业务状态。
    /// </summary>
    public class PopupEditor
    {
        /// <summary>
        /// 供 IMGUI 区分弹窗的运行时标识；不作为持久标识，哈希碰撞未额外处理。
        /// </summary>
        public readonly int PopupID = Guid.NewGuid().GetHashCode();

        /// <summary>
        /// 获取弹窗在屏幕坐标中的当前位置和尺寸。
        /// </summary>
        public Rect PopupRect { get; private set; }
        /// <summary>
        /// 获取弹窗和遮罩绘制所用的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }
        /// <summary>
        /// 获取弹窗标题样式资源。
        /// </summary>
        public StyleResource StyleProvider { get; }

        /// <summary>
        /// 创建弹窗编辑器，并按当前屏幕尺寸计算初始居中矩形。
        /// 后续屏幕尺寸变化不会自动重新居中。
        /// </summary>
        /// <param name="unityGui">提供屏幕尺寸和模态窗口绘制的 IMGUI 提供器。</param>
        /// <param name="styleProvider">提供弹窗标题样式的资源。</param>
        public PopupEditor(IUnityGuiProvider unityGui, StyleResource styleProvider)
        {
            UnityGui = unityGui;
            StyleProvider = styleProvider;

            float width = UnityGui.ScreenWidth * 0.25f;
            float height = UnityGui.ScreenHeight * 0.15f;
            PopupRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);
        }

        /// <summary>
        /// 绘制半透明全屏遮罩及模态窗口，并保存 Unity 返回的窗口位置。
        /// null 或无效上下文会被忽略。
        /// </summary>
        /// <param name="context">提供弹窗标题、主体和关闭回调的配置 GUI 上下文。</param>
        /// <remarks>
        /// 遮罩通过临时改写共享的 <see cref="IUnityGuiProvider.Color"/> 绘制；颜色恢复不在异常保护块内，
        /// 因此遮罩绘制器抛出时，调用方应视当前帧 GUI 颜色状态为不可继续复用。
        /// 主体和关闭回调中的异常会继续传播，但弹窗内部的垂直布局仍会闭合。
        /// 关闭回调先于 <see cref="GuiContext.PopupState.IsOpen"/> 清除；回调抛出时不会执行后续关闭赋值，
        /// 最终弹窗状态取决于回调在异常前是否已经修改上下文。
        /// </remarks>
        public void DrawPopup(GuiContext context)
        {
            if (context == null || !context.IsValid)
                return;

            var color = UnityGui.Color;
            UnityGui.Color = new Color(0f, 0f, 0f, 0.55f);
            UnityGui.Box(new Rect(0f, 0f, UnityGui.ScreenWidth, UnityGui.ScreenHeight), string.Empty);
            UnityGui.Color = color;

            PopupRect = UnityGui.ModalWindow(PopupID, PopupRect, id => DrawPopupWindow(id, context), string.Empty, UnityGui.BoxStyle);
        }

        private void DrawPopupWindow(int id, GuiContext context)
        {
            var title = context.Popup.Title;
            var drawAction = context.Popup.DrawAction;
            var closeAction = context.Popup.CloseAction;

            UnityGui.BeginVertical();
            try
            {
                UnityGui.Label(title, StyleProvider.PopupTitleStyle, UnityGui.ExpandWidth(true));

                drawAction?.Invoke();

                if (UnityGui.Button(TranslatorResource.Close, UnityGui.ExpandWidth(true)))
                {
                    closeAction?.Invoke();
                    context.Popup.IsOpen = false;
                }
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }
    }
}
