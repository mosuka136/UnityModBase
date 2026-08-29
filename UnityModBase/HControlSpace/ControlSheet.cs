using UnityModBase.HEntrySpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 按声明顺序保存实时控制表的运行时集合。
    /// 顺序即 <see cref="ControlService.CreateTable"/> 的声明顺序，控制 GUI 据此生成分组；
    /// 表的登记与结构变化通知由 <see cref="ControlService"/> 负责，本类型只提供有序存取。
    /// 集合不提供并发保护，登记和遍历应由调用方串行化。
    /// </summary>
    public sealed class ControlSheet : SheetBase<string, ControlTable>
    {
    }
}
