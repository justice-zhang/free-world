# 07 G0.2 Change Request 决策

- 决策日期：2026-08-04
- 决策范围：`CR-01`—`CR-11`
- 授权来源：用户当前“按 Demo 文档顺序持续开发”指令
- 实现状态：全部为设计决定；除既有框架能力外尚未实现

## 1. 决策矩阵

| Demo CR | 决策 | 正式 Change Request | G0.3 结果 |
|---|---|---|---|
| CR-01 角色机制资源与真实位移 | `ACCEPTED` | `CR-2026-004` | Character Mechanic、移动来源、资源档位 ADR/Schema/API 契约 |
| CR-02 回返/多段轨迹 | `ACCEPTED` | `CR-2026-005` | Returning Trajectory Delivery 契约与命中去重 |
| CR-03 状态条件、计数、消费、引爆 | `ACCEPTED` | `CR-2026-006` | 模块引用操作数、状态查询/消费事务和 TriggerPosition 契约 |
| CR-04 关键掉落资格与受控 Evolution | `ACCEPTED` | `CR-2026-007` | Reward Choice Context 与 BuildState 资格事务 |
| CR-05 泛化 Pickup/Reward/Relic | `ACCEPTED` | `CR-2026-008` | Reward/Relic/Pickup 定义、操作码、随机流和幂等契约 |
| CR-06 地图目标/交互/事件 | `ACCEPTED` | `CR-2026-009` | Objective/Event/Landmark Schema 与 Runtime |
| CR-07 Boss 阶段、抗性和地图修正 | `ACCEPTED` | `CR-2026-010` | Boss Phase/Rule/Resistance 契约 |
| CR-08 精英词缀组合 | `ACCEPTED` | `CR-2026-011` | Elite Affix Schema、组合验证与奖励输出 |
| CR-09 局外内容与 Loadout | `ACCEPTED` | `CR-2026-012` | Meta Definition、Profile v3、Loadout 与幂等首通事务 |
| CR-10 缺失公共属性/伤害规则 | `SPLIT` | `CR-2026-013`、`CR-2026-014` | 公共 Stat 扩展与 Damage Policy 分开设计/验证 |
| CR-11 完整 Run Recovery | `DEFERRED` | `CR-2026-015` | Demo 不提供“继续本局”；只检测、提示并清理不完整记录 |

## 2. 接受原则

- `ACCEPTED` 只授权进入 G0.3 的 ADR、Schema/API、迁移和测试契约设计，不等于代码已实现。
- 所有新机制必须服务至少两个消费者，使用稳定 wire token、显式注册和运行前紧凑绑定。
- Content Schema 1—5、Save Schema 2 和已发布 ContentId 必须保持兼容读取。
- 冻结公共 API 只能在对应 ADR、兼容方案和完整门禁就绪后更新 Hash。
- 所有结构创建/删除继续由 Cleanup 应用；固定 Tick 热路径不做字符串查找、反射或临时集合分配。

## 3. CR-10 拆分理由

公共属性增加会改变 `Game.Core`/StatCatalog 的冻结 API，但不需要改变伤害结算协议；接触伤害保护、
伤害屏障和分通道受击冷却会改变 `DamagePacket`/DamageResolution 语义。二者的迁移、性能风险和回滚
不同，因此拆为：

- `CR-2026-013`：ProjectileSpeed、CriticalMultiplier、ExperienceGain、KnockbackResistance 等 Stat；
- `CR-2026-014`：DamageChannel、按通道受击冷却、免伤/屏障和零伤害事件语义。

## 4. CR-11 延期边界

Demo 的 DOD 不要求任意 Tick 继续本局。G2/G3 只允许：检测到 `run_recovery.json` → 显示本地化提示 →
明确开始新局并清理旧记录。不得显示“继续”按钮，不得把不完整 Run 提交为胜利或首通。

若以后恢复 CR-11，必须重新评审随机流计数、Entity/Handle 重建、目标/Boss/奖励事务、版本迁移、
中断写入和长局 Fixture；不能直接序列化 `SimulationWorld`。

## 5. G0.3 输入顺序

1. 先统一 Content Schema 6 的新增 kind、模块操作数和向后兼容规则；
2. 再确定 Simulation Pipeline、所有者、事件、随机流和 Cleanup 边界；
3. 再确定 Profile Save Schema 3 及 v2→v3 迁移；
4. 最后形成 ADR、公共 API Freeze 变更计划、测试矩阵和回滚顺序。

G0.3 未完成前，G1/G2 只能继续使用现有纯内容能力，不得修改冻结核心程序集。

## 6. G0.3 结果（2026-08-04）

G0.3 已通过 ADR 0013、0014、0015 和
[08_G0_3_CONTRACT_FREEZE.md](08_G0_3_CONTRACT_FREEZE.md) 固化 Schema 6、公共 API 最大面、24 项
Pipeline、Profile 3、迁移、回滚和测试矩阵。实现仍从 G1.1 开始；现有冻结 Hash 尚未改变。
