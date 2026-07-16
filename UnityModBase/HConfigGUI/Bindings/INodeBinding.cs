using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    /// <summary>
    /// 配置 GUI 树中分组和配置项共享的只读显示契约。
    /// 该接口只描述导航与展示信息，不负责读取或写入配置值。
    /// </summary>
    public interface INodeBinding
    {
        /// <summary>
        /// 获取节点在所属配置结构中的稳定键。
        /// </summary>
        string Key { get; }
        /// <summary>
        /// 获取随当前语言解析的显示名称。
        /// </summary>
        Translator Name { get; }
        /// <summary>
        /// 获取用作工具提示的说明文本。
        /// </summary>
        Translator Description { get; }
    }
}
