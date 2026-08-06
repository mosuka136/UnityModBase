using System;
using System.Collections;
using System.Collections.Generic;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 运行时配置表。
    /// 它把文件表 <see cref="ConfigFileTable"/> 与一组强类型配置项关联起来，供 GUI 按表展示并供业务代码按声明顺序遍历。
    /// 构造时会把名称与说明写入传入的文件表模型；直接替换本类的元数据属性不会自动同步到对应文件表，
    /// 配置重载也不会再把运行时表名/说明同步到新读取的文件表。
    /// 该类型暴露可变列表且不提供并发保护，绑定和遍历应由调用方串行化。
    /// </summary>
    public class ConfigTable : IEnumerable<IConfigEntry>
    {
        /// <summary>
        /// 运行时表键名，对应配置文件中的表头。
        /// 只有构造函数会校验该值，后续赋值不会同步或校验 <see cref="ConfigSheet"/> 的索引键或表内配置项的所属表名。
        /// 配置服务重载时会用当前值查找候选文件表，因此完成绑定后修改该值可能导致整批重载失败。
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 供运行时消费者展示的多语言表名。
        /// </summary>
        public Translator Name { get; set; }

        /// <summary>
        /// 供运行时消费者展示的多语言表说明。
        /// </summary>
        public Translator Description { get; set; }

        /// <summary>
        /// 表内已绑定配置项，顺序与绑定顺序一致。
        /// 返回的是可变列表；直接修改会绕过 <see cref="Add"/> 对 <c>null</c> 的忽略规则，
        /// 也不会创建文件项或建立 <see cref="ConfigService"/> 的自动保存订阅。
        /// 自定义 <see cref="IConfigEntry"/> 实现还需正确实现事务协议方法，否则会使 <see cref="ConfigService.Reload"/> 预检或提交失败。
        /// </summary>
        public List<IConfigEntry> Table { get; private set; }

        /// <summary>
        /// 创建运行时配置表，并把展示元数据写入对应的文件表模型。
        /// </summary>
        /// <param name="key">表键名，只允许 Unicode 字母、数字和下划线。</param>
        /// <param name="table">对应的文件表模型。</param>
        /// <param name="name">展示名称；为 <c>null</c> 时使用空翻译对象。</param>
        /// <param name="description">展示说明；为 <c>null</c> 时使用空翻译对象。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 不符合表名语法。</exception>
        /// <exception cref="NullReferenceException"><paramref name="table"/> 为 <c>null</c>。</exception>
        public ConfigTable(string key, ConfigFileTable table, Translator name, Translator description)
        {
            if (!ConfigFileTable.IsValidTableName(key))
                throw new ArgumentException($"Invalid config table name: {key}.", nameof(key));
            Key = key;
            Name = name ?? new Translator(string.Empty);
            Description = description ?? new Translator(string.Empty);
            Table = new List<IConfigEntry>();

            table.Name = Name;
            table.Description = Description;
        }

        /// <summary>
        /// 按绑定顺序追加配置项；<c>null</c> 输入会被忽略。
        /// 该低级入口只修改运行时列表，不负责文件模型、自动保存订阅或重载能力；正常声明配置项应使用 <see cref="ConfigService.Bind{T}"/>。
        /// </summary>
        /// <param name="entry">待追加的运行时配置项。</param>
        public void Add(IConfigEntry entry)
        {
            if (entry == null)
                return;
            Table.Add(entry);
        }

        /// <summary>
        /// 按绑定顺序枚举运行时配置项。
        /// </summary>
        /// <returns>底层可变列表的枚举器。</returns>
        public IEnumerator<IConfigEntry> GetEnumerator()
        {
            return Table.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Table.GetEnumerator();
        }
    }
}
