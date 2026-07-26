# 内容新增与烘焙流程

## 1. 适用范围

本文记录 M1 的 Character/Enemy/Map、M3 Status 及 M4 Skill 的作者数据流程。它只生产
可验证的内容数据和通用行为，不在作者对象中运行模拟逻辑。

## 2. 新建 Pack

1. 在 Placeholder 或已完成 provenance 审核的正式内容目录创建
   `ContentPackAuthoring`。
2. 设置 canonical `packId`、严格 `major.minor.patch` 版本、Schema、游戏版本范围、
   Catalog Address 和唯一 Pack Label。
3. 依赖项写稳定 Pack ID 和 inclusive minimum/maximum 版本；不要依赖加载索引。
4. 创建需要的 `CharacterAuthoring`、`SkillAuthoring`、`EnemyAuthoring` 或
   `MapAuthoring`，所有用户可见字段只填写 Localization Key。
5. 将定义按期望的稳定加载顺序加入 Pack。引用使用作者资产，Baker 只输出稳定 ID。

## 3. ID 规则

- 作者 ID 和 Tag 必须已经是小写 canonical 字符串。
- 允许 `a-z`、`0-9`、点号和段内下划线，至少包含一个点号。
- 发布后不改名、不复用；显示名称与 ID 分离。
- 不以 enum、数组位置或 Hash 代替 ID。

## 4. Bake 与验证

在 Unity 菜单执行：

```text
Tools > Free World > M1 > Bake All Content Packs
Tools > Free World > Validate Project
```

每个 Pack 在作者资产旁生成 `<PackName>.baked.json`。JSON 保存 Manifest、纯运行时
定义和 SHA-256 Hash。重新 Bake 相同输入应产生相同 Hash。Schema 选择规则为：不含
状态的既有 M1 Pack 可继续使用 Schema 1；包含 StatusEffectAuthoring 的 Pack 必须使用
Schema 2，不得把状态字段静默写入 Schema 1。

命令行执行：

```powershell
$env:UNITY_PATH = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\validate.ps1
```

验证会重新 Bake 内存 Catalog，并检查：

- 作者 ID/Tag、数值、Localization Key 和资产路径；
- 在解析定义间引用前预验证每个作者资产自身的 ID，确保错误归因到实际资产；
- baked JSON 可解析、Hash 正确且与当前作者输入一致；
- Pack/依赖版本、缺失依赖、循环和重复 Pack；
- 跨 Pack 重复 ContentId 与缺失内容引用；
- M0 第三方、AI provenance 和 Release/Placeholder 门禁。

任何失败都会返回非零退出码，并记录 ContentId、PackId 和作者资产路径；不得通过重排
加载顺序覆盖错误。

## 5. M1 测试包

执行：

```text
Tools > Free World > M1 > Configure Test Content
```

会重建 `Assets/GameAssets/Placeholder/TestContent` 中的最小测试包、重新 Bake，并把
JSON 显式赋给 Bootstrap Scene。测试包是开发验证 Fixture，不能添加 `release` 标签。

## 6. 新内容类型

新增纯运行时类型继承 `RuntimeContentDefinition` 后，Registry 无需修改。若该类型需要写入
baked JSON，则属于 Content Schema 变化：先更新 ADR、DTO Codec、Hash 字段顺序、迁移策略
和测试，再接受该类型。不得使用反射扫描自动注册类型。

## 7. M3 状态作者数据

`StatusEffectAuthoring` 必须填写：

- 四种稳定叠层策略之一、正 Duration、合法 MaxStacks 和非负 TickInterval；
- canonical DispelTags 与 ImmunityTags；
- 可选 Modifier、周期伤害和临时 ShieldCapacity；
- 所有名称与说明只填写 Localization Key。

周期伤害行为要求正 TickInterval、合法 DamageType/Tags、有限非负伤害、`[0,1]`
ProcCoefficient 和有限 Knockback。Modifier 要求稳定 StatId、合法 Operation、有限 Value
以及可选 canonical StackingGroup。状态行为会写入 Runtime Definition；运行时申请只
提供 StatusIndex、来源、Strength 和 ProcDepth，不能覆盖行为。

创建或重建 M3 测试 Fixture：

```text
Tools > Free World > M3 > Configure Test Status Content
```

命令行入口：

```powershell
& $env:UNITY_PATH -batchmode -nographics -projectPath <project> `
  -executeMethod Game.Editor.M3TestStatusSetup.RunFromCommandLine
```

## 8. M4 模块化技能作者数据

可执行 `SkillAuthoring` 必须位于 Schema 3 Pack，并填写：

- 非负有限 Cooldown 和 ResourceCost；
- 已登记的 Trigger、Condition、Targeting、Delivery 模块 ID；
- 至少一个已登记 Effect；
- 非 Instant Delivery 的稳定 PresentationId；
- ApplyStatus/SpawnSecondarySkill 的作者资产引用；
- 从等级 2 开始连续的 LevelPatch。

模块的 Value/Int 槽语义和稳定 ID 以 `Docs/EFFECT_MODULES.md` 为唯一登记表。LevelPatch 路径
必须使用该文档的显式路径表；Baker 会验证路径、Effect 下标和 Float/Integer 类型，再写为
typed slot。不得用路径指向任意字段，也不得依赖运行时反射。

创建或重建四个 M4 Placeholder Fixture：

```text
Tools > Free World > M4 > Configure Test Skill Content
```

命令行入口：

```powershell
& $env:UNITY_PATH -batchmode -nographics -projectPath <project> `
  -executeMethod Game.Editor.M4TestSkillSetup.RunFromCommandLine
```

输出位于 `Assets/GameAssets/Placeholder/TestSkillContent`，包含单体投射物、环绕物、地面区域
和伤害光环及 Schema 3 baked JSON。它们只使用 `placeholder.presentation.*` ID，不创建 Prefab、
VFX、音频或专用 MonoBehaviour，不得加入 release label。
