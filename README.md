# SecretFlasherManaka For EveryThing

这是独立的 **BepInEx 6 IL2CPP 游戏状态插件**，发布振动与活塞两路纯 CLR 快照，供其他插件消费。它只读取游戏状态，不引用 BooBoopControl、不包含硬件档位映射，也不启动原厂程序或设备。

仓库目录为 `SecretFlasherManaka-ForEveryThing`；C# 项目、程序集及命名空间是 `SecretFlasherManaka.ForEveryThing`；显示名为 **SecretFlasherManaka For EveryThing**。产品版本 **1.1.0**，AssemblyVersion **1.0.0.0**，插件 GUID **local.seleka.gamesignals**，两套 Hub 的 API 主版本均为 1。名称 For EveryThing 不代表已经适配其他游戏。

## 当前能力

| 能力 | 当前实现 |
| --- | --- |
| 振动观察 | Hook 精确 CommonVibratorController.Update，只接受当帧启用的有效 Leader，读取实际 VibrationStrength |
| 振动模式 | Unknown / Off / Low / High；Random 依赖游戏计算结果，不自行随机或映射成 High |
| 活塞观察 | 读取精确 VibeStatePanelView.currentPistonMode 的 UI 缓存，发布 Unknown / Off / Slow / Medium / Fast |
| 状态事件 | 每次观察发布 Observed；只有语义变化才发布 StateChanged |
| 生命周期 | 暂停、场景变化、来源禁用、关闭时发布无效状态和对应停止屏障 |
| 来源隔离 | Leader 丢失只使振动失效；活塞绑定或观察失败只使活塞失效 |
| 诊断 | 输出 Hook 到达、Leader 接受、LateUpdate、帧间隔及活塞缓存状态 |

当前没有通用游戏适配、完整道具识别、硬件控制、活塞运动反馈或游戏执行器写入 Hook。历史“道具 A 可动、道具 B 未动”的原因仍未定位，本实现不声称修复。

## 实现方案与源码结构

```text
SecretFlasherManaka-ForEveryThing/
  SecretFlasherManaka.ForEveryThing.csproj
  src/
    Plugin.cs          # 插件、Harmony postfix、统一 LateUpdate 与生命周期
    GameBinding.cs     # 精确振动类型/成员校验与静态读取
    GameStateHub.cs    # 振动快照、原因、事件
    PistonBinding.cs   # 活塞 UI 成员、IL2CPP wrapper 与面板缓存
    PistonStateHub.cs  # 活塞快照与事件
```

振动绑定精确类型 `ExposureUnnoticed2.Object3D.AdultGoods.CommonVibratorController`，验证静态 Leader、VibrationStrength 的类型和游戏枚举 Off=0 / Low=1 / High=2 / Random=3，以及实例 Update 的无参数 void 签名。生成的 interop 成员可以是字段或属性，读取器加载时缓存。签名不兼容会关闭整个插件，不猜测替代来源。

Harmony postfix 只记录当前 Leader 的当帧观察，随后由生命周期组件的 LateUpdate 验证 Leader 是否仍启用、仍是同一对象、观察帧是否等于当前帧，再发布快照。读到仍为 Random 的值会发布 Unknown，而不是假定已获得有效强度。

活塞绑定精确类型 `ExposureUnnoticed2.ObjectUI.InGame.VIbeStatePanel.VibeStatePanelView`，校验 `currentPistonMode` 为 Int32，缓存 IL2CPP 类型、IntPtr wrapper 构造器、成员读取器及面板对象。找不到有效面板时，最多每秒 Find 一次；查找阶段发现多个启用面板会拒绝选择。有效缓存逐帧读取，允许 persistent / additive UI，不要求面板属于 active scene。

新找到或观察到重新启用的面板需要等待后续帧。场景、暂停和来源启停会使缓存失效；活塞绑定不兼容不影响振动，活塞观察抛异常则本次会话不再尝试恢复活塞绑定。游戏观察器整体异常会发布 ShuttingDown 并卸载 Hook。

## 公共 API 与事件契约

| 振动 API | 活塞 API | 含义 |
| --- | --- | --- |
| GameStrength | PistonMode | 游戏语义枚举，不是硬件档位 |
| GameStateSnapshot | PistonStateSnapshot | 不包含 Unity 对象的 readonly record struct |
| GameStateHub.Current | PistonStateHub.Current | 当前快照，适合显示 |
| StateChanged | StateChanged | Action<Snapshot>，仅语义变化触发 |
| Observed | Observed | Action<Snapshot, bool>，每次观察触发，bool 是 forceStop |
| ApiMajorVersion | ApiMajorVersion | 当前都为 1 |

`GameStrength`：Unknown=-1、Off=0、Low=1、High=2。`PistonMode`：Unknown=-1、Off=0、Slow=1、Medium=2、Fast=3。

快照包含 Active、Strength 或 Mode、Reason、Revision、ObservedAt。Active 表示观察有效；**Active=true 且 Off 是正常关闭状态**，Active=false 且 Unknown 是失效状态。Reason 为 Normal、LeaderUnavailable、SceneChanged、Paused、ShuttingDown、SourceDisabled、SourceUnavailable 或 InvalidValue。

Revision 只在 Active、模式或 Reason 改变时递增；ObservedAt 每次发布刷新，来自 `Stopwatch.GetTimestamp()`，不是 UTC 时间。稳定模式也会持续产生 Observed，这提供观察心跳，不要求消费者重复发送动作。发布顺序为 Observed，然后在有变化时 StateChanged；坏订阅者不会阻断其他订阅者。

暂停、场景切换、整个来源组件禁用及关闭用 forceStop=true 通知两路。单路来源失效通常发布该路无效快照；消费者决定如何停止对应输出。恢复后需等待新帧，不能读取 Current 来重放连接前状态。

消费者引用 DLL，并在 BepInEx 插件上声明依赖：

```csharp
[BepInDependency("local.seleka.gamesignals", ">=1.1.0 <2.0.0")]
```

加载时订阅，卸载时解除。事件在 Unity 主线程同步调用；回调应只缓存纯数据并快速返回，不能同步 I/O 或调用 Wait() / Result。持续动作消费者需要 Observed 心跳和停止屏障，不能只订阅 StateChanged。

## 构建与单独部署

在本项目目录执行：

```powershell
$GameDir = Read-Host '输入包含 BepInEx 的游戏安装目录绝对路径'
dotnet build .\SecretFlasherManaka.ForEveryThing.csproj -c Release "-p:GameDir=$GameDir"
```

需要可编译 net6.0 的 SDK 和目标游戏原有 `BepInEx/core`、`BepInEx/interop` 引用。GameDir 也可通过 `BOOBOOP_GAME_DIR` 提供。既有适配目标是 Windows x64、BepInEx 6.0.0-be.735 IL2CPP、Unity 2022.3.62f2、游戏运行时 .NET 6.0.7；不要为编译替换游戏加载器或 interop。

输出 `bin/Release/net6.0/SecretFlasherManaka.ForEveryThing.dll`。只读取游戏状态时，退出游戏后单独复制此 DLL 到 `BepInEx/plugins/SecretFlasherManakaBooBoop/` 即可，不需要硬件或 Bridge DLL。不要复制系统、Unity、Harmony 或 BepInEx 依赖。旧版若使用相同 GUID，需要移出 plugins 树以避免重复加载。

查看 `BepInEx/LogOutput.log` 中的 `[Game]` 和 `[Game/Piston UI cache]`。Hook 安装、postfix 到达、Leader 接受和有效快照是不同阶段，应分别确认。

## 代码审查发现与验证状态

1. **UI 缓存没有底层更新时间。** 面板仍启用时，相同的旧值可以不断获得新的 ObservedAt；心跳只证明正在读取面板，不证明游戏执行器仍运行。Bridge 的一秒超时不会检测这种“缓存值停滞”。
2. **唯一性校验只在查找阶段执行。** PistonBinding.Observe 在缓存仍有效时不再枚举面板；若同一场景之后增加第二个启用面板，仍会沿用旧面板，直到缓存失效并触发查找。需要进一步验证 additive UI 等实际场景；若要求持续唯一性，应增加生命周期感知或有界复核。
3. 精确反射类型、成员及 IL2CPP wrapper 是版本耦合点。编译只能验证当前引用能使用这些 API，不能证明运行时 Hook、字段读取或 UI 生命周期正确。

2026-10-07 三项目 Release 构建成功，0 错误；游戏源码有 6 项既有可空引用警告。**47/47 mock 通过**，覆盖两套纯 CLR Hub、消费者状态机及安装路径解析。

## 仓库入口与复用

在本仓库运行 build.ps1 -GameDir 你的游戏目录，或 bash build.sh 你的游戏目录。仓库名称使用横线，程序集使用点号；本次目录更名没有修改游戏 API 或插件 GUID。

ForEveryThing 是艺术名称，当前提供 SecretFlasherManaka 的振动和活塞观察接口。不同硬件 Bridge 都可以引用该 DLL，使用相同的 Observed 心跳与停止契约；游戏项目不接纳具体硬件的协议编号。程序集引用之外，还需在消费者插件上声明游戏 GUID 依赖。

## 许可证

[MIT](LICENSE)，Copyright (c) 2026 Lu_Noodles。外部软件与引用范围见 [第三方说明](THIRD_PARTY_NOTICES.md)。
