using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 运行时配置表集合。
    /// 该模型保存已绑定的强类型配置表，面向业务代码和 GUI 使用；文件级结构由 <see cref="ConfigFileSheet"/> 维护。
    /// 集合不提供并发保护，并假定键为字符串、值为 <see cref="ConfigTable"/>；通过公开字典写入其他类型会使强类型视图在枚举时转换失败。
    /// </summary>
    public class ConfigSheet : IEnumerable<KeyValuePair<string, ConfigTable>>
    {
        /// <summary>
        /// 表集合，使用有序字典保持绑定顺序，便于 GUI 和其他运行时消费者获得稳定顺序。
        /// 返回的是可变字典，直接修改会绕过本类的强类型接口。
        /// </summary>
        public OrderedDictionary Sheet { get; private set; }

        /// <summary>
        /// 按插入顺序枚举表键名。
        /// </summary>
        public IEnumerable<string> Keys
        {
            get
            {
                foreach (DictionaryEntry entry in Sheet)
                {
                    yield return (string)entry.Key;
                }
            }
        }

        /// <summary>
        /// 按插入顺序枚举运行时配置表。
        /// </summary>
        public IEnumerable<ConfigTable> Values
        {
            get
            {
                foreach (DictionaryEntry entry in Sheet)
                {
                    yield return (ConfigTable)entry.Value;
                }
            }
        }

        /// <summary>
        /// 当前运行时配置表数量。
        /// </summary>
        public int Count => Sheet.Count;

        /// <summary>
        /// 创建空的有序运行时配置表集合。
        /// </summary>
        public ConfigSheet()
        {
            Sheet = new OrderedDictionary();
        }

        /// <summary>
        /// 追加运行时配置表。
        /// 此方法沿用 <see cref="OrderedDictionary.Add(object, object)"/> 的约束：键重复时抛出异常，表值允许为 <c>null</c>。
        /// </summary>
        /// <param name="tableKey">用于索引表的键。</param>
        /// <param name="table">运行时配置表。</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="tableKey"/> 为 <c>null</c>。</exception>
        /// <exception cref="System.ArgumentException"><paramref name="tableKey"/> 已存在。</exception>
        public void Add(string tableKey, ConfigTable table)
        {
            Sheet.Add(tableKey, table);
        }

        /// <summary>
        /// 判断是否已登记指定表键名。
        /// </summary>
        /// <param name="tableKey">待查询的表键名。</param>
        /// <returns>字典中是否存在该键；即使对应值为 <c>null</c> 也返回 <c>true</c>。</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="tableKey"/> 为 <c>null</c>。</exception>
        public bool Contains(string tableKey)
        {
            return Sheet.Contains(tableKey);
        }

        /// <summary>
        /// 获取指定键对应的运行时配置表；键不存在时返回 <c>null</c>。
        /// </summary>
        /// <param name="key">待查询的表键名。</param>
        public ConfigTable this[string key]
        {
            get
            {
                return (ConfigTable)Sheet[key];
            }
        }

        /// <summary>
        /// 按插入顺序枚举表键和运行时表。
        /// </summary>
        /// <returns>强类型键值对枚举器。</returns>
        public IEnumerator<KeyValuePair<string, ConfigTable>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in Sheet)
            {
                yield return new KeyValuePair<string, ConfigTable>((string)entry.Key, (ConfigTable)entry.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
