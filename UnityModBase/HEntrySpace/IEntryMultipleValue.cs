namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 标记由多个泛型元素组成的共享条目值。
    /// <see cref="EntryModel.IsEntryMultipleValueType(System.Type)"/> 依据本标记识别此类类型并校验其泛型元素
    /// （必须封闭、数量任意、逐个为受支持的非嵌套条目值类型）；
    /// 配置编解码则按 <c>Value1..ValueN</c> 属性约定反射读写元素，平铺为无外层定界符的逗号分隔文本。
    /// 自 <see cref="IEntryValue"/> 继承等值判断契约，实现类型需提供与元素语义一致的 <c>Equals(IEntryValue)</c>。
    /// 仅确有此元素结构、且遵守上述属性与构造约定的值类型应实现本接口。
    /// </summary>
    public interface IEntryMultipleValue : IEntryValue
    {
    }
}
