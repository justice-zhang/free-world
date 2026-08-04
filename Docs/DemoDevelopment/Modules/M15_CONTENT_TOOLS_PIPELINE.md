# M15 内容工具与生产管线

## 1. 目标

让内容人员新增/修改 Demo 内容时复用现有向导、Validator、Baker、Timeline、Skill Preview 和 Pack
Builder；EditorWindow 只做薄 UI，不复制运行时公式。

## 2. Authoring 流程

```text
Create from approved template
→ assign DRAFT/RESERVED ContentId
→ add localization keys
→ add Placeholder or formal Profile references
→ validate one asset and references
→ bake whole pack
→ validate Catalog hash and dependencies
→ run skill/timeline/meta preview
→ run tests
→ build Development/Release as applicable
```

正式资源替换 Placeholder 是新工作包，需要 provenance/Addressables/Localization 审查，不在内容数值 PR
中顺手替换。

## 3. 向导扩展

如 CR 获批，向导需支持 CharacterMechanic、Reward、MapObjective、MapEvent、BossPhase、EliteAffix、
MetaNode、Insert、Collectible、Story、Facility；每种生成：同名资产、稳定 Key、测试模板、来源占位、
正确 Pack/标签和 Bake 入口。

向导不得生成正式内容或默认 `release` 标签；默认只生成程序化 Placeholder 与 development-only。

## 4. 验证器

| 领域 | 新检查 |
|---|---|
| ID/Pack | canonical、重复、依赖、版本、发布状态 |
| Skill | 等级、模块、参数、Secondary 循环、实体预算、Profile |
| Build | 显化可达、宝匣 fallback、槽位、Synergy 输出 |
| Enemy/Affix | 技能/行为、互斥、分裂代数、危险子预算 |
| Map | Anchor、Walkable、目标/事件/地标状态和奖励 |
| Boss | 阶段可达、修正输入、技能/Profile、清理策略 |
| Meta | 拓扑、成本、容量、互斥、Loadout、Save 类型 |
| Localization | zh-Hans/en/Pseudo Key 和字体 |
| Assets | Address、Release 标签、provenance、Third Party、Hash |

错误必须包含稳定错误码、Pack、ContentId、作者资产路径和引用链。

## 5. 预览工具

- Skill Preview：6 武器 1/4/8 级、显化前后、不同乘风/心诀/奇物；
- Timeline：12 分钟预算、间隔、权重、并发、XP、Boss/Elite 时间；
- Boss Preview：阶段技能时间线、8 组风脉台参数快照；
- Map Preview：Anchor/Walkable/目标路径/事件候选；
- Build Matrix：三构筑资格、候选可达和实体预算；
- Localization Preview：三 Locale、UI Scale、字体 fallback。

所有数值预览调用同一纯运行时 API，不能在 Editor 重写公式。

## 6. 内容包与版本

Pack Builder 输出 Manifest、Catalog、Content Hash、文件 SHA-256、依赖、标签和审计报告。相同输入两次
输出一致。任何作者内容变更需重 Bake；旧 Catalog Hash 不能手工复制。

版本策略：数值/文案兼容变更 bump patch；新增兼容内容 bump minor；破坏性 Schema 只在获批迁移后 bump
major/SchemaVersion。具体版本由 Release Owner 审核。

## 7. 自动化入口

必须提供非交互命令：Bake、Project Validation、Pack Build、Skill Preview Batch、Timeline Report、
Boss/Build Matrix、Release Gate。CLI 缺输出/标记即 FAIL，不能凭进程 exit 0 宣称成功。

## 8. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| EditMode | 每种 Definition 向导→Bake→Registry |
| Determinism | 两次 Bake/Preview/Timeline Hash 一致 |
| Negative | 缺 ID/引用/Key/Profile/provenance/互斥/循环必失败 |
| CLI | 成功证据与负向非零退出 |
| Extension | 新内容不修改 Core/Simulation 已有内容分支 |
| Release | Placeholder/来源/Third Party/Hash 任一失败都阻断 |

退出条件：内容生产不依赖手工改 JSON/YAML，不存在第二套公式，完整 Demo Pack 可由 CLI 重建和审计。
