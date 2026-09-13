using System;
using System.Linq;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.BSpace
{
    /// <summary>
    /// 声明 UnityModBase 自身使用的配置表和配置项，并把语言与配置重载热键接入运行时事件。
    /// 重载热键作用于全部已注册用户的配置文件，而不限于框架自身（见 <see cref="ReloadConfig"/>）。
    /// 配置文件的创建、解析和持久化由 <see cref="ConfigService"/> 负责，本类型只维护配置项的静态引用。
    /// </summary>
    internal static class BConfigManager
    {
        // 串行化初始化、实际重载和释放对静态引用及事件订阅的修改。
        // 热键状态读取发生在加锁前，因此该锁不使整个管理器具备线程安全性。
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 当前配置文件管理器。
        /// 初始化前、初始化失败回滚后及释放后为 <c>null</c>。
        /// </summary>
        internal static ConfigService Config { get; set; }

        /// <summary>
        /// GUI 与配置注释使用的语言，默认值为 <see cref="LanguageType.English"/>；修改后更新全局翻译语言。
        /// </summary>
        internal static ConfigEntry<LanguageType> SetLanguage { get; private set; }

        // 三个 GUI 窗口的布局持久化条目，由通用窗口宿主（GuiHostBase）在布局变化停顿、隐藏或销毁时自动写回。
        // 哨兵约定与恢复语义见 WindowResizeHelper.TryBuildPersistedRect：宽/高 0 表示未持久化（沿用默认尺寸），
        // 位置负数表示未持久化（该方向屏幕居中）；写回前位置会归一化到屏幕内，持久值不会与哨兵冲突。

        /// <summary>
        /// 配置界面的宽度，单位为像素；0 表示未持久化，首次显示时使用默认比例尺寸。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ConfigGuiWidth { get; private set; }

        /// <summary>
        /// 配置界面的高度，单位为像素；0 表示未持久化，首次显示时使用默认比例尺寸。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ConfigGuiHeight { get; private set; }

        /// <summary>
        /// 配置界面左上角的屏幕横坐标，单位为像素；负数表示未持久化，恢复时该方向屏幕居中。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ConfigGuiX { get; private set; }

        /// <summary>
        /// 配置界面左上角的屏幕纵坐标，单位为像素；负数表示未持久化，恢复时该方向屏幕居中。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ConfigGuiY { get; private set; }

        /// <summary>
        /// 日志界面的宽度，单位为像素；0 表示未持久化，首次显示时使用默认尺寸。
        /// 日志窗口显示期间宽度按列宽自适应，写回的是隐藏前的最终宽度。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> LogGuiWidth { get; private set; }

        /// <summary>
        /// 日志界面的高度，单位为像素；0 表示未持久化，首次显示时使用默认尺寸。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> LogGuiHeight { get; private set; }

        /// <summary>
        /// 日志界面左上角的屏幕横坐标，单位为像素；负数表示未持久化，恢复时该方向屏幕居中。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> LogGuiX { get; private set; }

        /// <summary>
        /// 日志界面左上角的屏幕纵坐标，单位为像素；负数表示未持久化，恢复时该方向屏幕居中。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> LogGuiY { get; private set; }

        /// <summary>
        /// 实时控制界面的宽度，单位为像素；0 表示未持久化，首次显示时使用默认比例尺寸。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ControlGuiWidth { get; private set; }

        /// <summary>
        /// 实时控制界面的高度，单位为像素；0 表示未持久化，首次显示时使用默认比例尺寸。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ControlGuiHeight { get; private set; }

        /// <summary>
        /// 实时控制界面左上角的屏幕横坐标，单位为像素；负数表示未持久化，恢复时该方向屏幕居中。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ControlGuiX { get; private set; }

        /// <summary>
        /// 实时控制界面左上角的屏幕纵坐标，单位为像素；负数表示未持久化，恢复时该方向屏幕居中。
        /// </summary>
        [EntrySlider(50f, 4000f, 1f)]
        internal static ConfigEntry<float> ControlGuiY { get; private set; }

        /// <summary>
        /// 打开配置界面的热键，默认值为 <c>F1</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> ConfigUIHotkey { get; private set; }

        /// <summary>
        /// 打开日志界面的热键，默认值为 <c>F2</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> LogUIHotkey { get; private set; }

        /// <summary>
        /// 打开实时控制界面的热键，默认值为 <c>F3</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> ControlUIHotkey { get; private set; }

        /// <summary>
        /// 从磁盘重新加载全部已注册用户配置的热键，默认值为 <c>Ctrl+R</c>。
        /// </summary>
        internal static ConfigEntry<Hotkey> ReloadConfigHotkey { get; set; }

        /// <summary>
        /// 是否向独立日志文件写入内容，默认启用；修改后会同步到当前 <see cref="LogWriter"/>。
        /// </summary>
        internal static ConfigEntry<bool> EnableLog { get; private set; }

        /// <summary>
        /// 独立日志文件的最低记录等级，默认为 <see cref="HLogSpace.LogLevel.Info"/>。
        /// </summary>
        internal static ConfigEntry<LogLevel> LogLevel { get; private set; }

        private const string SectionGeneral = "General";
        private const string SectionHotkey = "Hotkey";
        private const string SectionLog = "Log";

        /// <summary>
        /// 从 <see cref="BService.Config"/> 绑定全部框架配置项，补齐缺失项并尝试保存一次规范化后的文件。
        /// 重复调用直接返回；失败时撤销已建立的静态引用和事件订阅，然后重新抛出原始异常。
        /// </summary>
        /// <param name="configFilePath">用于初始化完成日志的配置文件路径；实际读写路径由已注册的 <see cref="ConfigService"/> 决定。</param>
        /// <remarks>
        /// 调用前必须先通过 <see cref="HUserSpace.UserService"/> 建立配置服务。
        /// 绑定期间会关闭逐项保存，待所有默认项建立后重新启用并统一写盘；该过程不会保留服务原有的开关值。
        /// 若绑定阶段抛出异常，静态引用和事件订阅会被清理，但配置服务的 <see cref="ConfigService.SaveOnConfigSet"/> 不会恢复。
        /// 最终写入失败只记录错误，不阻止配置继续以内存模型完成初始化。
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
                    ConfigGuiWidth = Config.Bind(
                        SectionGeneral,
                        nameof(ConfigGuiWidth),
                        0f,
                        new Translator(chinese: "配置界面宽度", english: "Config UI Width"),
                        new Translator(
                            chinese: "配置界面的宽度。",
                            english: "The width of config UI.")
                        );
                    ConfigGuiHeight = Config.Bind(
                        SectionGeneral,
                        nameof(ConfigGuiHeight),
                        0f,
                        new Translator(chinese: "配置界面高度", english: "Config UI Height"),
                        new Translator(
                            chinese: "配置界面的高度。",
                            english: "The height of config UI.")
                        );
                    ConfigGuiX = Config.Bind(
                        SectionGeneral,
                        nameof(ConfigGuiX),
                        -1f,
                        new Translator(chinese: "配置界面位置 X", english: "Config UI Position X"),
                        new Translator(
                            chinese: "配置界面左上角的屏幕横坐标。",
                            english: "The screen X coordinate of the config UI top-left corner.")
                        );
                    ConfigGuiY = Config.Bind(
                        SectionGeneral,
                        nameof(ConfigGuiY),
                        -1f,
                        new Translator(chinese: "配置界面位置 Y", english: "Config UI Position Y"),
                        new Translator(
                            chinese: "配置界面左上角的屏幕纵坐标。",
                            english: "The screen Y coordinate of the config UI top-left corner.")
                        );
                    LogGuiWidth = Config.Bind(
                        SectionGeneral,
                        nameof(LogGuiWidth),
                        0f,
                        new Translator(chinese: "日志界面宽度", english: "Log UI Width"),
                        new Translator(
                            chinese: "日志界面的宽度。日志界面实际显示宽度按列宽自适应，本项仅在界面关闭前记录。",
                            english: "The width of log UI. The actual log UI width adapts to column widths; this entry only records the value before the UI closes.")
                        );
                    LogGuiHeight = Config.Bind(
                        SectionGeneral,
                        nameof(LogGuiHeight),
                        0f,
                        new Translator(chinese: "日志界面高度", english: "Log UI Height"),
                        new Translator(
                            chinese: "日志界面的高度。",
                            english: "The height of log UI.")
                        );
                    LogGuiX = Config.Bind(
                        SectionGeneral,
                        nameof(LogGuiX),
                        -1f,
                        new Translator(chinese: "日志界面位置 X", english: "Log UI Position X"),
                        new Translator(
                            chinese: "日志界面左上角的屏幕横坐标。",
                            english: "The screen X coordinate of the log UI top-left corner.")
                        );
                    LogGuiY = Config.Bind(
                        SectionGeneral,
                        nameof(LogGuiY),
                        -1f,
                        new Translator(chinese: "日志界面位置 Y", english: "Log UI Position Y"),
                        new Translator(
                            chinese: "日志界面左上角的屏幕纵坐标。",
                            english: "The screen Y coordinate of the log UI top-left corner.")
                        );
                    ControlGuiWidth = Config.Bind(
                        SectionGeneral,
                        nameof(ControlGuiWidth),
                        0f,
                        new Translator(chinese: "实时控制界面宽度", english: "Live Controls UI Width"),
                        new Translator(
                            chinese: "实时控制界面的宽度。",
                            english: "The width of live controls UI.")
                        );
                    ControlGuiHeight = Config.Bind(
                        SectionGeneral,
                        nameof(ControlGuiHeight),
                        0f,
                        new Translator(chinese: "实时控制界面高度", english: "Live Controls UI Height"),
                        new Translator(
                            chinese: "实时控制界面的高度。",
                            english: "The height of live controls UI.")
                        );
                    ControlGuiX = Config.Bind(
                        SectionGeneral,
                        nameof(ControlGuiX),
                        -1f,
                        new Translator(chinese: "实时控制界面位置 X", english: "Live Controls UI Position X"),
                        new Translator(
                            chinese: "实时控制界面左上角的屏幕横坐标。",
                            english: "The screen X coordinate of the live controls UI top-left corner.")
                        );
                    ControlGuiY = Config.Bind(
                        SectionGeneral,
                        nameof(ControlGuiY),
                        -1f,
                        new Translator(chinese: "实时控制界面位置 Y", english: "Live Controls UI Position Y"),
                        new Translator(
                            chinese: "实时控制界面左上角的屏幕纵坐标。",
                            english: "The screen Y coordinate of the live controls UI top-left corner.")
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
                                     "- Ctrl+Shift+F, GamepadStart+GamepadA")
                        );
                    var unityService = UnityProvider.Instance;
                    ConfigUIHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(ConfigUIHotkey),
                        new Hotkey("F1", unityService),
                        new Translator(chinese: "配置界面热键", english: "Config UI Hotkey"),
                        new Translator(
                            chinese: "打开配置界面的热键。默认是 F1。",
                            english: "The hotkey to open config UI. Default is F1.")
                        );
                    LogUIHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(LogUIHotkey),
                        new Hotkey("F2", unityService),
                        new Translator(chinese: "日志界面热键", english: "Log UI Hotkey"),
                        new Translator(
                            chinese: "打开日志界面的热键。默认是 F2。",
                            english: "The hotkey to open log UI. Default is F2.")
                        );
                    ControlUIHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(ControlUIHotkey),
                        new Hotkey("F3", unityService),
                        new Translator(chinese: "实时控制界面热键", english: "Live Controls UI Hotkey"),
                        new Translator(
                            chinese: "打开实时控制界面的热键。默认是 F3。",
                            english: "The hotkey to open the live controls UI. Default is F3.")
                        );
                    ReloadConfigHotkey = Config.Bind(
                        SectionHotkey,
                        nameof(ReloadConfigHotkey),
                        new Hotkey("Ctrl+R", unityService),
                        new Translator(chinese: "重新加载配置热键", english: "Reload Config Hotkey"),
                        new Translator(
                            chinese: "重新加载配置的热键。默认值为 Ctrl+R。",
                            english: "The hotkey to reload config. Default is Ctrl+R.")
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
                                     "If the log level is set to Error, it will only log error messages.")
                        );
                    EnableLog = Config.Bind(
                        SectionLog,
                        nameof(EnableLog),
                        true,
                        new Translator(chinese: "启用日志", english: "Enable Log"),
                        new Translator(
                            chinese: "启用日志。将在 logs 文件夹中生成日志文件。",
                            english: "Enable log. It will generate a log file in logs folder.")
                        );
                    LogLevel = Config.Bind(
                        SectionLog,
                        nameof(LogLevel),
                        HLogSpace.LogLevel.Info,
                        new Translator(chinese: "日志等级", english: "Log Level"),
                        new Translator(
                            chinese: "日志等级。默认值为 Info。",
                            english: "The log level. Default is Info.")
                        );

                    SetLanguage.OnValueChanged += OnSetLanguageChanged;
                    Translator.DefaultLanguage = SetLanguage.Value;

                    Config.SaveOnConfigSet = true;
                    if (!Config.Save())
                        BLog.Error($"Failed to save UnityModBase config file. Path='{configFilePath}'.");

                    FrameUpdateManager.OnFrameUpdate += ReloadConfigOnUserOrder;

                    _initialized = true;
                    BLog.Info($"Config manager initialized. Path='{configFilePath}', Language='{SetLanguage.Value}', FileLogging={EnableLog.Value}, MinimumLogLevel='{LogLevel.Value}'.");
                }
                catch (Exception ex)
                {
                    BLog.Error($"Failed to initialize config manager. Path='{configFilePath}'.", ex);
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
                SetLanguage = null;
                ConfigGuiWidth = null;
                ConfigGuiHeight = null;
                ConfigGuiX = null;
                ConfigGuiY = null;
                LogGuiWidth = null;
                LogGuiHeight = null;
                LogGuiX = null;
                LogGuiY = null;
                ControlGuiWidth = null;
                ControlGuiHeight = null;
                ControlGuiX = null;
                ControlGuiY = null;
                ConfigUIHotkey = null;
                LogUIHotkey = null;
                ControlUIHotkey = null;
                ReloadConfigHotkey = null;
                EnableLog = null;
                LogLevel = null;

                _initialized = false;
            }
        }

        /// <summary>
        /// 遍历 <see cref="UserManager.UserContexts"/> 中的全部用户上下文，逐个从磁盘重新读取
        /// 各用户配置服务对应的文件，并按文件记录成功或失败日志；框架自身的上下文包含在内
        /// （<see cref="BService"/> 初始化时已把框架登记为普通用户）。
        /// 尚未登记配置文件的用户（<see cref="UserService.Config"/> 为 <c>null</c>）没有可重载的文件，直接跳过。
        /// 单个文件重载失败（<see cref="ConfigService.Reload"/> 返回 false）只记录错误，不中断其余用户的重载。
        /// 配置应用的原子性边界与失败后的内存状态由 <see cref="ConfigService.Reload"/> 定义。
        /// 因此失败日志也可能仅表示事件发布或最终写入失败，此时已提交的运行时配置仍然有效。
        /// </summary>
        private static void ReloadConfig()
        {
            lock (_lock)
            {
                // UserContexts 是注册表的实时视图而非快照，注册/移除用户与重载并发时枚举可能失效；
                // 由此产生的异常会中止剩余重载，由唯一调用方（热键帧回调）统一捕获记录。
                var configs = UserManager.UserContexts.Select(u => u.Service.Config).Where(c => c != null);

                foreach (var config in configs)
                {
                    if (config.Reload())
                        BLog.Info($"Config file reloaded. Path='{config.FilePath}'.");
                    else
                        BLog.Error($"Failed to reload config file. Path='{config.FilePath}'. See earlier diagnostics for the failing stage.");
                }
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
                    BLog.Error($"Unexpected error while processing the config reload hotkey.", ex);
                }
            }
        }

        private static void OnSetLanguageChanged(object sender, LanguageType language)
        {
            Translator.DefaultLanguage = language;
        }
    }
}
