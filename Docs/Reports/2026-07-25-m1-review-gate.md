# Codex 结果报告

- 任务：M1 核心类型与内容系统严格审查门禁
- 里程碑：M1
- 基准：`framework-m0`（`43ef77926ad917f4bf178943490e15139255aa02`）
- 分支：`codex/m1-content-system`
- Git Commit：`8edcfadee2f2d3824dee5db0a401146e51e39f22`
- 日期：2026-07-25
- 最终结论：`PASS`

## 1. 实现范围

先对 M1 做只读差异、程序集、禁用模式、内容/资产/本地化和完整门禁审查，再仅修复
三个 M1 范围内问题：

1. 被引用 Skill 的无效 ID 错误错误地归因到 Character 资产。
2. 运行时只读集合直接暴露 backing array，可绕过验证并使 Content Hash/Registry 状态失配。
3. 部分新增 public API 缺少仓库规则要求的简明 XML 文档。

没有实现实体、战斗、技能执行、刷怪、正式资源、存档功能或后续里程碑内容。

## 2. 新增、修改和删除文件

相对 `framework-m0`：

- 修改 16 个：
  - `Assets/Game/Application/GameApplication.cs`
  - `Assets/Game/Content/Authoring/Game.Content.Authoring.asmdef`
  - `Assets/Game/Editor/Game.Editor.asmdef`
  - `Assets/Game/Editor/ProjectGovernanceValidator.cs`
  - `Assets/Game/Infrastructure/Game.Infrastructure.asmdef`
  - `Assets/Game/Infrastructure/GameBootstrapper.cs`
  - `Assets/Scenes/Bootstrap.unity`
  - `Assets/Tests/EditMode/AssemblyGovernanceTests.cs`
  - `Assets/Tests/PlayMode/BootstrapPlayModeTests.cs`
  - `Assets/Tests/PlayMode/Game.Tests.PlayMode.asmdef`
  - `Docs/ADR/0003-content-packs.md`
  - `Docs/ARCHITECTURE.md`
  - `Docs/CONTENT_SCHEMA.md`
  - `Docs/TEST_PLAN.md`
  - `README.md`
  - `Scripts/validate.ps1`
- 新增 70 个：
  - `Assets/Game/Content/Authoring/CharacterAuthoring.cs` 及 `.meta`
  - `Assets/Game/Content/Authoring/ContentAuthoringBase.cs` 及 `.meta`
  - `Assets/Game/Content/Authoring/ContentBaker.cs` 及 `.meta`
  - `Assets/Game/Content/Authoring/ContentPackAuthoring.cs` 及 `.meta`
  - `Assets/Game/Content/Authoring/EnemyAuthoring.cs` 及 `.meta`
  - `Assets/Game/Content/Authoring/MapAuthoring.cs` 及 `.meta`
  - `Assets/Game/Content/Authoring/SkillAuthoring.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/BakedContentCatalog.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/BakedContentCatalogDto.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/ContentPackManifest.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/ContentPackTopology.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/ContentRegistry.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/ContentValidator.cs` 及 `.meta`
  - `Assets/Game/Content/Runtime/RuntimeContentDefinitions.cs` 及 `.meta`
  - `Assets/Game/Core/ContentError.cs` 及 `.meta`
  - `Assets/Game/Core/ContentId.cs` 及 `.meta`
  - `Assets/Game/Core/ContentTag.cs` 及 `.meta`
  - `Assets/Game/Core/ContentVersion.cs` 及 `.meta`
  - `Assets/Game/Core/Result.cs` 及 `.meta`
  - `Assets/Game/Core/RuntimeContentIndex.cs` 及 `.meta`
  - `Assets/Game/Editor/ContentBakeUtility.cs` 及 `.meta`
  - `Assets/Game/Editor/ContentProjectValidator.cs` 及 `.meta`
  - `Assets/Game/Editor/M1TestContentSetup.cs` 及 `.meta`
  - `Assets/Tests/EditMode/ContentBakerTests.cs` 及 `.meta`
  - `Assets/Tests/EditMode/ContentIdTests.cs` 及 `.meta`
  - `Assets/Tests/EditMode/ContentPackTopologyTests.cs` 及 `.meta`
  - `Assets/Tests/EditMode/ContentRegistryTests.cs` 及 `.meta`
  - `Assets/GameAssets/Placeholder/TestContent.meta`
  - `Assets/GameAssets/Placeholder/TestContent/TestCharacter.asset` 及 `.meta`
  - `Assets/GameAssets/Placeholder/TestContent/TestEnemy.asset` 及 `.meta`
  - `Assets/GameAssets/Placeholder/TestContent/TestM1ContentPack.asset` 及 `.meta`
  - `Assets/GameAssets/Placeholder/TestContent/TestM1ContentPack.baked.json` 及 `.meta`
  - `Assets/GameAssets/Placeholder/TestContent/TestMap.asset` 及 `.meta`
  - `Assets/GameAssets/Placeholder/TestContent/TestSkill.asset` 及 `.meta`
  - `Docs/CONTENT_AUTHORING_WORKFLOW.md`
  - `Docs/Reports/2026-07-25-m1-content-system.md`
  - `Docs/Reports/2026-07-25-m1-review-gate.md`
- 删除 0 个。

本次审查实际修复的文件：

- `Assets/Game/Content/Authoring/ContentBaker.cs`
- `Assets/Game/Content/Authoring/ContentPackAuthoring.cs`
- `Assets/Game/Content/Runtime/BakedContentCatalog.cs`
- `Assets/Game/Content/Runtime/BakedContentCatalogDto.cs`
- `Assets/Game/Content/Runtime/ContentPackManifest.cs`
- `Assets/Game/Content/Runtime/ContentRegistry.cs`
- `Assets/Game/Content/Runtime/ContentValidator.cs`
- `Assets/Game/Content/Runtime/RuntimeContentDefinitions.cs`
- `Assets/Game/Core/ContentError.cs`
- `Assets/Game/Editor/M1TestContentSetup.cs`
- `Assets/Tests/EditMode/ContentBakerTests.cs`
- `Assets/Tests/EditMode/ContentRegistryTests.cs`
- `Docs/CONTENT_SCHEMA.md`
- `Docs/CONTENT_AUTHORING_WORKFLOW.md`
- `Docs/TEST_PLAN.md`
- `Docs/Reports/2026-07-25-m1-content-system.md`
- `Docs/Reports/2026-07-25-m1-review-gate.md`

## 3. 关键决定

- Baker 在解析定义间引用前，先按 Pack 作者顺序解析每个定义资产路径并验证其自身 ID。
  因而被引用资产的无效 ID 始终报告实际资产，而不是第一个引用者。
- Catalog、Manifest、运行时定义和 Registry 保留内部数组用于确定性顺序，但公共属性返回
  缓存的 `Array.AsReadOnly` 视图。构造输入仍先 clone，不增加高频路径工作。
- 不改变 Content Hash 字段、排序、Schema 或 baked JSON，因此提交的测试 Catalog 无需重烘焙。
- 没有为 Addressables、Localization 表或存档提前增加后续里程碑功能。

## 4. 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| Core 六类核心类型及明确 Result/Error | PASS | 编译和 Core 源码审查 |
| ContentId 规范化、字符校验、完整字符串、比较和序列化 | PASS | `ContentIdTests` |
| Hash 碰撞不改变稳定 ID 身份 | PASS | 已知碰撞字典测试 |
| Manifest、依赖版本检查和稳定拓扑排序 | PASS | `ContentPackTopologyTests` |
| 缺失依赖与循环失败 | PASS | 对应 EditMode 测试 |
| 五类最小作者数据 | PASS | 作者层源码和 Placeholder fixture |
| 纯运行时定义与 Baked Catalog 无 Unity Object | PASS | 反射字段测试及静态引用审计 |
| 同一作者输入产生相同 SHA-256 Content Hash | PASS | Baker 确定性与 JSON round-trip 测试 |
| Registry 按稳定 ID 查找并分配同次加载稳定索引 | PASS | `ContentRegistryTests` |
| Registry 拒绝重复 ID 并报告两侧来源 | PASS | 重复来源测试 |
| 新定义子类无需修改 Registry 硬编码列表 | PASS | 自定义测试定义注册测试 |
| Validator 覆盖 ID、重复、缺失引用、依赖、循环和版本 | PASS | EditMode 测试与命令行验证 |
| 所有可定位内容错误含 ID、Pack 和实际作者路径 | PASS | 修复后的被引用资产、重复 ID、缺失引用测试 |
| 最小测试包只使用 Placeholder | PASS | 目录/标签静态审查及项目验证 |
| Bootstrap 加载测试包并输出条目数，不进入战斗 | PASS | PlayMode 及构建启动日志 `packs=1, entries=4` |
| 内容验证可从命令行运行 | PASS | `Scripts/validate.ps1` 返回 0 和 PASS marker |
| M0 测试无回归 | PASS | 最终完整 EditMode 33/33、PlayMode 5/5 |
| asmdef 无循环且 Core/Runtime/Simulation 不引用 UnityEngine | PASS | 13 asmdef、40 内部边、0 缺失引用、0 环 |
| 禁用 API/反射扫描/高频 LINQ/逐帧回调/Runtime AssetReference | PASS | 最终 `rg` 静态审计均 0 |
| 内容、存档、资产和本地化规则 | PASS | 无存档/Package/ProjectSettings/第三方/AI 改动；8 个本地化 Key 非空 |
| Windows x64 Development Build | PASS | Build Manifest 为 `Succeeded` / `StandaloneWindows64` |

## 5. 失败复现、根因与修复

### 5.1 错误作者路径

- 最小复现：Character 引用 ID 为 `Test.Skill.Uppercase` 的 Skill；期望
  `Assets/Test/Skill.asset`，实际为 `Assets/Test/Character.asset`。
- 修复前测试：`ContentBakerTests.AuthoringRejectsNonCanonicalIdWithPackAndAssetPath` FAIL。
- 根因：Character 在目标 Skill 自身 Bake 前验证引用 ID，并传入 owner 路径。
- 修复：ContentBaker 先预验证 Pack 内所有定义自身的 ID/path，再执行引用解析。

### 5.2 backing array 泄漏

- 最小复现：`catalog.Definitions is RuntimeContentDefinition[]` 为 true；同样问题存在于
  Manifest dependencies、定义 Tags/引用和 Registry Pack ID。
- 修复前测试：`ContentRegistryTests.RuntimeCollectionsDoNotExposeMutableBackingArrays` FAIL。
- 根因：构造函数 clone 了输入，但 `IReadOnlyList` 属性直接返回内部数组。
- 修复：返回缓存的只读集合视图，并保留构造 clone。

修复前红灯 XML：`TestResults/M1ReviewRed/editmode.xml`，33 项中 31 PASS、2 FAIL。

## 6. 实际执行的命令

```text
git -c safe.directory=E:/ai/free-world status --short --branch
git -c safe.directory=E:/ai/free-world diff --name-status framework-m0
git -c safe.directory=E:/ai/free-world ls-files --others --exclude-standard
git -c safe.directory=E:/ai/free-world diff --check

rg ... Assets/Game/Simulation
rg ... Assets/Game/Core Assets/Game/Content/Runtime
rg ... Assets/Game

Unity.exe -batchmode -nographics -quit -projectPath E:\ai\free-world
  -logFile TestResults\M1ReviewBeforeFixHost\compile-host.log
.\Scripts\test.ps1 -Platform All -ResultsDirectory TestResults/M1ReviewBeforeFixHost
.\Scripts\validate.ps1 -LogPath TestResults/M1ReviewBeforeFixHost/validation.log
.\Scripts\build-windows.ps1
  -OutputPath Builds/WindowsDevelopment/M1Review/AzureSword.exe
  -LogPath TestResults/M1ReviewBeforeFixHost/build-windows.log

.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory TestResults/M1ReviewRed
.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory TestResults/M1ReviewFix1

Unity.exe -batchmode -nographics -quit -projectPath E:\ai\free-world
  -logFile TestResults\M1ReviewFinal\compile.log
.\Scripts\test.ps1 -Platform All -ResultsDirectory TestResults/M1ReviewFinal
.\Scripts\validate.ps1 -LogPath TestResults/M1ReviewFinal/validation.log
.\Scripts\build-windows.ps1
  -OutputPath Builds/WindowsDevelopment/M1ReviewFinal/AzureSword.exe
  -LogPath TestResults/M1ReviewFinal/build-windows.log
Builds\WindowsDevelopment\M1ReviewFinal\AzureSword.exe
  -batchmode -nographics -logFile TestResults\M1ReviewFinal\player-smoke.log
```

补充真实运行记录：

- 沙箱内首次 Unity 编译退出码为 0，但日志显示许可证超时和 `SourceAssetDB` 锁；该次判为
  `NOT RUN`，未作为编译通过证据。
- 沙箱内首次测试返回 4，未生成 XML；该次判为 `NOT RUN`。
- 修复前红灯命令外层在 180 秒超时，Unity 已在超时前写出完整失败 XML 和退出码 2；
  失败数取自该 XML，不把外层超时解释为测试通过。
- 所有有效 Unity 门禁均在宿主上下文执行，以避免沙箱许可证和 Library 锁。

## 7. 测试、验证与构建结果

| 检查 | 结果 | 真实结果 |
|---|---|---|
| 最终编译 | PASS | Unity 退出码 0；C# error 0；无 SourceAssetDB 锁 |
| 最终 EditMode | PASS | 33/33，失败 0，跳过 0 |
| 最终 PlayMode | PASS | 5/5，失败 0，跳过 0 |
| 最终内容/治理验证 | PASS | Unity 退出码 0；`[Project Validation] PASS` |
| Windows Development Build | PASS | `Succeeded`，`StandaloneWindows64`，Unity 6000.3.20f1 |
| 构建启动冒烟 | PASS | 找到 `[Bootstrap] Loaded content: packs=1, entries=4` |
| Release Build | NOT RUN | M1 只要求适用构建；已运行 Development Build |
| 性能/Soak | NOT RUN | M1 没有模拟 Tick、实体或高频运行负载 |

构建 EXE SHA-256：
`5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`。

测试 baked JSON 文件 SHA-256：
`6307514BD0BCCD4A6D5018F34554F05E1C43787B02F641CE849772C9C07BFCA3`。

## 8. 范围外改动与架构违规

- 范围外改动：无。
- 未修复的架构违规：无。
- `Instantiate/Destroy` 产品代码命中 2 处，均非高频路径：Editor Placeholder 临时纹理清理和
  Bootstrap 重复实例拒绝。
- Presentation 的 asmdef 依赖 Simulation，但未发现 UI/View 写 Simulation Store 的代码。

## 9. 未执行项目、已知限制和风险

- Release Build：`NOT RUN`，不把 Development Build 表述为 Release。
- 性能/Soak：`NOT RUN`，M1 无可测量模拟热点。
- 正式 Addressables Pack 生命周期、Locale 表完整性、存档/迁移、Steam 和 DLC 属于后续
  里程碑；当前 Bootstrap 按已接受 ADR 的 M1 边界显式引用测试 baked TextAsset。
- 工作树尚未提交，仍需人工审查。

## 10. 下一步前置条件

- M1 实现已通过 [GitHub PR #4](https://github.com/justice-zhang/free-world/pull/4)
  合并，merge commit 为 `268e3f23535e304dfc4843d85ada5d2a47f642a0`。
- 执行日志与已知问题由
  [GitHub PR #5](https://github.com/justice-zhang/free-world/pull/5) 固化；
  `framework-m1` 指向该 PR 的最终 merge commit。
- M2 只能从该标签后的独立分支开始。
