# SecretFlasherManaka For EveryThing

面向 **SecretFlasherManaka** 的 BepInEx IL2CPP 插件，将游戏内的振动和活塞状态提供给其他插件，让不同硬件桥接可以共用同一套游戏接口。

## 当前能力

- 读取游戏振动状态：关闭、低档、高档。
- 读取游戏活塞模式：关闭、慢速、中速、快速。
- 提供状态变化事件和持续观察事件，方便桥接插件跟随游戏状态。
- 在暂停、场景切换、状态来源失效或退出时通知桥接插件停止输出。

## 安装与使用

需要 Windows x64 和游戏已有的 **BepInEx 6 IL2CPP** 环境。当前适配环境为 BepInEx 6.0.0-be.735、Unity 2022.3.62f2。

1. 从 [BooBoopBridge Release](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge/releases/latest) 下载安装包，或自行构建。
2. 退出游戏，将 `SecretFlasherManaka.ForEveryThing.dll` 放入游戏目录下的 `BepInEx/plugins/SecretFlasherManakaBooBoop/`。
3. 安装你需要的硬件桥接插件，然后启动游戏。

使用 BooBoop 设备时，直接按 [BooBoopBridge 的安装说明](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge#readme) 安装完整包即可，其中已包含本插件。单独安装本插件时，只提供游戏状态；设备连接和动作控制由桥接插件负责。

更新前移出旧版本 DLL，避免重复加载。运行日志位于 `BepInEx/LogOutput.log`。

## 支持的状态

| 类型 | 状态 | 接口 |
| --- | --- | --- |
| 振动 | Off / Low / High | `GameStateHub` |
| 活塞 | Off / Slow / Medium / Fast | `PistonStateHub` |

状态不可用时提供 `Unknown`。活塞模式来自游戏 UI 的状态缓存，表示游戏模式。

## 本地构建

需要支持 .NET 6 的 SDK，以及游戏目录中的 `BepInEx/core` 和 `BepInEx/interop` 引用。

在本仓库目录执行，将路径替换为你的游戏安装目录：

```powershell
.\build.ps1 -GameDir 'D:\Games\SecretFlasherManaka'
```

输出文件：

```text
bin/Release/net6.0/SecretFlasherManaka.ForEveryThing.dll
```

也可以通过环境变量 `BOOBOOP_GAME_DIR` 指定游戏目录。

## 开发与接入

其他硬件桥接可以引用本插件，订阅振动和活塞事件，再转换为对应设备的控制指令。

API、事件使用方式及实现说明见 [开发文档](docs/DEVELOPMENT.md)。源码位于 `src/`，插件入口为 `Plugin.cs`。

## 相关项目

- [SecretFlasherManaka-BooBoopBridge](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge)：游戏与 BooBoop 设备的整合桥接及完整安装包。
- [BooBoopControl](https://github.com/LuMianJun/BooBoopControl)：BooBoop 设备控制库，当前支持 FN010-RX。

## 许可证

[MIT](LICENSE)，Copyright (c) 2026 Lu_Noodles。第三方软件与依赖范围见 [第三方说明](THIRD_PARTY_NOTICES.md)。
