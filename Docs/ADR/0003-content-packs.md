# ADR 0003：稳定 ContentId、内容包与烘焙运行时目录

- 状态：Accepted
- 日期：2026-07-24
- 决策人：待填写

## 背景

项目需要长期增加角色、技能、构筑、敌人和地图，并支持存档迁移、DLC 和可能的官方内容包。直接依赖 ScriptableObject 引用、数组下标或 enum 会让内容顺序变化破坏存档，也会使验证和打包困难。

## 决策

- 所有长期内容使用命名空间字符串 `ContentId`，例如 `base.skill.arc_bolt`。
- 已发布 ID 不得修改或复用。
- 作者使用 ScriptableObject 配置内容。
- 构建前由 Validator 和 Baker 转换为纯运行时定义。
- 运行时目录建立 `ContentId → RuntimeContentIndex` 映射。
- 高频路径使用整数索引；存档、日志和跨版本引用使用稳定 ID。
- 内容以 `ContentPack` 组织，Manifest 包含版本、Schema、依赖、Catalog 地址和内容 Hash。
- 首阶段不允许两个包声明相同 ID。
- 内容包依赖必须验证并拓扑排序。
- `ContentId` 规范化外部输入，但作者资产必须已是 canonical 小写形式；比较和
  序列化始终使用完整字符串，Hash 碰撞不改变身份。
- Pack 和依赖版本使用严格 `major.minor.patch`，minimum/maximum 均为 inclusive。
- 拓扑排序对无依赖先后关系的 Pack 保留输入顺序。
- Registry 采用验证后原子替换，不允许部分加载或最后加载覆盖。
- Baked Catalog 内容 Hash 使用固定字段顺序和 SHA-256，并在加载时重新计算。

## 运行时定义限制

不得包含：

- MonoBehaviour
- GameObject / Transform
- Scene 对象
- Sprite / AudioClip / Animator
- 编辑器专用对象

视觉和音频通过独立 Profile 及 Addressables 地址解析。

磁盘 Catalog 使用显式 DTO，把 ID 和版本保存为字符串。Unity `TextAsset` 仅由
Composition Root 持有，解析后得到的 Manifest、定义、Validator 和 Registry 均位于
`noEngineReferences` 的程序集。

## M1 落地边界

- `Game.Core`：`ContentId`、`ContentTag`、`ContentVersion`、
  `RuntimeContentIndex`、`Result`、`Error`。
- `Game.Content.Authoring`：五类最小 ScriptableObject 和 `ContentBaker`。
- `Game.Content.Runtime`：Manifest、拓扑、纯定义、Catalog、Hash、Validator、Registry。
- `Game.Editor`：显式查找 `ContentPackAuthoring` 资产、Bake、命令行/构建前验证。
- `Game.Infrastructure`：只负责把 Scene 中显式引用的 baked TextAsset 转成 DTO 并
  交给 Application；不持有作者 ScriptableObject。

新运行时定义只需继承 `RuntimeContentDefinition` 即可进入 Registry；Registry 不包含
角色、技能、敌人或地图的类型分支。磁盘 DTO Codec 对新增定义类型的支持是显式的
Schema 变更，必须按 ADR/迁移流程处理。

## 后果

优点：内容可验证、可打包、可迁移并支持长期扩展。  
代价：需要 Baker、Registry、Schema 版本、缺失内容恢复和编辑器工具。
