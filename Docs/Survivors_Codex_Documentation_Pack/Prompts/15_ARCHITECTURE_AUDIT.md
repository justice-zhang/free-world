# 架构审计提示词

对仓库执行架构合规审计，默认只读。除非我明确要求，不修改代码。

## 检查

- asmdef 依赖方向和循环

- Core 是否引用 UnityEngine

- Simulation 是否引用 MonoBehaviour、GameObject、Scene、Prefab、Sprite、AudioClip、Addressables 或平台 SDK

- View/UI 是否直接修改 Simulation Store

- 是否存在全局 Service Locator、无控制 Singleton 或场景查找

- 高频路径中的 LINQ、反射、字符串格式化、集合分配

- 内容注册表是否硬编码具体内容

- Runtime Definition 是否持有 Unity Object

- 存档是否保存 RuntimeIndex

- 内容 ID 是否被修改或覆盖

- Placeholder、Third Party、provenance 和本地化门禁

- 测试是否覆盖关键规则

## 输出

按严重程度列出：Blocker、High、Medium、Low。每项包含文件、行号、原因、影响和最小修复建议。没有证据时不要推测。
