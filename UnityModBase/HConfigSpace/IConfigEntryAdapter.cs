namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 自定义配置值类型与配置文件单值文本之间的转换契约。
    /// 编码由当前值实例完成；解码和类型说明生成会通过反射创建临时实例，因此实现类型必须提供可访问的无参构造函数。
    /// 适配器只负责单值格式，不应解析键值行、表结构或执行文件 IO。
    /// </summary>
    public interface IConfigEntryAdapter
    {
        /// <summary>
        /// 将当前实例编码为可放在键值行等号右侧的单个值文本。
        /// </summary>
        /// <returns>编码文本；无法表示当前状态时返回带错误的失败结果。</returns>
        ConfigFileResult<string> Encode();

        /// <summary>
        /// 从配置文件中的单个值文本创建配置值。
        /// </summary>
        /// <param name="content">已去除首尾空白的等号右侧文本；外层不会替适配器移除引号或解析转义。</param>
        /// <returns>类型应与实现类型兼容的配置值；格式非法时返回失败结果。</returns>
        ConfigFileResult<object> Decode(string content);

        /// <summary>
        /// 生成写入配置文件注释的类型说明，不参与实际解码校验。
        /// </summary>
        /// <returns>面向人工编辑者的稳定类型说明。</returns>
        ConfigFileResult<string> EncodeValueType();
    }
}
