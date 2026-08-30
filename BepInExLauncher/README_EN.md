# UnityModBase.BepInExLauncher

[简体中文](./README.md) | [English](./README_EN.md)

`UnityModBase.BepInExLauncher` is the BepInEx lifecycle adapter for UnityModBase. It initializes, boots, and disposes the core framework at appropriate BepInEx and Unity lifecycle points. It does not declare mod configuration, logging, or business features itself.

## Responsibilities

- Call `UnityModBase.Initialize` during the BepInEx plugin's `Awake`
- Observe Unity scene loads and call `GameBootRegistry.Boot` after the first scene is loaded
- Remove the scene subscription and call `UnityModBase.Dispose` when the plugin is destroyed
- Report adapter initialization and boot failures through the BepInEx logger

Configuration, logging, hotkeys, translation, user contexts, and IMGUI windows are provided by the [core library](../UnityModBase/README_EN.md).

## Requirements

- .NET Framework 4.7.2
- BepInEx
- `UnityModBase.dll`

Launcher metadata:

| Item | Value |
| --- | --- |
| BepInEx GUID | `com.buele.bepinexlauncher` |
| Version | `1.0.0` |
| Assembly | `UnityModBase.BepInExLauncher.dll` |

## Installation

Place these files in the target game's `BepInEx/plugins` directory:

```text
UnityModBase.dll
UnityModBase.BepInExLauncher.dll
```

Install only one copy of the launcher. Although initialization and boot entry points are idempotent, destruction of any launcher instance may dispose the shared process-level state too early.

## Startup sequence

| Stage | Behavior |
| --- | --- |
| BepInEx `Awake` | Subscribe to `SceneManager.sceneLoaded`, then initialize UnityModBase with `Paths.PluginPath` |
| First scene load | Call `GameBootRegistry.Boot` once and run registered game-boot extensions |
| Later scene loads | Do not dispatch again after boot succeeds |
| Plugin `OnDestroy` | Remove the scene subscription and dispose UnityModBase |

If `Initialize` fails, the launcher removes its scene subscription, logs the error, and destroys itself. The destruction path calls `Dispose` to clean up partially initialized state.

If `GameBootRegistry.Boot` throws outside the registry, the attempt is not marked as successful and the next scene load retries it. Ordinary exceptions inside individual boot extensions are normally isolated and logged by the core registry without stopping other extensions.

## Mod integration

A BepInEx mod that requires this launcher can declare the dependency explicitly:

```csharp
using BepInEx;

[BepInPlugin("com.example.mod", "Example Mod", "1.0.0")]
[BepInDependency("com.buele.bepinexlauncher")]
public sealed class ExamplePlugin : BaseUnityPlugin
{
}
```

Mod initialization should use UnityModBase game-boot extensions instead of calling global lifecycle entry points again:

```csharp
using UnityEngine;
using UnityModBase.HClassAttribute;

internal static class ModStartup
{
    [InitializeOnGameBoot]
    private static void Initialize()
    {
        // Register the user, configuration, logging, and live controls.
    }
}

[RegisterOnGameBoot]
internal sealed class RuntimeComponent : MonoBehaviour
{
}
```

- An `InitializeOnGameBoot` method must be non-generic, static, parameterless, and return `void`.
- A type marked with `RegisterOnGameBoot` must derive from `UnityEngine.Component`.
- The launcher already owns `Initialize`, `Boot`, and `Dispose`; a mod must not call these entry points again.
- If a mod is unloaded independently, it should remove static event subscriptions and release its user context with `UserManager.RemoveUser`.

See the [core-library README](../UnityModBase/README_EN.md) for a complete integration example and the [API documentation](../UnityModBase/API_EN.md) for member details.

## Custom hosts

Do not install this launcher when BepInEx is not used or when the host needs to define a different game-boot boundary. A custom host can reference `UnityModBase.dll` directly and drive `Initialize`, `GameBootRegistry.Boot`, and `Dispose` as described by the core-library documentation.
