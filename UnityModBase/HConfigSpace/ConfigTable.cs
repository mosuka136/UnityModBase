using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        /// 返回的是可变列表；直接修改会绕过 <see cref="Add"/> 的归属表与重复键校验，
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
            if (!ConfigFileModel.IsValidTableKey(key))
                throw new ArgumentException($"Invalid config table name: {key}.", nameof(key));
            Key = key;
            Name = name ?? new Translator(string.Empty);
            Description = description ?? new Translator(string.Empty);
            Table = new List<IConfigEntry>();

            table.Name = Name;
            table.Description = Description;
        }

        /// <summary>
        /// 判断配置项是否已按“表键名 + 配置项键名”存在于本表中；不做引用比较，<c>null</c> 输入视为不存在。
        /// 现为线性扫描，供 <see cref="Add"/> 与 <see cref="ConfigService"/> 的绑定入口做重复键检测。
        /// </summary>
        /// <param name="entry">待查询的运行时配置项，可为 <c>null</c>。</param>
        /// <returns>存在同键配置项时返回 <c>true</c>。</returns>
        public bool Contains(IConfigEntry entry)
        {
            return entry != null && Key == entry.TableKey && Table.Select(e => e.Key).Contains(entry.Key);
        }

        /// <summary>
        /// 按绑定顺序追加配置项；输入为 <c>null</c>、配置项声明归属其他表或键名与本表已有项重复时抛出异常。
        /// 该低级入口只修改运行时列表，不负责创建文件项、订阅自动保存或验证事务重载协议；正常声明配置项应使用 <see cref="ConfigService"/> 的相应 <c>Bind</c> 重载。
        /// </summary>
        /// <param name="entry">待追加的运行时配置项，其 <c>TableKey</c> 必须等于本表键名。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <c>null</c>。</exception>
        /// <exception cref="ArgumentException"><paramref name="entry"/> 归属其他表，或其键名已存在于本表。</exception>
        public void Add(IConfigEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (Key != entry.TableKey)
                throw new ArgumentException($"Config entry {entry.Key} belongs to table {entry.TableKey}, not {Key}.", nameof(entry));
            if (Contains(entry))
                throw new ArgumentException($"Config entry {entry.Key} already exists in table {Key}.", nameof(entry));
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
