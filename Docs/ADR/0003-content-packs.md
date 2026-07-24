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

## 运行时定义限制

不得包含：

- MonoBehaviour
- GameObject / Transform
- Scene 对象
- Sprite / AudioClip / Animator
- 编辑器专用对象

视觉和音频通过独立 Profile 及 Addressables 地址解析。

## 后果

优点：内容可验证、可打包、可迁移并支持长期扩展。  
代价：需要 Baker、Registry、Schema 版本、缺失内容恢复和编辑器工具。
