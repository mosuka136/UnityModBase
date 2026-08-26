namespace UnityModBase.HClassAttribute
{
    /// <summary>
    /// 配置属性 GUI 元数据声明特性的标记接口。
    /// 仅用于让 <see cref="T:UnityModBase.HConfigGUI.UiMetadataHelper"/> 通过一次类型筛选
    /// 定位属性上的 GUI 声明特性，而不逐个枚举具体特性类型；接口本身不携带契约成员。
    /// </summary>
    /// <remarks>
    /// 实现该标记不足以让特性生效：元数据解析当前只识别具体类型
    /// （<see cref="ConfigSliderAttribute"/> 与组合标记 <see cref="ConfigGuiAttribute"/>），
    /// 未被识别的实现会降级为“无元数据”而非报错。新增可解析的声明特性时，
    /// 除实现本接口外还需在解析侧补充对应转换分支。
    /// 实现类型应声明 <c>AttributeTargets.Property</c>，与配置属性上的声明位置保持一致。
    /// </remarks>
    public interface IConfigGuiAttribute
    {
    }
}
