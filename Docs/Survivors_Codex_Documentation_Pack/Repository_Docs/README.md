# Unity 类幸存者框架仓库文档

本目录可复制到一个全新的 Unity 6 LTS URP 仓库根目录。它定义项目范围、架构、内容 Schema、Codex 工作方式、测试、性能、存档、AI 资产和 Steam 集成边界。

## 建议目录映射

复制后应形成：

```text
<unity-repository>/
├─ AGENTS.md
├─ README.md
├─ AI_ASSET_POLICY.md
├─ ASSET_PROVENANCE.csv
├─ THIRD_PARTY_NOTICES.md
└─ Docs/
   ├─ MASTER_PLAN.md
   ├─ PRODUCT_SCOPE.md
   ├─ ARCHITECTURE.md
   ├─ CONTENT_SCHEMA.md
   ├─ EFFECT_MODULES.md
   ├─ SAVE_FORMAT.md
   ├─ TEST_PLAN.md
   ├─ PERFORMANCE_BUDGET.md
   ├─ CODEX_WORKFLOW.md
   ├─ EXECUTION_ORDER.md
   ├─ DEFINITION_OF_DONE.md
   ├─ AI_ASSET_PIPELINE.md
   ├─ STEAM_INTEGRATION_BOUNDARY.md
   └─ ADR/
```

## 首次使用

1. 创建干净 Unity 6 LTS URP 项目。
2. 初始化 Git 并建立空工程基线。
3. 复制本目录内容到仓库根目录。
4. 填写 `Templates/PROJECT_VARIABLES.md`。
5. 在 `Docs/ADR/0001-unity-version.md` 中写入精确 Unity 版本。
6. 向 Codex 提供 `Prompts/00_MASTER_CONTROL.md`。
7. 执行人工预检，再从 M0 开始。

## 文档职责

| 文件 | 职责 |
|---|---|
| `AGENTS.md` | 自动化开发代理必须遵守的硬性规则。 |
| `Docs/MASTER_PLAN.md` | 项目目标、技术基线和交付阶段。 |
| `Docs/ARCHITECTURE.md` | 分层、程序集、固定 Tick、实体、地图和平台边界。 |
| `Docs/CONTENT_SCHEMA.md` | 稳定 ID、内容包、角色、技能、构筑、地图和验证规则。 |
| `Docs/EFFECT_MODULES.md` | 技能操作码、注册表和模块准入规则。 |
| `Docs/SAVE_FORMAT.md` | 存档、迁移、内容缺失和云同步边界。 |
| `Docs/TEST_PLAN.md` | EditMode、PlayMode、Soak 和扩展性测试。 |
| `Docs/PERFORMANCE_BUDGET.md` | 性能目标、测量规则和回归门禁。 |
| `Docs/EXECUTION_ORDER.md` | M0—M10 实施顺序。 |
| `Docs/DEFINITION_OF_DONE.md` | 框架冻结前必须满足的条件。 |
| `AI_ASSET_POLICY.md` | 正式 AI 资源的治理和发布门禁。 |
| `THIRD_PARTY_NOTICES.md` | 第三方代码与资产登记。 |

## 核心原则

- 新建干净工程，不 fork 参考游戏工程。
- 模拟、应用、表现和平台层分离。
- 作者数据在构建前验证并烘焙成纯运行时定义。
- 新角色、新技能、新构筑和新地图优先通过内容配置新增。
- 所有正式 AI 资源和第三方资源必须可追溯。
- 未经过实际测试和构建验证，不得声明里程碑完成。
