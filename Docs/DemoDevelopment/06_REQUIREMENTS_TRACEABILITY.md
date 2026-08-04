# 06 需求追踪矩阵

## 1. 需求到模块

| Req | 总纲需求 | Owner 模块 | 主要证据 | 状态 |
|---|---|---|---|---|
| R-001 | 标题至再次出发闭环 | M01、M11、M14 | PlayMode/Player | DESIGN |
| R-002 | 陆青野与真实位移乘风 | M02 | EditMode/PlayMode | CR-01 |
| R-003 | 六把武器与等级成长 | M04 | Preview/EditMode | CR-02/03 部分阻塞 |
| R-004 | 六心诀与六显化 | M05 | Validation/Build Matrix | CR-04 部分阻塞 |
| R-005 | 三种目标构筑 | M04、M05、M16 | Seed/人工矩阵 | DESIGN |
| R-006 | 六即时灵物与六奇物 | M06 | EditMode/PlayMode | CR-05 |
| R-007 | 六敌人与四词缀 | M07 | Headless/Validation | CR-08 部分阻塞 |
| R-008 | 12 分钟时间轴 | M09 | Timeline/Headless | 可用现有 Encounter |
| R-009 | 三风脉台、三事件、五地标 | M08 | PlayMode/State Trace | CR-06 |
| R-010 | 折枝和听风三阶段 | M10 | EditMode/PlayMode | CR-07 |
| R-011 | 风脉台改变最终 Boss | M08、M10 | 参数快照/PlayMode | CR-06/07 |
| R-012 | 胜败/首通/重复通关 | M01、M11、M14 | Save Fixture/PlayMode | CR-09 |
| R-013 | 12 行脉、3 嵌片、4 设施 | M11 | EditMode/UI/Save | CR-09 |
| R-014 | 6 藏品、3 故事 | M11、M14 | Save/Localization | CR-09 |
| R-015 | 键鼠/手柄完整流程 | M12 | PlayMode | 现有输入边界可扩展 |
| R-016 | 可访问性与双语 | M12、M14 | Locale/Layout/PlayMode | DESIGN |
| R-017 | 东方清朗视听与危险可读 | M13 | 资产评审/GPU 捕获 | G3 |
| R-018 | 1080p 60 与 1% Low 警报 | M16 | 目标硬件 JSON | G3 |
| R-019 | 正式资产合规/Release | M13、M15、M16 | provenance/Manifest | G3 |
| R-020 | 离线完整运行 | M01、M14、M16 | Null Platform Player | 现有边界可用 |

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
