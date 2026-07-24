# ADR 0001：锁定 Unity 6 LTS 精确补丁版本

- 状态：Accepted
- 日期：2026-07-24
- 决策人：待填写

## 背景

Unity 项目、官方 Package、序列化资源、Shader 导入和构建结果都可能受到编辑器补丁版本影响。让每位开发者使用不同版本会造成不可复现的 YAML 变化、Package 分歧和构建问题。

## 决策

- 项目使用 Unity 6 LTS。
- 精确版本以 `ProjectSettings/ProjectVersion.txt` 为唯一事实来源。
- M0 开始前填写实际版本：`<UNITY_VERSION>`。
- CI、开发机和正式构建机必须使用同一精确版本。
- 开发期间不得自动升级编辑器或 Package。

## 变更流程

升级 Unity 版本必须：

1. 新建 ADR。
2. 在独立分支完成升级。
3. 保存升级前基线。
4. 运行完整 EditMode、PlayMode、内容验证和 Windows Build。
5. 比较序列化、渲染、输入、Addressables 和性能结果。
6. 提供回滚方案。
7. 经人工批准后合并。

## 后果

优点：构建可复现，减少资源和 Package 差异。  
代价：安全或功能补丁不能在没有回归验证的情况下立即升级。
