using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HEntrySpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI.Bindings
{
    /// <summary>
    /// 将实现 <see cref="IEntryMultiple"/> 的配置项适配为多元素 GUI 绑定：
    /// 单值编辑行为全部继承 <see cref="EntryBinding"/>，在此之上透出元素数量与两级说明，
    /// 供组合编辑器绘制带独立提示的元素槽位。由配置绑定工厂在条目实现多元素契约时选用。
    /// </summary>
    internal sealed class EntryMultipleBinding : EntryBinding, IEntryMultipleBinding
    {
        /// <summary>获取被适配的多元素配置项视图。</summary>
        public IEntryMultiple EntryMultiple { get; }

        /// <inheritdoc/>
        public int Count => EntryMultiple.Count;

        /// <inheritdoc/>
        public Translator BaseDescription => EntryMultiple.BaseDescription;

        /// <summary>
        /// 以 <c>new</c> 隐藏基类属性，改为返回未拼接分元素文本的 <see cref="BaseDescription"/>。
        /// 底层配置项的说明是“整体 + 值1 + 值2”逐行拼接文本（同时写入配置文件注释），
        /// 若继续透传，条目行的名称提示会与各槽位控件的悬停提示重复展示同一批内容。
        /// 本类在基类列表重新声明了派生自 <see cref="IEntryBinding"/> 的 <see cref="IEntryMultipleBinding"/>，
        /// 接口映射因此从最派生类开始查找，经接口访问时命中本属性；
        /// 若移除该接口声明，接口调用会回落到基类的透传实现。
        /// </summary>
        new public Translator Description => BaseDescription;

        /// <inheritdoc/>
        public Translator[] ValueDescription => EntryMultiple.ValueDescription;

        /// <summary>
        /// 创建多元素配置项绑定。
        /// </summary>
        /// <param name="classType">声明配置字段及其 GUI 特性的配置管理器类型，用于解析元数据。</param>
        /// <param name="entry">
        /// 要适配的多元素配置项；必须同时实现 <see cref="IConfigEntry"/>，
        /// 否则会以 null 传入基类构造函数并抛出 <see cref="ArgumentNullException"/>。
        /// </param>
        public EntryMultipleBinding(Type classType, IEntryMultiple entry)
            : base(classType, entry as IConfigEntry)
        {
            EntryMultiple = entry;
        }
    }
}
