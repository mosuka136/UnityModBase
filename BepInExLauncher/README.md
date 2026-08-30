# UnityModBase.BepInExLauncher

[简体中文](./README.md) | [English](./README_EN.md)

`UnityModBase.BepInExLauncher` 是 UnityModBase 的 BepInEx 生命周期适配层。它负责在合适的 BepInEx 和 Unity 生命周期节点初始化、启动和释放核心框架，本身不声明模组配置、日志或业务功能。

## 职责

- 在 BepInEx 插件 `Awake` 阶段调用 `UnityModBase.Initialize`
- 监听 Unity 场景加载，并在首次场景加载后调用 `GameBootRegistry.Boot`
- 在插件销毁时取消场景订阅并调用 `UnityModBase.Dispose`
- 将适配层初始化和启动错误写入 BepInEx 日志

配置、日志、热键、翻译、用户上下文和 IMGUI 窗口均由[核心类库](../UnityModBase/README.md)提供。

## 运行要求

- .NET Framework 4.7.2
- BepInEx
- `UnityModBase.dll`

启动器元数据：

| 项目 | 值 |
| --- | --- |
| BepInEx GUID | `com.buele.bepinexlauncher` |
| 版本 | `1.0.0` |
| 程序集 | `UnityModBase.BepInExLauncher.dll` |

## 安装

将下列文件放入目标游戏的 `BepInEx/plugins` 目录：

```text
UnityModBase.dll
UnityModBase.BepInExLauncher.dll
```

只应安装一份启动器。多个启动器实例虽然会遇到幂等的初始化和启动入口，但任一实例销毁时都可能提前释放共享的进程级状态。

## 启动时序

| 阶段 | 行为 |
| --- | --- |
| BepInEx `Awake` | 先订阅 `SceneManager.sceneLoaded`，再以 `Paths.PluginPath` 初始化 UnityModBase |
| 首次场景加载 | 调用一次 `GameBootRegistry.Boot`，执行已登记的游戏启动扩展点 |
| 后续场景加载 | 启动已经成功时不再重复派发 |
| 插件 `OnDestroy` | 取消场景订阅，并统一释放 UnityModBase |

如果 `Initialize` 失败，启动器会撤销场景订阅、记录错误并销毁自身；销毁路径会调用 `Dispose` 清理部分初始化状态。

如果 `GameBootRegistry.Boot` 向外抛出异常，本次启动不会被标记为成功，后续场景加载会再次尝试。启动扩展点内部的普通异常通常由核心注册器隔离并记录，不会阻止其他扩展点执行。

## 模组接入

依赖该启动器的 BepInEx 模组可以显式声明依赖关系：

```csharp
using BepInEx;

[BepInPlugin("com.example.mod", "Example Mod", "1.0.0")]
[BepInDependency("com.buele.bepinexlauncher")]
public sealed class ExamplePlugin : BaseUnityPlugin
{
}
```

模组初始化逻辑应使用 UnityModBase 的游戏启动扩展点，而不是再次调用全局生命周期入口：

```csharp
using UnityEngine;
using UnityModBase.HClassAttribute;

internal static class ModStartup
{
    [InitializeOnGameBoot]
    private static void Initialize()
    {
        // 注册用户、配置、日志和实时控制项。
    }
}

[RegisterOnGameBoot]
internal sealed class RuntimeComponent : MonoBehaviour
{
}
```

- `InitializeOnGameBoot` 方法必须是非泛型、静态、无参数且返回 `void`。
- `RegisterOnGameBoot` 类型必须继承 `UnityEngine.Component`。
- 启动器已经负责 `Initialize`、`Boot` 和 `Dispose`，模组不应重复调用这些入口。
- 模组自身被单独卸载时，应主动移除静态事件订阅，并通过 `UserManager.RemoveUser` 释放自己的用户上下文。

完整接入示例见[核心类库 README](../UnityModBase/README.md)，成员说明见[API 文档](../UnityModBase/API.md)。

## 自定义宿主

不使用 BepInEx 或需要自行定义游戏启动边界时，不需要安装本启动器。宿主可以直接引用 `UnityModBase.dll`，并按照核心类库文档自行驱动 `Initialize`、`GameBootRegistry.Boot` 和 `Dispose`。
