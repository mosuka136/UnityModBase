# UnityModBase

[![GitHub all releases](https://img.shields.io/github/downloads/mosuka136/UnityModBase/total)](https://github.com/mosuka136/UnityModBase/releases) [![GitHub release (latest by date)](https://img.shields.io/github/v/release/mosuka136/UnityModBase)](https://github.com/mosuka136/UnityModBase/releases) ![platform](https://img.shields.io/badge/platform-Windows-lightgrey)

[简体中文](./README.md) | [English](./README_EN.md)

UnityModBase 是一个面向 Unity 模组开发的基础设施解决方案。仓库包含核心类库和 BepInEx 生命周期适配层，主要用于统一管理模组的生命周期、配置、日志、热键、翻译和 IMGUI 工具窗口。

## 仓库组成

| 目录 | 说明 |
| --- | --- |
| [`UnityModBase`](./UnityModBase/) | 核心类库，包含框架公开 API 和详细接入文档 |
| [`BepInExLauncher`](./BepInExLauncher/README.md) | BepInEx 启动适配层，负责初始化、游戏启动派发和统一释放 |
| [`UnityModBase.Test`](./UnityModBase.Test/) | 基于 xUnit 的单元测试项目 |


## 文档导航

| 文档 | 简体中文 | English |
| --- | --- | --- |
| 核心类库说明与快速开始 | [README](./UnityModBase/README.md) | [README](./UnityModBase/README_EN.md) |
| 公开 API、调用顺序与约束 | [API](./UnityModBase/API.md) | [API](./UnityModBase/API_EN.md) |

初次接入建议先阅读核心类库 README；需要查找类型、成员、事件或线程约束时，再查阅 API 文档。

## 使用指引

### 使用内置 BepInEx 启动器

1. 构建或取得 `UnityModBase.dll` 与 `UnityModBase.BepInExLauncher.dll`。
2. 将两个文件放入目标游戏的 `BepInEx/plugins` 目录。
3. 在模组项目中引用 `UnityModBase.dll`。
4. 按[核心类库快速开始](./UnityModBase/README.md)注册模组用户、配置、日志和实时控制项。

内置启动器会负责调用框架的 `Initialize`、`GameBootRegistry.Boot` 和 `Dispose`。使用该启动器的模组不应重复驱动全局生命周期。

### 使用自定义宿主

如果不使用 BepInEx 启动器，宿主需要自行选择安全的 Unity 生命周期节点，并按顺序调用初始化、游戏启动派发和释放入口。具体顺序及主线程约束见[核心类库说明](./UnityModBase/README.md)和[生命周期 API](./UnityModBase/API.md)。

## 许可

`UnityModBase` 使用 `LGPL-3.0` 许可证，详见 `LICENSE.txt`。
