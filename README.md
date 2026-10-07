# SecretFlasherManaka For EveryThing

面向 **SecretFlasherManaka** 的 BepInEx IL2CPP 插件，为其他插件和硬件桥接提供统一的游戏状态接口。

## 当前能力

- 提供状态变化事件和持续观察事件，方便桥接插件跟随游戏状态。
- 在暂停、场景切换、状态不可用或退出时通知桥接插件停止输出。

## 安装与使用

需要 Windows x64 和游戏已有的 **BepInEx 6 IL2CPP** 环境。当前适配环境为 BepInEx 6.0.0-be.735、Unity 2022.3.62f2。

1. 按下方构建方法生成 `SecretFlasherManaka.ForEveryThing.dll`。也可以从 [BooBoopBridge Release](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge/releases/latest) 的安装包中提取此 DLL。
2. 退出游戏，将 `SecretFlasherManaka.ForEveryThing.dll` 放入游戏目录下的 `BepInEx/plugins/SecretFlasherManakaForEveryThing/`。
3. 启动游戏。需要使用状态数据的插件可自行接入本插件接口。

本插件可独立安装，并供状态显示、日志记录、自动化或硬件桥接等插件使用。使用 BooBoop 设备时，可按 [BooBoopBridge 的安装说明](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge#readme) 安装完整包，其中已包含本插件。

更新前移出旧版本 DLL，避免重复加载。运行日志位于 `BepInEx/LogOutput.log`。

## 支持的状态

| 类型 | 状态 | 接口 |
| --- | --- | --- |
| 振动 | Off / Low / High | `GameStateHub` |
| 活塞 | Off / Slow / Medium / Fast | `PistonStateHub` |

状态不可用时提供 `Unknown`。

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

其他插件可以引用本插件并订阅游戏状态事件，按自身需求处理数据。硬件桥接可以将状态转换为设备控制指令，也可以接入其他硬件控制库。

API、事件使用方式及实现说明见 [开发文档](docs/DEVELOPMENT.md)。源码位于 `src/`，插件入口为 `Plugin.cs`。

## 接入示例

- [SecretFlasherManaka-BooBoopBridge](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge)：使用本插件接入 BooBoop 设备，提供完整安装包。

## 许可证

[MIT](LICENSE)，Copyright (c) 2026 Lu_Noodles。第三方软件与依赖范围见 [第三方说明](THIRD_PARTY_NOTICES.md)。
