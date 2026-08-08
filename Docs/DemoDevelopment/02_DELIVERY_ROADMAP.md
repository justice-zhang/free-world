# 02 G0—G3 交付路线与分支结构

## 1. 单工作包纪律

每次只实施下表一个工作包。分支默认使用 `codex/gN-<work-package>`；只有前一依赖包的审查、
合并和结果报告完成后才能创建下一分支。不得在一个 PR 中同时扩 Schema、批量做正式资产并完成
Release 调优。

## 2. G0 方案冻结

| 顺序 | 分支建议 | 交付物 | 退出门禁 |
|---:|---|---|---|
| G0.1 | `codex/g0-demo-structure` | 本文档集、追踪矩阵、ID 草案 | 文档检查 PASS |
| G0.2 | `codex/g0-demo-cr-review` | CR-01—CR-11 决策包 | 每项 Accepted/Rejected/Deferred |
| G0.3 | `codex/g0-demo-schema-contracts` | 获批 ADR、Schema/API/Save 迁移与测试计划 | 架构审查 PASS |
| G0.4 | `codex/g0-demo-asset-plan` | 资产/音频/字体/本地化生产清单与预算 | provenance 流程可执行 |

若 G0.2 拒绝某项能力，必须同步修改 Demo 完成定义或找到不改变体验承诺的现有模块组合；不能静默
采用近似行为。

## 3. G1 数据切片

| 顺序 | 分支建议 | 模块 | 主要验证 |
|---:|---|---|---|
| G1.1 | `codex/g1-demo-approved-modules` | 获批通用模块/Schema | API Freeze、EditMode、Validation、性能短测 |
| G1.2 | `codex/g1-demo-character-combat` | M02、M03 | 乘风状态机、属性/状态、固定 Seed |
| G1.3 | `codex/g1-demo-weapons` | M04 | 六技能等级、预览、ProcDepth、清理 |
| G1.4 | `codex/g1-demo-build-content` | M05 | 六心诀、Offers、Synergy/Evolution 可达性 |
| G1.5 | `codex/g1-demo-enemies` | M07 | 六敌人、四词缀、行为与攻击技能 |
| G1.6 | `codex/g1-demo-encounter` | M09 | 12 分钟时间轴、并发上限、固定 Seed |
| G1.7 | `codex/g1-demo-pack-gate` | M05、M15 | Reward Choice 适配器、完整 Pack Bake、双语占位、Development Build |

G1 只使用程序化 Placeholder。正式角色、Logo、字体、音频和品牌素材不得混入数据切片。

## 4. G2 可玩切片

| 顺序 | 分支建议 | 模块 | 主要验证 |
|---:|---|---|---|
| G2.1 | `codex/g2-demo-map-runtime` | M08 | 五区、三风脉台、三事件、五地标 |
| G2.2 | `codex/g2-demo-bosses` | M10 | 折枝、听风三阶段、目标修正 |
| G2.3 | `codex/g2-demo-rewards` | M06 | 灵物、奇物、显化宝匣、首通奖励 |
| G2.4 | `codex/g2-demo-game-flow` | M01 | 标题→Run→结算→据点→再次出发 |
| G2.5 | `codex/g2-demo-meta-save` | M11、M14 | 行脉、嵌片、收藏、故事、Meta 校验与幂等结算事务 |
| G2.6 | `codex/g2-demo-ui-input` | M12 | 键鼠/手柄、页面、HUD、可访问性 |
| G2.7 | `codex/g2-demo-placeholder-polish` | M13 | 程序化表现、池、预警和音频占位 |
| G2.8 | `codex/g2-demo-vertical-slice-gate` | 全模块 | PlayMode、Development Build、可读性评审 |

G2.6 已在统一实现分支交付，实际证据见 `21_G2_6_UI_INPUT_ACCESSIBILITY.md` 与
`Docs/Reports/2026-08-09-g2-6-ui-input-accessibility.md`；G2.7 必须继续复用其单一 Canvas、输入命令和
只读 `RunUiSnapshot`，不得建立第二套 UI 或玩法真值。

## 5. G3 发布候选

| 顺序 | 分支建议 | 交付物 | 退出门禁 |
|---:|---|---|---|
| G3.1 | `codex/g3-demo-art-import` | 正式角色、敌人、地图、UI、VFX Profile | provenance＋Addressables PASS |
| G3.2 | `codex/g3-demo-audio-import` | 音乐、环境、机制和危险提示 | 许可证＋混音可读性 PASS |
| G3.3 | `codex/g3-demo-localization` | zh-Hans/en 正文、字体与伪本地化 | Locale/裁切/字体 PASS |
| G3.4 | `codex/g3-demo-balance` | 数值冻结与 Seed 矩阵 | 三构筑与失败率目标 PASS |
| G3.5 | `codex/g3-demo-performance` | 目标硬件 CPU/GPU/GC/池证据 | 1080p 60、1% Low 警报审查 |
| G3.6 | `codex/g3-demo-release-candidate` | Release Build、Smoke、Manifest、合规包 | DOD-01—10 全部 PASS |

## 6. 每个工作包固定交付

1. 范围与明确非范围；
2. 修改文件、内容 ID 和资产来源；
3. 关键决定及 ADR/CR；
4. 实际命令；
5. `PASS/FAIL/NOT RUN` 证据表；
6. 生成物路径与 Hash；
7. 风险、未执行项和下一工作包前置条件。

## 7. 回滚原则

- 内容回滚：移除未发布 Catalog 条目并重 Bake；发布 ID 不复用；
- Schema 回滚：保留旧读取器与迁移 Fixture，不能只降低版本号；
- 公共 API 回滚：恢复 Freeze Hash 前先验证下游源码/二进制兼容；
- 资产回滚：Addressables 清单与 provenance 同步回滚；
- 平衡回滚：只修改内容数值和版本，不更改确定性随机流协议。
