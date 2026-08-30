# UnityModBase

[简体中文](./README.md) | [English](./README_EN.md)

UnityModBase 是一个面向 Unity 模组的基础类库，为多个模组提供统一的生命周期、用户隔离、配置持久化、内存与文件日志、双语文本、热键以及 IMGUI 工具窗口。

项目内置 BepInEx 启动器。使用该启动器时，框架会在插件加载阶段初始化，在首个场景加载后派发游戏启动扩展点，并在启动器销毁时统一释放资源。

- [API 文档（中文）](./API.md)
- [API Documentation (English)](./API_EN.md)

## 功能

- 进程级初始化、游戏启动、逐帧更新和退出事件
- 基于特性的启动方法与常驻 `Component` 自动注册
- 按模组隔离的用户上下文和子上下文生命周期
- 强类型配置、自动保存、事务式重载和自定义值编解码
- 有界内存日志、重复日志合并和按小时文件日志
- 键盘与手柄热键，支持多个备选组合
- 中文/英文双语文本及运行时语言切换
- 自动生成的配置、日志和实时控制 IMGUI 窗口
- 可替换的 Unity/IMGUI 提供器，便于测试界面与输入逻辑

## 运行环境

- .NET Framework 4.7.2
- BepInEx（仅 `UnityModBase.BepInExLauncher` 需要）

## 安装

使用内置 BepInEx 启动器时，将下列文件放入游戏的 `BepInEx/plugins` 目录：

```text
UnityModBase.dll
UnityModBase.BepInExLauncher.dll
```

模组项目应引用 `UnityModBase.dll`。如模组直接使用热键类型，还需要引用目标游戏的 `Unity.InputSystem.dll`。

内置启动器提供以下默认操作：

| 热键 | 操作 |
| --- | --- |
| `F1` | 打开配置窗口 |
| `F2` | 打开日志窗口 |
| `F3` | 打开实时控制窗口 |
| `Ctrl+R` | 从磁盘重新加载配置 |

这些热键和界面语言可在 UnityModBase 自身的配置文件中修改。

## 快速开始

下面的示例在游戏启动扩展点中注册一个模组用户，并建立配置、日志和实时控制项。内置 BepInEx 启动器已经负责框架的全局初始化和启动派发，模组不应再次调用 `UnityModBase.Initialize` 或 `GameBootRegistry.Boot`。

```csharp
using System.IO;
using BepInEx;
using UnityEngine;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;
using UnityModBase.HControlSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

internal static class DemoMod
{
    internal static UserContext Context { get; private set; }

    [InitializeOnGameBoot]
    private static void Initialize()
    {
        Context = UserManager.Register(
            "com.example.demomod",
            new Translator("示例模组", "Demo Mod"));

        string dataDirectory = Path.Combine(Paths.ConfigPath, "DemoMod");
        string configPath = Path.Combine(dataDirectory, "DemoMod.cfg");
        string logDirectory = Path.Combine(dataDirectory, "logs");

        Context.Service.RegisterConfig<DemoConfig>(configPath);
        Context.Service.RegisterLog(logDirectory, "DemoMod", LogLevel.Info);

        DemoConfig.Initialize(Context.Service.Config);
        DemoControls.Initialize(Context.Service.Control);

        Context.Service.LogDatabase.Info(
            "Demo Mod initialized.",
            nameof(Initialize),
            string.Empty,
            0);
    }
}

internal static class DemoConfig
{
    public static ConfigEntry<bool> Enabled { get; private set; }

    [EntrySlider(0.25f, 3f, 0.25f)]
    public static ConfigEntry<float> Speed { get; private set; }

    internal static void Initialize(ConfigService config)
    {
        config.SaveOnConfigSet = false;
        try
        {
            config.CreateTable(
                "General",
                new Translator("通用", "General"));

            Enabled = config.Bind(
                "General",
                "Enabled",
                true,
                new Translator("启用", "Enabled"));

            Speed = config.Bind(
                "General",
                "Speed",
                1f,
                new Translator("速度", "Speed"),
                new Translator("运行速度倍率。", "Runtime speed multiplier."));
        }
        finally
        {
            config.SaveOnConfigSet = true;
        }

        config.Save();
    }
}

internal static class DemoControls
{
    internal static void Initialize(ControlService control)
    {
        control.CreateTable(
            "Runtime",
            new Translator("运行时", "Runtime"));

        ControlEntry<float> timeScale = control.Bind(
            "Runtime",
            "TimeScale",
            () => Time.timeScale,
            ControlUpdatePolicy.WhenVisibleEveryFrame,
            new Translator("时间倍率", "Time Scale"));

        timeScale.OnValueChanged += (sender, value) => Time.timeScale = value;
    }
}
```

配置管理器类型会被配置 GUI 用于查找静态配置属性上的 `EntrySlider` 等元数据，因此应将绑定结果保存在该类型的静态属性中。

## 自定义宿主

不使用内置 BepInEx 启动器时，宿主必须按顺序驱动框架生命周期：

```csharp
UnityModBase.UnityModBase.Initialize(dataDirectory);

// 在游戏已经进入可安全创建 Unity 对象的阶段调用一次。
GameBootRegistry.Boot();

// 在宿主卸载或进程退出时调用。
UnityModBase.UnityModBase.Dispose();
```

`Initialize` 只建立基础服务和扫描启动扩展点，不代表游戏已经完成启动。`Boot` 会创建带 `RegisterOnGameBoot` 的常驻组件并执行带 `InitializeOnGameBoot` 的静态方法。涉及 Unity 对象的启动和释放应在 Unity 主线程执行。

## 热键写法

- 单键：`F1`、`Space`、`Tab`
- 键盘组合：`Ctrl+Shift+F`
- 指定左右修饰键：`LeftCtrl+RightShift+F`
- 手柄组合：`GamepadStart+GamepadA`
- 多个备选组合：`Ctrl+F,GamepadStart+GamepadA`

同一个组合中不能混用键盘和手柄输入。为避免名称歧义，手柄按键建议始终使用 `Gamepad` 前缀。

## 生命周期与线程约束

- `UnityModBase.Initialize` 和 `UnityModBase.Dispose` 可重复调用，并串行化顶层生命周期切换。
- `GameBootRegistry.Boot` 在每个初始化周期只执行一次。
- `UserManager`、`ConfigService`、`ControlService` 和大部分 GUI 模型不提供完整的并发保护，应在受控线程中串行使用。
- `FrameUpdateManager.OnFrameUpdate`、GUI 绘制、输入查询以及 Unity 对象创建/销毁应在 Unity 主线程运行。
- 静态事件订阅者应在自身卸载时退订。框架全局释放会清理大部分进程级订阅，但不能替代短生命周期组件的主动清理。
