using UnityModBase.HTranslatorSpace;

namespace UnityModBase.BSpace
{
    /// <summary>
    /// 集中保存 UnityModBase 自身注册到公共界面时使用的可翻译文案。
    /// 本类型只声明框架内置资源，不负责选择或持久化运行时语言。
    /// </summary>
    internal static class BTranslatorResource
    {
        /// <summary>
        /// UnityModBase 用户上下文的显示名称；实际文本在界面绘制时按当前语言解析。
        /// </summary>
        public readonly static Translator UserName = new Translator("Unity 模组基础库", nameof(UnityModBase));
    }
}
