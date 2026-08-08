# 框架完成 Definition of Done

只有以下项目全部满足，才允许开始大规模正式内容生产。

## 代码与架构

> ☐ 项目不包含参考开源游戏的美术、音频、Prefab、Scene、动画或品牌资源。
>
> ☐ Game.Core 不引用 UnityEngine。
>
> ☐ Game.Simulation 不引用 Scene、Prefab、MonoBehaviour 或表现资源。
>
> ☐ 高频敌人和投射物没有逐实体 MonoBehaviour 更新。
>
> ☐ 无程序集循环依赖。
>
> ☐ 核心服务通过 Composition Root 组合。
>
> ☐ Simulation 中无 GameObject.Find、FindObjectOfType、Resources.Load、LINQ 或运行时反射扫描。

## 内容扩展

> ☐ 新角色可只通过配置新增。
>
> ☐ 新技能可通过已有 Trigger/Targeting/Delivery/Effect 组合新增。
>
> ☐ 新构筑可通过标签、联动和进化新增。
>
> ☐ 新地图可通过 MapDefinition、场景和 Encounter 新增。
>
> ☐ 内容包有稳定 ID、版本、依赖和校验。
>
> ☐ 内容验证可阻止重复 ID、缺失引用和循环依赖。

## 保存与平台

> ☐ 存档版本化、原子写入、可迁移。
>
> ☐ 存档使用稳定 ContentId，不使用运行时索引。
>
> ☐ 缺失内容不会导致未处理异常。
>
> ☐ 无 Steam 环境可完整运行。

## 表现与资产

> ☐ 表现层可替换而不修改模拟层。
>
> ☐ Placeholder 被 Addressables 标签识别。
>
> ☐ Release 构建不能包含 Placeholder。
>
> ☐ 正式 AI 资源有完整 provenance。
>
> ☐ 第三方文件有许可证记录。
>
> ☐ 所有用户可见文字使用本地化 Key。

## 测试与性能

> ☐ EditMode 与 PlayMode 测试通过。
>
> ☐ 固定种子的核心测试可复现。
>
> ☐ 30 分钟压力运行无持续内存增长。
>
> ☐ 稳态无高频托管分配。
>
> ☐ 第二角色、第二技能、第二地图无代码扩展测试通过。
>
> ☐ Windows Development 与 Release Build 可从干净 clone 生成。

## 文档

> ☐ 架构、内容 Schema、测试、性能和保存文档与代码一致。
>
> ☐ 每个重大架构决定有 ADR。
>
> ☐ 所有已知限制和技术债有记录。

## 《剑起青岚》Demo G2.8 状态说明

G2.8 的 Placeholder Development 门禁已 `PASS`：真实 12 分钟三路线、十次 Host 生命周期、600 敌人
P0 可读性、54,000 Tick Soak、Windows Development Build 和独立 Player 均有证据。上述结果不勾选
正式资产、Release、目标 GPU、正式字体/音频或干净 clone Release 条目；这些项目仍由 G3.1—G3.6
逐项完成，全部通过前不得称 Demo Release `COMPLETE`。
