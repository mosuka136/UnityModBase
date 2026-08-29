using System;
using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    /// <summary>
    /// 自定义配置值类型与配置文件单值文本之间的转换契约。
    /// </summary>
    /// <remarks>
    /// 实现该接口后，类型即可作为 <see cref="ConfigEntry{T}"/> 的泛型参数，由配置框架统一负责文本编解码与等值判断，
    /// 业务代码无需再为每种自定义值类型单独编写序列化与比较逻辑。接口只描述单值的文本格式约定，
    /// 实现不应解析键值行、表结构或执行文件 IO。
    /// <para>
    /// <see cref="Encode"/> 以当前实例为输入；而 <see cref="Decode"/> 与 <see cref="EncodeValueType"/>
    /// 在框架侧通过 <see cref="Activator.CreateInstance(Type)"/> 创建临时实例调用，因此实现类型必须提供可访问的无参构造函数。
    /// </para>
    /// <para>
    /// <see cref="IEntryValue.Equals(IEntryValue)"/> 用于让 <see cref="ConfigEntry{T}"/> 判断“值未变化”，
    /// 以便在重载或赋值等价值时跳过写入与事件发布。实现应进行基于内容的比较（而非引用比较），
    /// 且对 <c>null</c> 或不同类型安全返回 <c>false</c>。
    /// </para>
    /// </remarks>
    public interface IConfigEntryValue : IEntryValue
    {
        /// <summary>
        /// 将当前实例编码为可放在键值行等号右侧的单个值文本。
        /// </summary>
        /// <returns>编码文本；无法表示当前状态时返回带错误的失败结果。</returns>
        ConfigFileResult<string> Encode();

        /// <summary>
        /// 从配置文件中的单个值文本创建配置值。
        /// </summary>
        /// <param name="content">已去除首尾空白的等号右侧文本；外层不会替实现移除引号或解析转义。</param>
        /// <returns>类型应与实现类型兼容的配置值；格式非法时返回失败结果。</returns>
        /// <remarks>
        /// 框架通过无参构造函数创建临时实例后调用本方法，返回值通常应是新建的值对象，而非修改临时实例后返回自身。
        /// </remarks>
        ConfigFileResult<object> Decode(string content);

        /// <summary>
        /// 生成写入配置文件注释的类型说明，不参与实际解码校验。
        /// </summary>
        /// <returns>面向人工编辑者的稳定类型说明。</returns>
        ConfigFileResult<string> EncodeValueType();
    }
}
