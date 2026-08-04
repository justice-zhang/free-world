# Change Request：通用角色机制资源与真实位移

- 编号：CR-2026-004
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.1、G1.6
- 关联 ADR：待 G0.3；对应 Demo CR-01

## 1. 变更摘要

新增数据驱动的角色机制资源、档位效果与真实位移来源，使位移蓄势、资源消耗和受伤损失可被多个角色复用，而不把青岚或具体技能写入核心系统。

## 2. 触发场景

- 用户或设计需求：角色位移产生“岚势”，不同档位修改战斗输出，受伤会损失资源。
- 当前限制：现有 Stat、Trait 和 Trigger 只能表达结果，不能可靠识别已结算位移、资源档位和受伤事务。
- 可复现示例：仅按输入累计会把撞墙、传送和击退误算成主动位移。

## 3. 现有模块为何不足

Trigger、Effect 与 Trait 可组合档位奖励，但缺少角色级资源所有者和 `MovementSource`。使用角色 ContentId 分支会破坏内容扩展规则。

## 4. 提议方案

- 新增或修改的模块：Character Mechanic Runtime、Movement Resolution 和 Damage Event 适配。
- 公共 API：类型化 `MovementSource`、资源档位快照和档位变化事件。
- 数据结构：Schema 6 `CharacterMechanicDefinition`，包含资源 ID、容量、增长/损失规则、档位阈值与输出。
- 注册方式：按稳定模块 ID 注册并在加载期绑定紧凑索引。
- 编辑器工作流：Authoring 校验阈值递增、输出合法且消费者存在。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | `Game.Simulation` 扩展；`Game.Core` 只承载纯值契约 |
| Content Schema | 升至 6，新增 Character Mechanic 定义 |
| Save Schema | 不保存运行时资源；Profile 不变 |
| Addressables | 沿用 Content Pack |
| 性能 | 每玩家固定大小 sidecar；仅移动/受伤时更新 |
| 测试 | 位移来源、撞墙、档位边界、受伤损失和确定性 |
| 平台 | 无影响 |
| 资产与许可 | 无新外部资产 |
| 兼容性 | Schema 1—5 以无角色机制默认值读取 |

## 6. 备选方案

用 Trait 轮询位置差最简单，但不能区分位移来源且会累计误差，因此不采用。

## 7. 迁移与回滚

- 迁移步骤：Schema 6 读取器为旧角色填充空机制；更新 API Freeze 后再实现。
- 旧数据处理：旧 Pack 无定义时保持原行为。
- 回滚步骤：移除新定义和 sidecar，恢复空机制默认值；旧内容无需改写。

## 8. 验收标准

- [ ] 至少两个 Fixture 共享机制定义
- [ ] 不出现具体角色或技能 ID 分支
- [ ] EditMode 覆盖来源、档位和受伤事务
- [ ] 热路径零临时托管分配
- [ ] ADR、Schema、API Freeze 已更新
- [ ] Schema 1—5 兼容测试通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
