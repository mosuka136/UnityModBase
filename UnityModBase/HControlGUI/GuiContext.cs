using System;
using UnityModBase.HControlSpace;
using UnityModBase.HGuiSpace;

namespace UnityModBase.HControlGUI
{
    /// <summary>
    /// 保存单个用户的实时控制绑定树、编辑缓冲和结构变化订阅。
    /// </summary>
    public sealed class GuiContext : EditableGuiContext
    {
        private Action _structureChangedHandler;

        /// <summary>获取所投影的实时控制服务。</summary>
        public ControlService ControlService { get; }

        internal GuiContext(ControlService controlService)
        {
            ControlService = controlService ?? throw new ArgumentNullException(nameof(controlService));
        }

        internal void SubscribeStructureChanged(Action handler)
        {
            UnsubscribeStructureChanged();
            _structureChangedHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            ControlService.OnStructureChanged += _structureChangedHandler;
        }

        internal void UnsubscribeStructureChanged()
        {
            if (_structureChangedHandler == null)
                return;
            ControlService.OnStructureChanged -= _structureChangedHandler;
            _structureChangedHandler = null;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            UnsubscribeStructureChanged();
            base.Dispose();
        }
    }
}
