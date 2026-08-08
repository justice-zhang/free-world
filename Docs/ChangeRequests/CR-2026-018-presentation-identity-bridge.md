# Change Request：Active Delivery 与 Affix 表现身份只读桥接

- 编号：CR-2026-018
- 状态：Accepted
- 提交日期：2026-08-09
- 提交人：Codex
- 目标里程碑：G2.7、M13
- 关联 ADR：ADR 0025

## 1. 变更摘要

把内容中已存在的 Skill Delivery `PresentationId` 与 Enemy Affix 稳定 ID 通过 Simulation/Application
只读查询暴露给 Presentation。View 继续只消费纯值身份，不读取 Store 或拥有玩法真值。

## 2. 现有模块为何不足

`RunSession.TryGetVisualProfileId` 只解析敌人基础 VisualProfile；Projectile/Area 的 Delivery Profile 和
敌人实例在 Spawn 时冻结的 Affix 组合无法到达 View。仅按 EntityKind 着色会让 Boss 前摇、种囊范围和
四 Affix 失去形状/优先级表达，也浪费已经 Bake 的稳定 ID。

## 3. 提议方案

- `SkillRuntime.TryGetDeliveryPresentationId`：只返回 active delivery 已编译等级中的稳定 ID。
- `EnemyRuntime.TryGetAffixId` / `GetAffixCount`：把既有 internal 查询公开为只读 API。
- `RunSession.TryGetVisualProfileId` 扩展 Projectile/Area/Pickup；新增 `TryGetVisualOverlayId`。
- Profile 解析、色觉变体、Sprite、VFX 和 Audio 仍只存在 Presentation/Infrastructure。

## 4. 影响

| 领域 | 影响 |
|---|---|
| Assembly | 方向不变；Application 仍单向读取 Simulation |
| Content Schema | 不变，继续消费 Schema 3/6 已有字段 |
| Save | 不变，不保存运行时句柄或 Profile 解析结果 |
| Tick | 不变；查询只在 View Acquire/Overlay 绑定时调用 |
| API Freeze | Simulation 追加 3 条、Application 追加 1 条，删除 0 |
| 回滚 | 停止调用即可；公开追加项保留以维持二进制兼容 |

## 5. 备选方案

按具体技能/敌人 ID 在 Host 猜测表现会形成硬编码并无法表达实例 Affix；让 View 读取 EnemyRuntime/
Delivery Storage 会暴露可变真值，均拒绝。

## 6. 验收

- [x] 不暴露 Store、ActiveDeliveryRecord 或 Affix 可变对象
- [x] 不改变 Content/Save/Tick
- [x] 真实 Projectile/Area 稳定 Profile 与 Affix Overlay 通路可达
- [x] API diff、完整测试、Validation、性能与 Development Build 通过

## 7. 审批

- 结论：Accepted；依据用户连续开发、全部自行决策与免确认授权
