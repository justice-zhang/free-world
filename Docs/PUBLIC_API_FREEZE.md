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
