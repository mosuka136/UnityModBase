using System;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 配置项和实时控制项共享的只读条目视图。
    /// 配置空间的 <c>IConfigEntry</c> 与控制空间的 <c>IControlEntry</c> 均派生自本接口，
    /// 使通用 GUI（HGuiSpace）能以同一契约投影两类条目，而不依赖任一具体空间。
    /// </summary>
    public interface IEntry
    {
        /// <summary>获取所属表键。</summary>
        string TableKey { get; }

        /// <summary>获取条目键。</summary>
        string Key { get; }

        /// <summary>获取显示名称。</summary>
        Translator Name { get; }

        /// <summary>获取显示说明。</summary>
        Translator Description { get; }

        /// <summary>获取声明值类型。</summary>
        Type ValueType { get; }

        /// <summary>获取当前装箱值。</summary>
        object BoxedValue { get; }
    }
}
