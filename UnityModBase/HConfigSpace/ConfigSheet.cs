using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 运行时配置表集合，按绑定顺序保存已声明的 <see cref="ConfigTable"/>，面向业务代码和 GUI 使用。
    /// 文件级结构由 <see cref="ConfigFileSheet"/> 维护；表的一致登记（含文件模型同步）由 <see cref="ConfigService"/> 负责。
    /// 集合不提供并发保护，登记和遍历应由调用方串行化。
    /// </summary>
    public sealed class ConfigSheet : SheetBase<string, ConfigTable>
    {
    }
}
