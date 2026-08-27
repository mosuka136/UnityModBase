using System;
using UnityModBase.HGuiSpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 在通用可编辑 GUI 状态上补充配置热键编辑所需的模态弹窗状态。
    /// </summary>
    public class GuiContext : EditableGuiContext
    {
        /// <summary>获取供宿主表达无有效用户时使用的无效上下文。</summary>
        public static readonly GuiContext InvalidGuiContext = new GuiContext(false);

        /// <summary>获取配置热键编辑使用的模态弹窗状态。</summary>
        public PopupState Popup { get; } = new PopupState();

        /// <summary>创建有效的配置 GUI 上下文。</summary>
        public GuiContext() : base()
        {
        }

        private GuiContext(bool isValid) : base(isValid)
        {
        }

        /// <summary>
        /// 描述由具体值编辑器提供内容的模态弹窗。
        /// </summary>
        public class PopupState
        {
            /// <summary>获取或设置弹窗是否打开。</summary>
            public bool IsOpen { get; set; }

            /// <summary>获取或设置弹窗标题。</summary>
            public Translator Title { get; set; }

            /// <summary>获取或设置弹窗内容绘制回调。</summary>
            public Action DrawAction { get; set; }

            /// <summary>获取或设置弹窗关闭回调。</summary>
            public Action CloseAction { get; set; }
        }
    }
}
