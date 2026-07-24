# Steam 集成边界

## 目标

Steam 是可替换平台适配器，不是游戏核心依赖。

## 初始接口

- Achievements

- Stats

- Cloud Sync

- Rich Presence

- User Identity

- Optional Leaderboards

## 规则

- M8 只实现接口、Null 实现和数据模型，不引入真实 Steam SDK。

- 真实 SDK 集成在框架冻结后单独执行。

- 本地存档始终可用。

- 云冲突必须有明确策略，不静默覆盖更新文件。

- 成就触发来自应用事件或统计，不允许战斗系统直接调用 SDK。

- Steam Assembly 不得被 Simulation、Content.Runtime 或 Core 依赖。
