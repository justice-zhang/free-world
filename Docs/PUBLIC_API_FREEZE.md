# 公共 API 冻结基线

M10 冻结以下程序集的公开类型与 public 成员 API。`Game.Editor.CoreApiFreezeValidator` 使用编译后反射
输出规范化类型/成员签名并计算 SHA-256；Project Validation 会在签名数量或 Hash 漂移时失败。

| 程序集 | 签名数 | SHA-256 |
|---|---:|---|
| `Game.Core` | 147 | `cbc7dcb08b2460e73f94e4bdc0f521cd38bb4c12e86156ce732fa8d792e5385f` |
| `Game.Content.Runtime` | 663 | `f38753a12ebbbb32a436c7f59c83a49eee0ba85b481e31acf9d964109b04c235` |
| `Game.Simulation` | 1002 | `ed82f11b72a93c079843eb7d41b27c11926e0f63f17380253c5ff80621ffd19a` |
| `Game.Application` | 305 | `56f87d47e257170228686e27583e79ae0bcb9eb5ea72dbd7e8f4a1796d08e2aa` |
| `Game.Platform.Abstractions` | 73 | `8eb5f2ccca0f5845a55d90c9f00fb42eae59cc82d81e98369995e84428a51738` |

## 变更协议

1. 先提交并接受 ADR；说明旧 API 的消费者、二进制/源码兼容性、Content/Save Schema 影响、迁移
   步骤、回滚方案和测试。
2. 优先增加兼容 API 或提供明确弃用周期；不得只为使验证通过而无说明地替换 Hash。
3. 修改实现和测试后运行完整 EditMode、PlayMode、Project Validation、性能基准和受影响构建。
4. 只有审查接受 API 变化后，才可更新 `CoreApiFreezeValidator` 与本文件中的签名数和 Hash。

内部类型和成员不进入冻结 Hash，但仍受程序集方向、内容扩展和存档稳定 ID 规则约束。规范化
输出不依赖元数据 Token 或编译顺序，因此相同源码应得到相同基线。

## Qinglan Demo G0.3 已批准变更窗口

ADR 0013—0015 已批准 G1.1 在不改变依赖方向的前提下追加下列公共契约。当前表头的 M10 Hash 仍是
真实代码基线；在 G1.1 实现、完整测试和签名 diff 审查前不得提前替换。

| Assembly | 批准追加范围 | 明确禁止 |
|---|---|---|
| `Game.Core` | 4 个 BuiltInStatId；DamageChannelId/内建通道；必要的稳定事务值类型 | 删除/重排旧 Stat、具体 Qinglan ID |
| `Game.Content.Runtime` | Schema 6 常量、14 类定义、模块引用操作数、RewardOp/纯值枚举 | 改写 Schema 1—5 构造/Hash |
| `Game.Simulation` | 24 项 Demo Pipeline、Mechanic/Reward/Map/Boss/Affix Runtime、Movement/Damage 纯值与快照 | UnityEngine、Scene、存档/平台写入 |
| `Game.Application` | Profile 3、按 kind Save 版本、Meta/RewardChoice/RunResult/Commit 契约 | RuntimeIndex/EntityHandle 持久化 |
| `Game.Platform.Abstractions` | 无计划变化 | 为 Demo 绕过 Application 直接调平台 |

G1.1 Freeze 更新门禁：

1. 在旧 Hash 下运行 Project Validation，预期只因批准签名漂移而 FAIL，并保存规范签名 diff；
2. 证明 diff 仅含上表追加项，旧 public 类型/成员签名仍存在；
3. 运行完整 EditMode、PlayMode、内容验证、性能短测和 Development Build；
4. 再更新 Validator 签名数/Hash 与本文件，并重跑全部门禁至 PASS；
5. 报告同时保留“旧 Hash 预期 FAIL”和“新 Hash 最终 PASS”，不得删除前者。

若实现需要删除、改名、重排或改变旧成员语义，G0.3 授权不足，必须新提交 CR/ADR。
