# UnityModBase API

[简体中文](./API.md) | [English](./API_EN.md)

本文档描述 UnityModBase 1.0.0 的公开 API、常用调用顺序和主要约束。成员的精确异常条件及底层模型细节同时记录在源码 XML 注释和 Release 构建生成的 `UnityModBase.xml` 中。

## 命名空间

| 命名空间 | 作用 |
| --- | --- |
| `UnityModBase` | 顶层生命周期、游戏启动、逐帧更新和退出事件 |
| `UnityModBase.HUserSpace` | 用户注册表、用户资源作用域和服务聚合 |
| `UnityModBase.HConfigSpace` | 强类型配置、文件模型、编解码和重载 |
| `UnityModBase.HLogSpace` | 内存日志、重复合并和文件写入 |
| `UnityModBase.HControlSpace` | 不持久化的运行时控制项 |
| `UnityModBase.HotkeyManager` | 键盘、手柄组合和可配置热键 |
| `UnityModBase.HTranslatorSpace` | 中英文文本和默认语言 |
| `UnityModBase.HClassAttribute` | 启动扩展点与配置 GUI 元数据特性 |
| `UnityModBase.HGuiSpace` | 通用 IMGUI 宿主、绑定和编辑器基础设施 |
| `UnityModBase.HConfigGUI` | 配置窗口 |
| `UnityModBase.HLogGUI` | 日志窗口 |
| `UnityModBase.HControlGUI` | 实时控制窗口 |
| `UnityModBase.HEnumHelper` | 枚举描述和显示控制 |
| `UnityModBase.HProvider` | Unity 与 IMGUI 调用抽象 |

## 生命周期

### `UnityModBase.UnityModBase`

进程级入口。初始化和释放通过内部锁串行化。

```csharp
public static void Initialize(string baseDirectory);
public static void Dispose();
```

`Initialize` 完成以下工作：

1. 规范化数据目录；末级目录不是 `UnityModBase` 时自动追加该子目录。
2. 创建框架自身的用户、配置和日志服务。
3. 扫描当前已加载程序集，并监听后续程序集加载。
4. 登记带游戏启动特性的类型和方法。

它不会自动调用 `GameBootRegistry.Boot`。初始化失败会回滚已经建立的框架状态并重新抛出原始异常。

`Dispose` 依次派发退出回调、停止启动注册器、释放框架服务和所有用户上下文，并清空逐帧及语言事件。涉及 Unity 对象的释放应在主线程执行。

### `GameBootRegistry`

```csharp
public static event Action OnGameBoot;

public static void Boot();
public static void RegisterAssemblies(params Assembly[] assemblies);
public static void RegisterAssembly(Assembly assembly);
public static bool RegisterComponentOnGameBoot(Type type);
public static bool RegisterMethodOnGameBoot(MethodInfo method);
public static bool IsShouldSkipAssembly(Assembly assembly);
```

- `Boot` 在当前初始化周期只派发一次。
- 单个启动处理器失败只记录日志，不阻止其他处理器。
- 启动后才加载的程序集不会补执行其中的启动扩展点。
- `RegisterComponentOnGameBoot` 接受派生自 `UnityEngine.Component` 的类型。
- `RegisterMethodOnGameBoot` 的有效目标必须是非泛型、静态、无参数、返回 `void` 的方法；签名在执行阶段校验。
- 注册器创建的组件位于隐藏、跨场景保留的 `GameObject` 上，并在框架释放时销毁。

### 启动特性

```csharp
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class InitializeOnGameBootAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisterOnGameBootAttribute : Attribute;
```

```csharp
[InitializeOnGameBoot]
private static void InitializeMod()
{
}

[RegisterOnGameBoot]
internal sealed class RuntimeComponent : MonoBehaviour
{
}
```

### `FrameUpdateManager`

```csharp
public static event Action OnFrameUpdate;
```

`FrameUpdateManager.Updater` 在游戏启动时自动创建，并在 Unity 主线程的每个 `Update` 中同步派发事件。单个普通异常会被记录，`MissingMethodException` 会使失效订阅被自动移除。

### `GameQuitManager`

```csharp
public static event Action OnGameQuit;
```

事件在 Unity 退出或框架显式释放时执行一次。派发前会清空当前订阅列表，单个回调失败不影响后续回调。

## 用户与资源作用域

### `UserManager`

```csharp
public static event Action<UserContext> OnUserRegistered;
public static event Action<string> OnUserRemoved;
public static event Action<UserContext> OnConfigChanged;

public static IEnumerable<string> UserIds { get; }
public static IEnumerable<UserContext> UserContexts { get; }

public static UserContext Register(string userId, Translator name);
public static UserContext CreateUser(string userId, Translator name);
public static UserContext GetUser(string userId);
public static bool ContainsUser(string userId);
public static string GetDefaultUserId();
public static void RemoveUser(string userId);
```

`Register` 是模组接入的首选入口：

- 空标识会自动生成。
- 标识冲突时为新用户追加随机后缀。
- 自动接入用户级配置变化转发。
- 登记完成后派发 `OnUserRegistered`。

`CreateUser` 是较低级入口。遇到同标识时直接返回已有上下文，不派发注册事件，也不接入全局配置变化转发。

`UserIds` 和 `UserContexts` 是注册表的实时视图，不是快照；注册表变化期间不要继续枚举。`UserManager` 本身不是并发集合，应由调用方串行访问。

### `UserContext`

```csharp
public UserContext(string userId, Translator name);

public string UserId { get; }
public Translator Name { get; }
public UserService Service { get; set; }

public void AddChildContext(string key, IUserContext context);
public IUserContext GetChildContext(string key);
public void RemoveChildContext(string key);
public void Dispose();
```

`AddChildContext` 将子上下文的生命周期所有权交给当前用户。`RemoveChildContext` 只解除登记，不释放对象；移除后由调用方负责其生命周期。不要构造父子引用环。

### `IUserContext`

```csharp
public interface IUserContext : IDisposable
{
}
```

自定义用户级模块上下文实现该接口后，可挂载到 `UserContext` 并随用户统一释放。

### `UserService`

```csharp
public UserService(string userId);

public event Action OnConfigChanged;

public string UserId { get; }
public LogDatabase LogDatabase { get; }
public LogWriter LogWriter { get; }
public ConfigService Config { get; }
public ControlService Control { get; }
public Type ConfigManagerType { get; }

public void RegisterConfig<T>(string configFilePath) where T : class;
public void RegisterConfig(Type configManagerType, string configFilePath);
public void RegisterLog(string directory, string fileName, LogLevel level);
public void Dispose();
```

重复注册配置会释放旧 `ConfigService`，重复注册日志会释放旧 `LogWriter`。通过 `UserService.OnConfigChanged` 登记的处理器会迁移到后续替换的配置服务；直接订阅旧 `ConfigService` 的处理器不会迁移。

## 配置

### 基本流程

```csharp
UserContext user = UserManager.Register(
    "com.example.mod",
    new Translator("示例", "Example"));

user.Service.RegisterConfig<ModConfig>(configPath);

ConfigService config = user.Service.Config;
config.CreateTable("General", new Translator("通用", "General"));

ConfigEntry<bool> enabled = config.Bind(
    "General",
    "Enabled",
    true,
    new Translator("启用", "Enabled"));

enabled.OnValueChanged += (sender, value) => ApplyEnabled(value);
```

键名规则：表键和配置项键都必须非空，且只能包含 Unicode 字母、数字或下划线。

### `ConfigService`

```csharp
public ConfigService(string filePath);

public event Action OnConfigChanged;

public string FilePath { get; set; }
public bool SaveOnConfigSet { get; set; }
public ConfigFileSheet FileSheet { get; }
public ConfigSheet Sheet { get; }

public void CreateTable(
    string key,
    Translator name,
    Translator description = null);

public ConfigEntry<T> Bind<T>(
    string tableKey,
    string key,
    T defaultValue,
    Translator name,
    Translator description = null);

public ConfigEntry<T1, T2> Bind<T1, T2>(
    string tableKey,
    string key,
    T1 defaultValue1,
    T2 defaultValue2,
    Translator name,
    Translator description = null,
    Translator valueDescription1 = null,
    Translator valueDescription2 = null);

public bool Read();
public bool Reload();
public bool Save();
public bool Write();
public void Dispose();
```

调用约束：

- `filePath` 必须包含目录部分。
- 必须先 `CreateTable`，再对该表调用 `Bind`。
- 同一运行时表或配置键不能重复声明。
- 文件中的有效值优先于代码中的默认值。
- `SaveOnConfigSet = true` 时，每次有效赋值都会在当前线程同步写回完整配置文件。
- 批量声明时可临时关闭 `SaveOnConfigSet`，最后调用一次 `Save`。
- `Read` 只替换文件模型，不重新绑定现有运行时配置；希望更新现有绑定时使用 `Reload`。
- `Reload` 会先验证全部已绑定项，再整体提交新值；准备失败不会改变当前运行时值。

### `ConfigEntry<T>`

```csharp
public T Value { get; set; }
public T DefaultValue { get; }
public string TableKey { get; }
public string Key { get; }
public Translator Name { get; }
public Translator Description { get; }
public Type ValueType { get; }
public ConfigFileEntry Entry { get; }

public event EventHandler<T> OnValueChanged;
public event EventHandler OnValueChangedBase;
```

只有新旧值不等价时才更新文件模型并派发事件。强类型事件先于非泛型事件。事件处理器中的重入赋值会排到当前一轮通知之后，仍在同一调用线程同步处理。

### `ConfigEntry<T1, T2>`

```csharp
public EntryValue<T1, T2> Value { get; set; }
public T1 Value1 { get; set; }
public T2 Value2 { get; set; }
public event EventHandler OnValueChangedBase;
```

双元素配置以一个整体值持久化。修改 `Value1` 或 `Value2` 会构造新的整体值，从而复用统一的编码、事件和自动保存流程。

```csharp
ConfigEntry<int, int> size = config.Bind(
    "Window",
    "Size",
    800,
    600,
    new Translator("窗口尺寸", "Window Size"),
    valueDescription1: new Translator("宽度", "Width"),
    valueDescription2: new Translator("高度", "Height"));

size.Value1 = 1024;
size.Value2 = 768;
```

### 支持的配置值

| 类别 | 支持范围 | 文本形式示例 |
| --- | --- | --- |
| 基础类型 | `sbyte`、`short`、`int`、`long`、`byte`、`ushort`、`uint`、`ulong`、`float`、`double`、`bool`、`string` | `42`、`1.5`、`true`、`"text"` |
| 枚举 | 任意枚举类型，不区分大小写解析 | `Warning` |
| 集合 | 数组、可赋值的 `List<T>`、具有兼容构造函数或公开 `Add` 的泛型集合 | `[1,2,3]` |
| 元组 | 最多 7 个直接元素的 `ValueTuple` | `(10,"name")` |
| 双元素条目 | `EntryValue<T1,T2>`，通常通过双泛型 `Bind` 创建 | `10,20` |
| 自定义值 | 实现 `IConfigEntryValue` 且具有可访问无参构造函数 | 由实现定义 |

配置格式不支持 `null`。集合和元组的元素也必须能递归编码。字符串使用双引号，并支持 `\\`、`\"`、`\n`、`\r`、`\t` 转义。

### 自定义配置值

```csharp
public interface IConfigEntryValue : IEntryValue
{
    ConfigFileResult<string> Encode();
    ConfigFileResult<object> Decode(string content);
    ConfigFileResult<string> EncodeValueType();
}

public interface IEntryValue
{
    bool Equals(IEntryValue other);
}
```

实现类型必须提供可访问的无参构造函数，因为框架会通过 `Activator.CreateInstance` 创建临时解码器。`Equals` 应实现内容等值，以便框架跳过无变化的赋值和重载事件。

### 文件模型与结果类型

以下类型用于扩展配置格式或直接处理文件模型：

| 类型 | 作用 |
| --- | --- |
| `ConfigFileModel` | 值、集合、元组和自定义值的静态编解码入口 |
| `ConfigFileSheet` | 有序的配置文件表集合 |
| `ConfigFileTable` | 单个文件表及其文件项 |
| `ConfigFileEntry` | 配置项文本、类型、默认值和多语言注释 |
| `ConfigFileResult<T>` | 携带 `Success`、`Value` 和结构化错误的结果 |
| `ConfigFileError` | 错误码、消息和调用位置 |
| `ConfigFileErrorCode` | `InvalidValue`、`UnsupportedType` 等错误分类 |
| `ConfigSheet` / `ConfigTable` | 运行时强类型配置模型 |
| `EntryChangePlan` | 重载期间使用的可应用、发布和回滚计划 |

通常的模组配置只需要 `ConfigService` 和 `ConfigEntry<T>`，无需直接修改文件层模型。

## 配置 GUI 元数据

### `EntrySliderAttribute`

```csharp
[EntrySlider(float min, float max, float step = -1f)]
public static ConfigEntry<float> Volume { get; private set; }
```

该特性只改变 GUI 的控件和拖动行为，不限制配置文件或文本输入的值域。`step <= 0` 表示不吸附。

双元素配置可使用组合元数据：

```csharp
[EntryGui(2)]
[EntrySlider(0, 320f, 3840f, 1f)]
[EntrySlider(1, 240f, 2160f, 1f)]
public static ConfigEntry<int, int> Resolution { get; private set; }
```

`EntryGuiAttribute.Count` 指定槽位数量，带索引的 `EntrySliderAttribute` 指定每个元素使用的槽位。

配置 GUI 通过 `UserService.ConfigManagerType` 查找静态属性当前持有的配置项，而不是按属性名匹配。元数据属性可以是公开或非公开属性，但必须是静态、非索引属性并已经完成初始化。

### 元数据类型

```csharp
public interface IUiMetadata
{
    Type MetadataType { get; }
}

public sealed class UiSliderMetadata : IUiMetadata;
public sealed class UiCompositeMetadata : IUiMetadata;
```

`UiMetadataHelper.GetMetadata(Type, IConfigEntry)` 可从配置管理器的属性特性生成上述运行时元数据。

## 日志

### `LogDatabase`

```csharp
public const int MaxLogCount = 500;

public LogDatabase(UnityProvider unityService);

public IReadOnlyList<LogEntry> Logs { get; }
public int Seq { get; }

public event Action<LogEntry> OnLogAdded;
public event Action<LogEntry> OnLogRemoved;
public event Action<LogEntry> OnLogRepeated;

public void AddLog(LogEntry log);
public void AddLog(
    LogLevel logLevel,
    string msg,
    Exception ex,
    string member,
    string file,
    int line);

public void Debug(string msg, string member, string file, int line);
public void Info(string msg, string member, string file, int line);
public void Notice(string msg, string member, string file, int line);
public void Warn(string msg, string member, string file, int line);
public void Error(string msg, Exception ex, string member, string file, int line);

public void SubscribeWithSnapshot(
    Action<IReadOnlyList<LogEntry>> initialize,
    Action<LogEntry> onLogAdded,
    Action<LogEntry> onLogRemoved,
    Action<LogEntry> onLogRepeated);

public void Unsubscribe(
    Action<LogEntry> onLogAdded,
    Action<LogEntry> onLogRemoved,
    Action<LogEntry> onLogRepeated);

public void Dispose();
```

数据库最多保留 500 个非重复条目。线程、场景、等级、消息、调用位置和异常文本相同的日志会合并到首个匹配项，并更新重复次数。

需要“初始快照 + 后续变化”无遗漏衔接时，使用 `SubscribeWithSnapshot`，不要自行组合 `Logs` 与事件订阅。

### `LogWriter`

```csharp
public LogWriter(string directory, string fileName, LogLevel level);

public bool Enable { get; set; }
public LogLevel Level { get; set; }

public void Log(LogEntry log);
public void Write(LogEntry log);
public void Flush(bool forced = false);
public void Dispose();
```

文件名格式为 `<基础名>-yyyy-MM-dd-HH.log`。写入器实例存续期间不会跨小时切换文件。`UserService.RegisterLog` 会自动把数据库快照和后续新增/重复事件接入写入器，通常无需直接创建 `LogWriter`。

### `LogEntry` 与 `LogLevel`

`LogEntry` 保存序号、时间、线程、Unity 帧、场景、等级、消息、调用位置、异常和重复状态。`ToString` 返回可直接写入日志文件的多行文本。

日志等级从低到高为：

```text
Debug < Info < Notice < Warning < Error
```

## 实时控制

实时控制服务管理纯内存条目，不读取或写入配置文件。GUI 写入只更新条目缓存并派发事件；业务代码应在事件中将新值应用到目标对象。

### `ControlService`

```csharp
public ControlSheet Sheet { get; }
public event Action OnStructureChanged;

public void CreateTable(
    string key,
    Translator name,
    Translator description = null);

public ControlEntry<T> Bind<T>(
    string tableKey,
    string key,
    Func<T> valueGetter,
    ControlUpdatePolicy updatePolicy,
    Translator name,
    Translator description = null,
    IUiMetadata metadata = null);

public void Update(
    float unscaledDeltaTime,
    bool isVisible,
    bool becameVisible = false);

public void Dispose();
```

`Bind` 会立即调用一次 `valueGetter` 取得初始缓存值。每次成功新增表或条目后都会同步派发 `OnStructureChanged`。

内置 `HControlGUI.GuiHost` 会负责每帧调用当前用户控制服务的更新入口；自行使用 `ControlService` 时，宿主必须主动调用 `Update`。

### `ControlEntry<T>`

```csharp
public T Value { get; }
public Func<T> ValueGetter { get; }
public ControlUpdatePolicy UpdatePolicy { get; }
public IUiMetadata Metadata { get; }
public event EventHandler<T> OnValueChanged;
```

getter 自动刷新不会触发 `OnValueChanged`；只有 GUI 提交了不同的新值时才触发。控制项只支持单值条目类型，不接受 `EntryValue<T1,T2>` 这类多元素类型。

### `ControlUpdatePolicy`

```csharp
public static ControlUpdatePolicy EveryFrame { get; }
public static ControlUpdatePolicy EverySecond { get; }
public static ControlUpdatePolicy WhenVisibleEveryFrame { get; }
public static ControlUpdatePolicy WhenVisibleEverySecond { get; }
public static ControlUpdatePolicy Never { get; }

public static ControlUpdatePolicy When(Func<bool> condition);
public static ControlUpdatePolicy WhenVisible(Func<bool> condition);
```

按秒策略使用非缩放时间。`WhenVisibleEverySecond` 在界面重新显示的第一帧立即刷新，并重新开始计时。

## 热键

### `Hotkey`

```csharp
public static bool GlobalValid { get; set; }

public Hotkey();
public Hotkey(string hotkey, UnityProvider unityService);

public List<HotkeyChord> Hotkeys { get; set; }
public bool Valid { get; set; }
public int Count { get; }

public static bool TryParse(
    string text,
    UnityProvider unityService,
    out Hotkey result);

public bool WasPressedThisFrame();
public void Add(HotkeyChord chord);
public void Remove(HotkeyChord chord);
public void RemoveInvalidHotkey();
public bool HasSameHotkey(Hotkey other);
public Hotkey Clone();
public override string ToString();
```

`Hotkey` 同时实现 `IConfigEntryValue`，可直接作为配置项类型。逗号分隔备选组合，加号连接同一组合中的按键：

```csharp
Hotkey openPanel = new Hotkey(
    "Ctrl+F1,GamepadStart+GamepadA",
    UnityProvider.Instance);

if (openPanel.WasPressedThisFrame())
    TogglePanel();
```

`GlobalValid = false` 会禁用所有热键；实例的 `Valid = false` 只禁用该实例。

### 组合与触发器

| 类型 | 作用 |
| --- | --- |
| `HotkeyChord` | 键盘或手柄组合的统一包装 |
| `KeyboardChord` | 一个主键和零个或多个修饰键 |
| `GamepadChord` | 一个或多个手柄按键 |
| `KeyboardTrigger` | 普通键盘按键 |
| `KeyboardModifierTrigger` | Ctrl、Shift、Alt 及左右侧修饰键 |
| `GamepadTrigger` | 手柄按键及常用别名 |
| `IHotkeyChord` | 组合的清理、复制和输入查询契约 |
| `IHotkeyTrigger` | 单个触发器的复制和输入查询契约 |
| `HotkeyResult<T>` | 热键解析结果和错误列表 |

## 翻译

### `Translator`

```csharp
public Translator(string chinese = "", string english = "");

public static LanguageType DefaultLanguage { get; set; }
public static event EventHandler<LanguageType> OnDefaultLanguageChanged;

public LanguageType LanguageType { get; set; }
public string Chinese { get; set; }
public string English { get; set; }
public string Default { get; }
```

实例默认使用 `LanguageType.Default`，即跟随全局 `DefaultLanguage`。`ToString` 和到 `string` 的隐式转换都会返回当前语言文本。

```csharp
Translator title = new Translator("设置", "Settings");
Translator.DefaultLanguage = LanguageType.Chinese;

string text = title; // "设置"
```

`LanguageType.None`、未知值以及仍为 `Default` 的全局语言都按英文解析。该类型不执行资源查找，也不会在某种语言文本为空时自动回退到另一种语言。

## 枚举辅助

```csharp
public sealed class DisplayEnumAttribute : Attribute
{
    public bool IsDisplay { get; set; }
}

public static string EnumHelper.GetDescription<TEnum>(TEnum value);
public static bool EnumHelper.IsDisplay<TEnum>(TEnum value);
```

`EnumHelper.GetDescription` 读取 `DescriptionAttribute`，未标记时返回枚举名称。`DisplayEnum(false)` 可从 GUI 选项中隐藏枚举值，但不阻止配置文件解析该值。

## GUI

### 内置窗口

| 宿主 | 默认热键 | 数据来源 |
| --- | --- | --- |
| `HConfigGUI.GuiHost` | `F1` | 用户的 `ConfigService` |
| `HLogGUI.GuiHost` | `F2` | 用户的 `LogDatabase` |
| `HControlGUI.GuiHost` | `F3` | 用户的 `ControlService` |

三个宿主都带 `RegisterOnGameBoot`，会由启动注册器自动创建。它们按用户挂载独立 GUI 子上下文，并使用 `UserManager` 事件响应用户、配置或控制结构变化。

### `GuiHostBase`

自定义 IMGUI 工具窗口可继承 `GuiHostBase`。主要公开状态和操作：

```csharp
public readonly int WindowID;
public string SelectedUserKey { get; }
public string GuiContextKey { get; }
public IEnumerable<UserContext> Users { get; }
public IUserContext CurrentContext { get; }
public bool IsVisible { get; }
public Rect WindowRect { get; }
public Hotkey UIHotkey { get; }

public virtual void DrawWindow(int id);
public virtual void TryAutoHideOnFocusLost();
public virtual void Hide();
public virtual void ToggleVisibility();
public IUserContext GetContext(string key);
public string GetDefaultUserKey();
```

派生类覆盖 `Awake` 和 `OnDestroy` 时必须调用基类实现，以建立和解除进程级用户移除事件。Unity 生命周期、用户移除和 IMGUI 绘制必须串行执行。

### 通用 GUI 公开类型

| 类型组 | 主要类型 |
| --- | --- |
| 上下文 | `EditableGuiContext`、`EntryEditBuffer`、`EntryChangeSink` |
| 用户编辑器 | `UserEditorBase`、`EditableUserEditorBase` |
| 绑定 | `INodeBinding`、`IEntryBinding`、`IResettableEntryBinding`、`GroupBinding` |
| 值编辑器 | `IValueEditor`、`ValueEditorRegistry`、`BooleanEditor`、`NumberEditor`、`SliderEditor`、`StringEditor`、`EnumEditor`、`UnsupportedEditor` |
| 浮层 | `ToastEditor`、`TooltipEditor` |
| 样式 | `IStyleResource`、`IEntryStyleResource`、`EntryStyleResource` |
| 转换 | `TypeConvert`、`ValueProvider`、`EntryModel` |

这些类型主要用于扩展内置窗口或编写新的 IMGUI 宿主。普通模组配置和实时控制接入不需要直接使用它们。

## Unity 提供器

### `IUnityProvider`

封装时间、帧号、场景、键盘、手柄、鼠标、剪贴板、数学函数和退出事件。默认实现为单例 `UnityProvider.Instance`。

### `IUnityGuiProvider`

封装 Unity IMGUI 的窗口、布局、标签、按钮、输入框、滑条和样式访问。默认实现为单例 `UnityGuiProvider.Instance`。

通用 GUI 编辑器通过接口接收这些依赖，可在测试中替换为模拟实现。热键底层类型当前直接持有 `UnityProvider`，不是 `IUnityProvider`。

## 反射辅助

`ClassHelper` 提供以下公开扫描能力：

```csharp
public static Type[] GetClasses<TAttribute>(Assembly assembly);
public static MethodInfo[] GetMethods<TAttribute>(Assembly assembly);
public static PropertyInfo[] GetProperties<TAttribute>(Type classType);
public static Type[] GetTypeSafe(Assembly assembly);
public static Type[] GetRegisterOnGameBootClasses(Assembly assembly);
public static MethodInfo[] GetInitializeOnGameBootMethods(Assembly assembly);
public static Attribute[] GetEntryDeclarationAttributes(
    Type classType,
    IConfigEntry entry);
```

扫描方法不缓存结果。高频调用方应自行缓存，部分类型加载失败时应按返回的可用子集继续处理。
