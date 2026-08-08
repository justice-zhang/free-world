# 06 需求追踪矩阵

## 1. 需求到模块

| Req | 总纲需求 | Owner 模块 | 主要证据 | 状态 |
|---|---|---|---|---|
| R-001 | 标题至再次出发闭环 | M01、M11、M14 | PlayMode/Player | G2.6 键盘/手柄＋G2.8 独立 Development Player 闭环 PASS；Release Player 待 G3.6 |
| R-002 | 陆青野与真实位移乘风 | M02 | EditMode/PlayMode | G1.2 EditMode、G2.6 两种输入 PlayMode PASS |
| R-003 | 六把武器与等级成长 | M04 | Preview/EditMode | G1.3/G1.4 Preview 与 G2.8 12 分钟真实升级 PASS；正式手感待 G3.4 |
| R-004 | 六心诀与六显化 | M05 | Validation/Build Matrix | G1.4 资格/转换、G1.7 选择事务、G2.3 消费与 G2.8 实际路线 PASS |
| R-005 | 三种目标构筑 | M04、M05、M16 | Seed/人工矩阵 | G2.8 三 Seed/路线 Decision Checksum 互异 PASS；正式平衡/手感待 G3.4 |
| R-006 | 六即时灵物与六奇物 | M06 | EditMode/PlayMode | G2.3 内容/事务＋G2.8 真实奖励选择 PASS |
| R-007 | 六敌人与四词缀 | M07 | Headless/Validation | G1.5 行为/组合 PASS；G1.6 两次固定词缀精英与 21,600 Tick PASS |
| R-008 | 12 分钟时间轴 | M09 | Timeline/Headless | G2.8 真实 Factory 四局各≥21,600 Tick、两 Boss、停止边界 PASS |
| R-009 | 三风脉台、三事件、五地标 | M08 | PlayMode/State Trace | G2.8 路线完成 1/2/3 目标、3 Event、5 Landmark PASS |
| R-010 | 折枝和听风三阶段 | M10 | EditMode/PlayMode | G2.2 阶段专项＋G2.8 四局两 Boss 三阶段 PASS |
| R-011 | 风脉台改变最终 Boss | M08、M10 | 参数快照/PlayMode | G2.2 八组合＋G2.8 实际 Rule Mask 1/6/7 PASS |
| R-012 | 胜败/首通/重复通关 | M01、M11、M14 | Save Fixture/PlayMode | G2.4/G2.5 四结果与事务 PASS；G2.8 Victory Player 提交 PASS |
| R-013 | 12 行脉、3 嵌片、4 设施 | M11 | EditMode/UI/Save | G2.5 真值、G2.6 四设施/确认 UI PASS |
| R-014 | 6 藏品、3 故事 | M11、M14 | Save/Localization | G2.5 真值、G2.6 页面/Localization PASS |
| R-015 | 键鼠/手柄完整流程 | M12 | PlayMode | G2.6 键盘与手柄独立闭环 PASS |
| R-016 | 可访问性与双语 | M12、M14 | Locale/Layout/PlayMode | G2.6 Placeholder 自动化 PASS；正式字体/正文待 G3.3 |
| R-017 | 东方清朗视听与危险可读 | M13 | 资产评审/GPU 捕获 | G2.8 Placeholder 600 敌人 P0 自动/人工 PASS；正式视听/GPU 待 G3 |
| R-018 | 1080p 60 与 1% Low 警报 | M16 | 目标硬件 JSON | G3 |
| R-019 | 正式资产合规/Release | M13、M15、M16 | provenance/Manifest | G3 |
| R-020 | 离线完整运行 | M01、M14、M16 | Null Platform Player | G2.8 Null Platform Development Player 完整闭环 PASS；Release 待 G3.6 |

## 2. Demo 完成定义映射

| DOD | 模块 | 自动化最低要求 | 人工最低要求 |
|---|---|---|---|
| DOD-01 | M01/M12/M14 | 全页面 PlayMode | Release Player 完整跑一局 |
| DOD-02 | M01/M11/M14 | 四种结算 Save Fixture | 文件恢复与错误文案 |
| DOD-03 | M02/M04/M05/M16 | 三套固定 Seed | 手感和决策差异评审 |
| DOD-04 | M05/M06 | 奇物资格/互斥矩阵 | 选择是否真正改变路线 |
| DOD-05 | M06/M08/M11 | 地标奖励事务测试 | 探索提示可发现性 |
| DOD-06 | M08/M10 | 8 组风脉台组合参数测试 | Boss 阶段可读性 |
| DOD-07 | M11/M12/M14 | 容量/互斥/重置/迁移 | 据点操作清晰度 |
| DOD-08 | 全运行时 | Run 清理/句柄/存档 | 长局返回据点检查 |
| DOD-09 | M13/M16 | VFX 池/丢弃/危险优先级 | 目标 GPU 混战评审 |
| DOD-10 | M15/M16 | 全门禁脚本 | 合规与发布签字 |

## 3. 覆盖完整性规则

- 每个 `R-*` 必须有唯一 Owner 模块；协作者不能复制真值。
- 每个 `CR-*` 在实施前必须链接实际 Change Request 和 ADR。
- 每个 `DOD-*` 在 G3 报告中必须指向日志/XML/JSON/Manifest/Player 证据。
- `NOT RUN` 不能关闭需求；它只表示尚无证据。
- 产品范围变化必须同时更新 00、03、05、06 和受影响模块，避免只改一处数量。

## 4. G0.2 Change Request 状态

- `CR-01`—`CR-09` 已接受并映射至 `CR-2026-004`—`CR-2026-012`。
- `CR-10` 已拆分为公共 Stat `CR-2026-013` 与 Damage Policy `CR-2026-014`，两项均接受。
- `CR-11` 映射至 `CR-2026-015` 并延期；当前 Demo 只允许检测、提示和清理不完整 Run。
- 完整矩阵见 [07_CHANGE_REQUEST_DECISIONS.md](07_CHANGE_REQUEST_DECISIONS.md)。接受只授权 G0.3 契约设计，状态列中的 CR 依赖尚不表示实现完成。

## 5. G0.3 契约状态

- ADR 0013：R-002—R-014 所需 Schema 6、新模块和公共 Stat。
- ADR 0014：R-001—R-012 的 Pipeline、Owner、Damage、Reward、Map、Boss、随机流和 Cleanup。
- ADR 0015：R-012—R-014 的 Profile 3、Loadout、首通/唯一事务和 CR-11 延期边界。
- [08_G0_3_CONTRACT_FREEZE.md](08_G0_3_CONTRACT_FREEZE.md) 把每个 Formal CR 映射到首次实现包和最低测试。

G1.1 已完成批准通用契约，G1.2 已完成 R-002 的角色、真实位移、受伤掉档和 54,000 Tick 自动化部分；
G1.3 已完成 R-003 的六把 8 级主武器、十个隐藏技能、固定 Seed Preview Golden、ProcDepth 与 Cleanup
自动化部分。G1.4 已完成六个 5 级心诀、18 Offer、三 Synergy、六条 Evolution 资格/原子转换、六显化
固定 Seed Golden 与三条目标构筑自动矩阵。G1.5/G1.6 已完成 R-007 与 R-008 的纯内容/模拟部分：六敌人、
四词缀、九段时间轴、两个固定精英、停止边界和双实例 21,600 Tick；Boss 规则与实际地图 PlayMode 仍
由 G2.2/G2.6/G2.8 关闭。G1.7 已完成独立 Reward Choice 的资格、随机隔离、暂停、fallback、幂等提交和
Application 投影，并通过完整 Placeholder Pack 与 Development Build；G2.1—G2.7 已依次关闭地图、Boss、
奖励、流程、Meta、UI 和 Placeholder 表现。G2.8 以真实 12 分钟三路线、十次生命周期、600 敌人可读性、
54,000 Tick 性能和 Development Player 关闭 G2 集成门禁。正式资产、目标 GPU、平衡与 Release DOD 仍
只有 G3 的真实证据可以改为 PASS。
