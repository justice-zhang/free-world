# ADR 0004：使用 Addressables 管理表现资源与内容包资源

- 状态：Accepted
- 日期：2026-07-24
- 决策人：待填写

## 背景

角色、地图、VFX、音频、UI 和未来 DLC 需要异步加载、分组、版本化和构建验证。`Resources.Load` 缺乏明确依赖和分组控制，也不适合后续本地/远程 Catalog。

## 决策

- 使用 Unity Addressables 管理非核心表现资源。
- 初期使用本地 Catalog；架构保留远程 Catalog 和 DLC 的能力，但 M0—M10 不实现在线内容服务。
- 所有 Addressables 地址必须由内容或 Visual/Audio Profile 间接引用。
- 模拟层不得持有 Addressables、AssetReference 或 Unity Object。
- Placeholder 使用独立标签：`placeholder`、`development-only`。
- 正式发布内容使用显式 Release 标签和内容包标签。
- Release 构建前验证：不得存在 Placeholder、缺失地址、重复地址或未登记正式资产。
- 禁止使用 `Resources.Load` 绕开内容加载服务。

## 失败与释放

- 加载失败必须返回结构化错误，并进入可恢复的 `ContentError` 流程。
- 所有加载句柄必须明确拥有者和释放时机。
- 场景、地图包和运行结束时应验证无悬挂句柄。

## 后果

优点：资源分组、异步加载、构建门禁和后续 DLC 路径统一。  
代价：需要地址规范、句柄生命周期管理和专门验证器。
