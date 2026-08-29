using System;
using UnityModBase.HEntrySpace;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 运行时配置表，按绑定顺序保存一组 <see cref="IConfigEntry"/>，供 GUI 按表展示并供业务代码遍历。
    /// 条目的增删和归属校验由基类 <see cref="TableBase{T}"/> 负责；本类型不持有文件表模型，
    /// 文件级结构与展示元数据由 <see cref="ConfigService.CreateTable"/> 在创建运行时表时写入 <see cref="ConfigFileTable"/>，
    /// 配置重载不会把运行时表名/说明同步到新读取的文件表。
    /// 该类型不提供并发保护，绑定和遍历应由调用方串行化。
    /// </summary>
    public sealed class ConfigTable : TableBase<IConfigEntry>
    {
        /// <summary>
        /// 创建运行时配置表；展示元数据向文件表的同步由 <see cref="ConfigService.CreateTable"/> 在调用前后完成，构造函数本身不触碰文件模型。
        /// </summary>
        /// <param name="key">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="name">展示名称；为 <c>null</c> 时使用空翻译对象。</param>
        /// <param name="description">展示说明；为 <c>null</c> 时使用空翻译对象。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 不符合表名语法。</exception>
        internal ConfigTable(string key, Translator name, Translator description) : base(key, name, description)
        {
        }

        /// <summary>
        /// 判断配置项是否已按“表键名 + 配置项键名”存在于本表中；不做引用比较，<c>null</c> 输入视为不存在。
        /// 线性扫描，供 <see cref="TableBase{T}.Add"/> 与 <see cref="ConfigService"/> 的绑定入口做重复键检测。
        /// </summary>
        /// <param name="entry">待查询的运行时配置项，可为 <c>null</c>。</param>
        /// <returns>存在同键配置项时返回 <c>true</c>。</returns>
        public bool Contains(IConfigEntry entry)
        {
            return entry != null && Key == entry.TableKey && base.Contains(entry.Key);
        }
    }
}
