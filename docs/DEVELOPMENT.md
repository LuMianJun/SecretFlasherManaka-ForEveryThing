# 开发与接入

本插件通过游戏状态事件为硬件桥接或其他插件提供数据。

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

