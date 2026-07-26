# ADR 0006：模块化技能运行时与 Content Schema 3

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据 M4 当前用户指令

## 背景

M3 已提供统一属性、伤害、状态、护盾和死亡真值，但 M1 的技能定义仍只有冷却元数据，
不能表达 Trigger、Targeting、Delivery、Effect 或等级成长。M4 需要让普通技能由配置组合，
同时保持模拟层不依赖 Unity Object、运行时不扫描程序集、不解析任意字符串路径，并继续
通过 M3 请求缓冲修改伤害和状态。

## 决策

### Content Schema 3

- Schema 3 的可执行技能包含 Tags、Cooldown、ResourceCost、Trigger、Condition、
  Targeting、Delivery、`EffectOp[]` 和已解析的 `SkillLevelPatch[]`。
- Schema 1/2 的旧技能仍按仅含冷却元数据的非可执行定义加载；Schema 3 不接受这种旧形态。
- 作者数据和 JSON 边界保存稳定 ContentId；Registry 完成全包验证和 RuntimeContentIndex
  分配后，再以第二阶段把 ApplyStatus 和 SpawnSecondarySkill 引用绑定为运行时索引。
- EffectOp 的稳定引用与绑定索引同时存在：前者用于 Hash、验证和错误报告，后者只用于
  当前 Registry 生命周期内的高频执行，不得保存。
- 非 Instant Delivery 必须提供稳定 Placeholder/正式表现 ID；模拟不加载或引用表现资产。

### 显式模块注册

- `SkillModuleRegistry.CreateDefault()` 使用直接注册调用安装 Trigger、Condition、Targeting、
  Delivery 和 Effect executor；不使用反射、类型扫描或全局 Service Locator。
- Content 模块稳定 ID 由 `SkillModuleIds` 集中声明。内容验证只接受该白名单；新增底层模块
  必须更新注册、Schema 文档、测试，并按需新增 Change Request/ADR。
- Registry 在构建 `SkillRuntimeCatalog` 时一次性把模块 ID 解析为 executor 引用。固定 Tick
  执行只使用已解析引用和紧凑数值数据，不做字符串查找。

### LevelPatch

- 作者数据只允许显式路径表：Cooldown、ResourceCost、模块数值槽和 `effects[n]` 数值槽。
- Baker/DTO 解码阶段把路径转换为 `SkillPatchTarget + TargetIndex + ValueType`；路径不存在、
  Effect 下标越界或 Float/Integer 类型不匹配时拒绝内容。
- 运行时只把 typed patch 应用到每个预编译等级，不保存或解析路径字符串，也不反射修改对象。
- 等级从 2 开始连续；同等级 patch 按作者顺序应用，等级 N 基于 N-1 的结果继续构建。

### 模拟边界与顺序

- Actor 拥有独立 `SkillInstance`（等级、冷却和 owner），多个实例共享同一个不可变
  `CompiledSkillDefinition`。
- TriggerContext、TargetResult 和 Effect/Delivery 命令使用可复用结构缓冲；二次技能继承
  来源、位置和 ProcDepth，并由 M3 的最大深度保护截断递归。
- M4 默认 Pipeline 固定为：

```text
SkillTrigger
→ Movement
→ SkillDelivery
→ SkillEffectResolution
→ DamageResolution
→ StatusTick
→ Death
→ Lifetime
→ Cleanup
→ EventFlush
→ SnapshotBuild
```

- Trigger 阶段消费 Timer 和上一 Tick 产生的战斗事件；Delivery 在移动后推进 Projectile、
  Area、Aura 和 Orbit；EffectResolution 只把通用命令路由到模拟 API。Damage、ApplyStatus
  和 RemoveStatus 必须进入 M3 缓冲，技能实例与 executor 不直接修改 Health。
- 高频 Delivery 的实体创建集中到 Cleanup；Projectile 使用扫掠线段碰撞，避免一个 Tick
  跨越目标造成穿透。表现层以后只消费稳定 PresentationId 和模拟快照。

### 纯模拟预览

- `SkillPreviewHarness` 使用同一 SkillRuntime、空间网格和固定 Tick 管线，不创建 GameObject。
- 输出固定时间窗口内的 DPS、命中数和触发次数；相同内容、场景和种子必须产生相同结果。
- 预览不是性能基准，也不承诺与后续完整角色构筑 UI 的数值口径完全相同。

## 被拒绝的方案

- 为每个技能建立 MonoBehaviour/Controller：会把内容组合固化为代码并制造高频 Update。
- 运行时按字符串路径反射修改 ScriptableObject：破坏类型安全、确定性和无 Unity Object 边界。
- 每次触发按稳定字符串查找 executor 或内容引用：把作者边界工作带入高频路径。
- Effect 直接写 Health：绕过 M3 的护盾、抗性、事件、死亡和 ProcDepth 规则。
- 为 M4 引入通用 ECS、第三方 DI、Tween 或 Jobs/Burst：没有测量依据，且超出本里程碑。

## 后果

普通技能可通过已有五类模块和 LevelPatch 组合，不需要修改核心程序集或新增 MonoBehaviour；
代价是每个新增底层模块都要维护稳定 ID、显式注册、参数契约和测试。Schema 3 内容必须重新
Bake，Schema 1/2 保持可加载。M8 尚未实现存档，本 ADR 不改变 Save Schema；未来存档只能
保存 Skill ContentId、等级和必要实例状态，不能保存 RuntimeContentIndex 或 executor 引用。
