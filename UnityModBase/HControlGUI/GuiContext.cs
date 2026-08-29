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

        /// <summary>创建包裹指定实时控制服务的 GUI 上下文。</summary>
        /// <param name="controlService">被投影的非 null 实时控制服务。</param>
        internal GuiContext(ControlService controlService)
        {
            ControlService = controlService ?? throw new ArgumentNullException(nameof(controlService));
        }

        /// <summary>
        /// 设置结构变化处理器；重复调用会先替换旧处理器，保证每个上下文至多挂接一个。
        /// </summary>
        /// <param name="handler">控制模型结构变化时执行的通知回调。</param>
        internal void SubscribeStructureChanged(Action handler)
        {
            UnsubscribeStructureChanged();
            _structureChangedHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            ControlService.OnStructureChanged += _structureChangedHandler;
        }

        /// <summary>解除当前结构变化订阅；未订阅时为空操作。</summary>
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
