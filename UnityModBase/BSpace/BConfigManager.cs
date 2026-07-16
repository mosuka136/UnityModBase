using System;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.BSpace
{
    /// <summary>
    /// 声明 UnityModBase 自身使用的配置表和配置项，并把语言与配置重载热键接入运行时事件。
    /// 配置文件的创建、解析和持久化由 <see cref="ConfigService"/> 负责，本类型只维护配置项的静态引用。
    /// </summary>
    internal static class BConfigManager
    {
        // 保护静态配置引用及其事件订阅的完整生命周期，避免初始化、重载热键与释放交错。
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 当前配置文件管理器。
        /// 初始化前、初始化失败回滚后及释放后为 <c>null</c>。
        /// </summary>
        internal static ConfigService Config { get; set; }

        /// <summary>
        /// 是否向独立日志文件写入内容，默认启用；修改后会同步到当前 <see cref="LogWriter"/>。
        /// </summary>
        internal static ConfigEntry<bool> EnableLog { get; private set; }

        /// <summary>
        /// 独立日志文件的最低记录等级，默认为 <see cref="HLogSpace.LogLevel.Info"/>。
        /// </summary>
        internal static ConfigEntry<LogLevel> LogLevel { get; private set; }

        /// <summary>
        /// 打开配置界面的热键，默认值为 <c>F1</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> ConfigUIHotkey { get; private set; }

        /// <summary>
        /// 打开日志界面的热键，默认值为 <c>F2</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> LogUIHotkey { get; private set; }

        /// <summary>
        /// 从磁盘重新加载配置的热键，默认值为 <c>Ctrl+R</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> ReloadConfigHotkey { get; set; }

        /// <summary>
        /// GUI 与配置注释使用的语言，默认值为 <see cref="LanguageType.English"/>；修改后更新全局翻译语言。
        /// </summary>
        internal static ConfigEntry<LanguageType> SetLanguage { get; private set; }

        private const string SectionGeneral = "General";
        private const string SectionHotkey = "Hotkey";
        private const string SectionLog = "Log";

        /// <summary>
        /// 从 <see cref="BService.Config"/> 绑定全部框架配置项，补齐缺失项并保存一次规范化后的文件。
        /// 重复调用直接返回；失败时撤销已建立的静态引用和事件订阅，然后重新抛出原始异常。
        /// </summary>
        /// <param name="configFilePath">用于初始化完成日志的配置文件路径；实际读写路径由已注册的 <see cref="ConfigService"/> 决定。</param>
        /// <remarks>
        /// 调用前必须先通过 <see cref="HUserSpace.UserService"/> 建立配置服务。
        /// 初始化期间会暂时关闭逐项保存，待所有默认项绑定完成后统一写盘。
        /// </remarks>
        internal static void Initialize(string configFilePath)
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                try
                {
                    Config = BService.Config;

                    Config.SaveOnConfigSet = false;

                    Config.CreateTable(SectionGeneral, new Translator(chinese: "通用", english: "General"));
                    SetLanguage = Config.Bind(
                        SectionGeneral,
                        nameof(SetLanguage),
                        LanguageType.English,
                        new Translator(chinese: "设置语言", english: "Set Language"),
                        new Translator()
                        );

                    Config.CreateTable(
                        SectionHotkey,
                        new Translator(chinese: "热键", english: "Hotkey"),
                        new Translator(
                            chinese: "热键写法说明：\n" +
                                     "1） 单键：F / F1 / Space / Tab\n" +
                                     "2） 键盘组合键：修饰键在前，主键在最后，例如 Ctrl+Shift+F\n" +
                                     "3） 手柄组合键：手柄按键用 + 连接，例如 GamepadStart+GamepadA\n" +
                                     "4） 备选热键：Ctrl+F, GamepadStart+GamepadA（用逗号分隔）\n" +
                                     "5） 同一组组合键不能混用键盘和手柄，例如 Ctrl+GamepadA 不支持\n" +
                                     "\n" +
                                     "键盘修饰键：\n" +
                                     "- Ctrl（或 Control）/ Shift / Alt\n" +
                                     "- LeftCtrl（或 LCtrl）/ RightCtrl（或 RCtrl）\n" +
                                     "- LeftShift（或 LShift）/ RightShift（或 RShift）\n" +
                                     "- LeftAlt（或 LAlt）/ RightAlt（或 RAlt）\n" +
                                     "\n" +
                                     "手柄按键名称（配置写回时会规范化为以下名称）：\n" +
                                     "- GamepadA / GamepadB / GamepadX / GamepadY\n" +
                                     "- GamepadStart / GamepadBack / GamepadLB / GamepadRB\n" +
                                     "- GamepadDpadUp / GamepadDpadDown / GamepadDpadLeft / GamepadDpadRight\n" +
                                     "- GamepadLS / GamepadRS\n" +
                                     "- 也接受别名：South / East / West / North、Cross / Circle / Square / Triangle、Select / LeftShoulder / RightShoulder / LeftStick / RightStick\n" +
                                     "- 为避免与键盘键名冲突，建议手柄按键始终写成带 Gamepad 前缀的形式，例如 GamepadA\n" +
                                     "\n" +
                                     "示例：\n" +
                                     "- Ctrl+F\n" +
                                     "- LeftCtrl+RightShift+F\n" +
                                     "- GamepadStart+GamepadA\n" +
                                     "- Ctrl+Shift+F, GamepadStart+GamepadA",
                            english: "Hotkey notation guide:\n" +
                                     "1) Single key: F / F1 / Space / Tab\n" +
                                     "2) Keyboard chord: modifiers first, main key last, e.g. Ctrl+Shift+F\n" +
                                     "3) Gamepad chord: join gamepad buttons with +, e.g. GamepadStart+GamepadA\n" +
                                     "4) Alternatives: Ctrl+F, GamepadStart+GamepadA (comma-separated)\n" +
                                     "5) Keyboard and gamepad cannot be mixed in the same chord, e.g. Ctrl+GamepadA is not supported\n" +
                                     "\n" +
                                     "Keyboard modifiers:\n" +
                                     "- Ctrl (or Control) / Shift / Alt\n" +
                                     "- LeftCtrl (or LCtrl) / RightCtrl (or RCtrl)\n" +
                                     "- LeftShift (or LShift) / RightShift (or RShift)\n" +
                                     "- LeftAlt (or LAlt) / RightAlt (or RAlt)\n" +
                                     "\n" +
                                     "Gamepad button names (config values are normalized to these names when written back):\n" +
                                     "- GamepadA / GamepadB / GamepadX / GamepadY\n" +
                                     "- GamepadStart / GamepadBack / GamepadLB / GamepadRB\n" +
                                     "- GamepadDpadUp / GamepadDpadDown / GamepadDpadLeft / GamepadDpadRight\n" +
                                     "- GamepadLS / GamepadRS\n" +
                                     "- Also accepts aliases: South / East / West / North, Cross / Circle / Square / Triangle, Select / LeftShoulder / RightShoulder / LeftStick / RightStick\n" +
                                     "- To avoid conflicts with keyboard key names, gamepad buttons should be written with the Gamepad prefix, e.g. GamepadA\n" +
                                     "\n" +
                                     "Examples:\n" +
                                     "- Ctrl+F\n" +
                                     "- LeftCtrl+RightShift+F\n" +
                                     "- GamepadStart+GamepadA\n" +
                                     "- Ctrl+Shift+F, GamepadStart+GamepadA"
                        )
                        );

                    var unityService = UnityProvider.Instance;

                    ConfigUIHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(ConfigUIHotkey),
                        new Hotkey("F1", unityService),
                        new Translator(chinese: "配置界面热键", english: "Config UI Hotkey"),
                        new Translator(
                            chinese: "打开配置界面的热键。默认是 F1。",
                            english: "The hotkey to open config UI. Default is F1."
                        )
                        );
                    LogUIHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(LogUIHotkey),
                        new Hotkey("F2", unityService),
                        new Translator(chinese: "日志界面热键", english: "Log UI Hotkey"),
                        new Translator(
                            chinese: "打开日志界面的热键。默认是 F2。",
                            english: "The hotkey to open log UI. Default is F2."
                        )
                        );
                    ReloadConfigHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(ReloadConfigHotkey),
                        new Hotkey("Ctrl+R", unityService),
                        new Translator(chinese: "重新加载配置热键", english: "Reload Config Hotkey"),
                        new Translator(
                            chinese: "重新加载配置的热键。默认值为 Ctrl+R。",
                            english: "The hotkey to reload config. Default is Ctrl+R."
                        )
                        );

                    Config.CreateTable(
                        SectionLog,
                        new Translator(chinese: "日志", english: "Log"),
                        new Translator(
                            chinese: "日志将生成在 logs 文件夹中。\n" +
                                     "日志和 BepInEx 日志的等级可分别设置。\n" +
                                     "若日志等级为 Info，将记录所有消息。\n" +
                                     "若日志等级为 Warning，仅记录警告和错误消息。\n" +
                                     "若日志等级为 Error，仅记录错误消息。",
                            english: "Logs will be generated in logs folder.\n" +
                                     "The log level of logs and BepInEx log can be set separately.\n" +
                                     "If the log level is set to Info, it will log all messages.\n" +
                                     "If the log level is set to Warning, it will only log warning and error messages.\n" +
                                     "If the log level is set to Error, it will only log error messages."
                        )
                        );
                    EnableLog = Config.Bind(
                        SectionLog,
                        nameof(EnableLog),
                        true,
                        new Translator(chinese: "启用日志", english: "Enable Log"),
                        new Translator(
                            chinese: "启用日志。将在 logs 文件夹中生成日志文件。",
                            english: "Enable log. It will generate a log file in logs folder."
                        )
                        );
                    LogLevel = Config.Bind(
                        SectionLog,
                        nameof(LogLevel),
                        HLogSpace.LogLevel.Info,
                        new Translator(chinese: "日志等级", english: "Log Level"),
                        new Translator(
                            chinese: "日志等级。默认值为 Info。",
                            english: "The log level. Default is Info."
                        )
                        );

                    SetLanguage.OnValueChanged += OnSetLanguageChanged;
                    Translator.DefaultLanguage = SetLanguage.Value;

                    Config.SaveOnConfigSet = true;
                    Config.Save();

                    FrameUpdateManager.OnFrameUpdate += ReloadConfigOnUserOrder;

                    _initialized = true;
                    BLog.Info($"Config manager initialized: {configFilePath}");
                }
                catch (Exception ex)
                {
                    BLog.Error("Failed to initialize config manager.", ex);
                    Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// 移除语言与逐帧热键订阅，并清空所有静态配置引用。
        /// 此方法不释放 <see cref="ConfigService"/>，其所有权属于 <see cref="HUserSpace.UserService"/>。
        /// </summary>
        internal static void Dispose()
        {
            lock (_lock)
            {
                if (SetLanguage != null)
                    SetLanguage.OnValueChanged -= OnSetLanguageChanged;

                FrameUpdateManager.OnFrameUpdate -= ReloadConfigOnUserOrder;

                Config = null;
                EnableLog = null;
                LogLevel = null;
                ConfigUIHotkey = null;
                LogUIHotkey = null;
                ReloadConfigHotkey = null;
                SetLanguage = null;

                _initialized = false;
            }
        }

        /// <summary>
        /// 从当前配置路径重新读取文件，将已有运行时配置项重新绑定到新文件项并写回规范化内容。
        /// </summary>
        private static void ReloadConfig()
        {
            lock (_lock)
            {
                Config.Reload();
                BLog.Info("Config file reloaded.");
            }
        }

        /// <summary>
        /// 在逐帧回调中检测重载热键；重载期间发生的异常只记录日志，不传播到帧更新派发器。
        /// </summary>
        private static void ReloadConfigOnUserOrder()
        {
            if (ReloadConfigHotkey?.Value?.WasPressedThisFrame() == true)
            {
                try
                {
                    ReloadConfig();
                }
                catch (Exception ex)
                {
                    BLog.Error($"Unexpected error in {nameof(ReloadConfigOnUserOrder)}.", ex);
                }
            }
        }

        private static void OnSetLanguageChanged(object sender, LanguageType language)
        {
            Translator.DefaultLanguage = language;
        }
    }
}
