using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 按声明顺序保存一组实时控制项的运行时表。
    /// 条目的归属校验与重复键检测由基类 <see cref="TableBase{T}"/> 负责；
    /// 本类型只由 <see cref="ControlService.CreateTable"/> 创建，条目经 <see cref="ControlService.Bind{T}"/> 加入，
    /// 控制 GUI 通过只读视图 <see cref="TableBase{T}.Entries"/> 按声明顺序展示。
    /// 该类型不提供并发保护，绑定和遍历应由调用方串行化。
    /// </summary>
    public sealed class ControlTable : TableBase<IControlEntry>
    {
        /// <summary>
        /// 创建实时控制表；键名语法由基类校验，名称与说明的空值回退同样沿用基类约定。
        /// </summary>
        internal ControlTable(string key, Translator name, Translator description) : base(key, name, description)
        {
        }
    }
}
