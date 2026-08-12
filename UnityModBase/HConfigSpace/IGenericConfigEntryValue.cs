namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 标记编码文本自身会占用顶层分隔结构的泛型配置值适配器。
    /// 双元素配置使用无外层定界符的逗号分隔格式；若其元素再次采用同一格式，内外层逗号将无法区分，
    /// 因此 <see cref="ConfigService"/> 通过本标记在绑定阶段拒绝直接嵌套。
    /// </summary>
    /// <remarks>
    /// 本接口仅用于类型分类，不定义额外的编解码行为。只有确实存在上述嵌套边界歧义的
    /// <see cref="IConfigEntryValue"/> 实现才应使用该标记。
    /// </remarks>
    internal interface IGenericConfigEntryValue
    {
    }
}
