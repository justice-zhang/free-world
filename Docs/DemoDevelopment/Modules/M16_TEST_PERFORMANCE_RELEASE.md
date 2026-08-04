# M16 测试、性能与发布门禁

## 1. 测试金字塔

| 层 | 目标 |
|---|---|
| EditMode | 纯逻辑、Schema、确定性、迁移、状态机、内容验证 |
| Headless | 12 分钟自动玩家、Boss/目标/构筑、清理和 Checksum |
| PlayMode | 页面、输入、Scene、目标、Boss、奖励、据点、语言、生命周期 |
| Performance/Soak | 正式内容 CPU/GPU/GC/内存/池/实体峰值 |
| Build/Player | Windows Development/Release、离线 Smoke、Manifest |
| 人工评审 | 手感、构筑差异、危险可读、视听、叙事节奏 |

## 2. EditMode 必测

- ContentId、Pack、Bake、Hash、全部新增 Definition/引用；
- 乘风真实位移、档位、受伤、暂停和确定性；
- 六武器等级/显化、状态、条件、ProcDepth、实体上限；
- 六心诀、Offer、槽位、Synergy/Evolution、宝匣 fallback；
- 六敌人、四 Affix、12 分钟 Encounter、Spawn 保护；
- 地图目标/事件/地标、Boss 阶段、8 组风脉台；
- Reward 去重、首通/失败/重复、Meta 容量/互斥；
- Settings/Profile/Recovery 迁移、校验、备份和缺内容。

## 3. Headless 场景

固定 30 Hz 推进至少 21,600 Tick，自动移动、升级、目标、奖励选择和 Boss。相同 Seed 两次比较：
Tick、等级、Build、击杀/精英/Boss、目标/事件/地标、候选历史、Spawn、Boss Phase、奖励和最终 Checksum。

至少三组 Golden Seed 分别偏向移动御剑、符阵爆发、草木铺场；Golden 只能在设计变更经审查后更新，
不能用更新 Golden 隐藏回归。

## 4. PlayMode 场景

1. 键鼠完整闭环；
2. 手柄完整闭环；
3. 三风脉台不同组合进入听风；
4. 胜利、失败、放弃、保存失败；
5. 简中、英文、Pseudo 与正式字体；
6. 低闪/色觉/伤害数字关闭；
7. Scene/Run 重复加载 10 次后无残留 View、Input、Handle、Addressables Owner。

## 5. 性能目标

| 指标 | Demo 目标/警报 |
|---|---|
| 分辨率/帧率 | 1080p 60 FPS；1% Low <45 触发警报和审查 |
| Simulation | 30 Hz；Tick p99 必须留在 33.33 ms 内并保有余量 |
| 正常活动敌人 | 600—1200 |
| 正常投射物/领域 | 400—900 |
| 扩展压力 | 2000 敌人，非 Demo 发布必过项但需记录 `NOT RUN/PASS/FAIL` |
| 稳态分配 | 0 B/frame 高频路径 |
| 高频 Instantiate/Destroy | 0 |
| Soak | 正式内容 30 分钟以上；至少两次 Demo 循环 |

M10 的 1,500/3,000/5,000、Tick p99 10.9851 ms 是纯框架基线；正式 Sprite/Shader/VFX/音频和目标 GPU
必须重新测量，不能继承为 Demo PASS。

## 6. 性能 JSON

输出硬件/OS/驱动/Unity/Git SHA/Pack Hash/Quality/分辨率/Seed，以及 CPU/GPU average/p95/p99/max、
各系统 Tick、实体峰值、Mono/Native/GC 趋势、GC 次数/分配、池命中/扩容/失败/丢弃、触发截断、无效
Handle、VFX 丢弃和 Checksum。

优化顺序沿用：远处动画→受击闪烁→伤害数字聚合→非关键 VFX 采样→屏外 View 降频→批处理/Instancing。
不得改变模拟命中或降低敌人数量掩盖表现问题。

## 7. 构建矩阵

| 配置 | 内容 | 必须结果 |
|---|---|---|
| Development | Placeholder 或正式 Demo Pack | Build＋完整 Player 流程 PASS |
| Release Verification | 仅正式 Demo 输入 | Placeholder=0、provenance/Third Party/Key PASS |
| Release Player | 正式 Demo Pack、Null Platform | 启动、完整闭环/Smoke、退出码 0 |
| Clean Clone | 独立克隆 | Test→Validation→Perf→Build→Player 同结果 |
| CI | 激活的自托管 Unity Runner | 实际 Job 步骤运行；排队/取消为 NOT RUN |

## 8. Build Manifest

记录 Git SHA/分支/Tag、Unity、Package/Addressables Hash、Content/Save Schema、Pack 版本/Hash、资产清洁
状态、测试结果、性能报告 Hash、UTC、EXE SHA-256、目标平台和 Release Validator 结果。

## 9. 发布阻断

以下任一为 FAIL：Placeholder/Development 标签进入 Release；来源/许可证/Hash 缺失；本地化 Key 空；
API Freeze 未批准漂移；存档迁移失败；目标/显化不可达；高危预警与伤害不一致；Release Player 无法离线；
测试日志/JSON/Manifest 缺失或过期。

## 10. 结果判定

每项只写 `PASS`、`FAIL`、`NOT RUN`。代码/内容修复后受影响旧证据失效，必须重跑。DOD-01—10 全部有
当前候选 Commit 的真实证据，才可称 Demo `COMPLETE`；否则为 `INCOMPLETE` 或 `BLOCKED`。
