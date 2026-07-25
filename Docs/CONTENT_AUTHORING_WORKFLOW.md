# 内容新增与烘焙流程

## 1. 适用范围

本文记录 M1 已实现的 Character、Skill、Enemy 和 Map 最小作者数据流程。它只生产
内容元数据，不实现实体、战斗、技能执行、刷怪或地图运行时。

## 2. 新建 Pack

1. 在 Placeholder 或已完成 provenance 审核的正式内容目录创建
   `ContentPackAuthoring`。
2. 设置 canonical `packId`、严格 `major.minor.patch` 版本、Schema 1、游戏版本范围、
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
定义和 SHA-256 Hash。重新 Bake 相同输入应产生相同 Hash。

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
