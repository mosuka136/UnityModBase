using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityModBase.HTranslatorSpace
{
    /// <summary>
    /// 简单的双语文本容器。
    /// 该类型用于配置文件注释、GUI 文案和用户显示名；它只按当前默认语言返回中文或英文，
    /// 不负责资源文件加载或运行时本地化回退链。
    /// </summary>
    public sealed class Translator : IEnumerable<string>
    {
        /// <summary>
        /// 全局默认语言实际变化后同步触发；事件发送者固定为 <c>null</c>，单个订阅者异常会被忽略，不影响后续订阅者。
        /// </summary>
        public static event EventHandler<LanguageType> OnDefaultLanguageChanged;

        private static LanguageType _defaultLanguage = LanguageType.English;
        /// <summary>
        /// 当实例语言设置为 <see cref="LanguageType.Default"/> 时使用的全局语言。
        /// 重复赋相同值不会派发变更事件；<see cref="LanguageType.None"/> 和 <see cref="LanguageType.Default"/> 最终按英文解析。
        /// </summary>
        public static LanguageType DefaultLanguage
        {
            get => _defaultLanguage;
            set
            {
                if (_defaultLanguage != value)
                {
                    _defaultLanguage = value;
                    foreach (var handler in OnDefaultLanguageChanged.GetInvocationListOrEmpty())
                    {
                        try { handler?.Invoke(null, value); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>
        /// 当前实例的语言选择。<see cref="LanguageType.Default"/> 表示跟随 <see cref="DefaultLanguage"/>，
        /// <see cref="LanguageType.None"/> 直接按英文解析。
        /// </summary>
        public LanguageType LanguageType { get; set; } = LanguageType.Default;
        /// <summary>
        /// 按当前语言解析后的文本。
        /// </summary>
        public string Default
        {
            get
            {
                var language = LanguageType == LanguageType.Default ? DefaultLanguage : LanguageType;
                switch (language)
                {
                    case LanguageType.Chinese:
                        return Chinese;
                    case LanguageType.English:
                        return English;
                    case LanguageType.None:
                    case LanguageType.Default:
                    default:
                        return English;
                }
            }
        }
        /// <summary>
        /// 简体中文文本；不会执行资源查找或空值回退。
        /// </summary>
        public string Chinese { get; set; }

        /// <summary>
        /// 英文文本，也是未知或控制语言值的回退结果。
        /// </summary>
        public string English { get; set; }

        /// <summary>
        /// 创建双语文本，实例默认跟随全局语言。
        /// </summary>
        /// <param name="chinese">简体中文文本，默认空字符串。</param>
        /// <param name="english">英文及回退文本，默认空字符串。</param>
        public Translator(string chinese = "", string english = "")
        {
            Chinese = chinese;
            English = english;
        }

        /// <summary>
        /// 按实例当前语言隐式解析文本。
        /// </summary>
        /// <param name="translator">要解析的翻译容器；为 <c>null</c> 时访问会抛出 <see cref="NullReferenceException"/>。</param>
        /// <returns>按当前语言选择的文本。</returns>
        public static implicit operator string(Translator translator)
        {
            return translator.Default;
        }

        /// <summary>
        /// 返回按实例当前语言解析后的文本。
        /// </summary>
        /// <returns>按当前语言选择的文本。</returns>
        public override string ToString()
        {
            return Default;
        }

        /// <summary>
        /// 依次枚举中文和英文原始文本，不受当前语言选择影响。
        /// </summary>
        /// <returns>先中文、后英文的枚举器。</returns>
        public IEnumerator<string> GetEnumerator()
        {
            yield return Chinese;
            yield return English;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 清除全部全局语言变更订阅者，并把默认语言复位为英文。
        /// </summary>
        internal static void Dispose()
        {
            OnDefaultLanguageChanged = null;
            _defaultLanguage = LanguageType.English;
        }
    }
}
