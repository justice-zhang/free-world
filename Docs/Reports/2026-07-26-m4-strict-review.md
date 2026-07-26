# Codex 结果报告

- 任务：M4 模块化技能运行时严格只读审查与最小修复
- 里程碑：M4
- 分支：`codex/m4-skill-runtime`
- 实现提交：`0f5df90c5f3224052c2ca3711b22fcd1e5d56f6f`
- GitHub PR：[#8](https://github.com/justice-zhang/free-world/pull/8)
- 日期：2026-07-26
- 里程碑结论：`PASS`

## 1. 实现范围

先对相对 `framework-m3` 的完整 M4 工作树执行只读审查，再仅修复审查中可复现的 M4
失败。未新增模块、技能类型、敌人 AI、UI、VFX、构筑、存档或后续里程碑功能。

审查确认并最小修复五类失败：

1. LevelPatch 只验证路径、类型和操作数，没有验证逐级累积后的非有限浮点、32 位整数溢出
   和 Effect 参数契约。
2. SpawnSecondarySkill 接受非可执行的 Schema 1/2 Skill 引用，运行时会静默缺少实例。
3. 二次技能只预注册直接引用，三级及更深调用链中断，ProcDepth 无法继续传播。
4. Actor 删除时 Skill Instance 不释放，实例计数和固定 Tick 扫描上界持续增长，旧句柄也
   没有可安全复用的代际保护。
5. 集中 Heal resolver 仍直接写 `ActorCombatRecord.HealthCurrent`，不满足“Skill 不直接修改
   Health”的严格验收口径。

另修正文档中 OnStatusApplied 的语义描述，使其与代码和测试一致：Owner 是状态接收方。

## 2. 新增和修改文件

### 2.1 审查开始时的完整 Git 范围

相对 `framework-m3`：审查开始时为 16 个已跟踪修改、46 个未跟踪新增、0 个删除；加入
本审查报告后为 17 个已跟踪修改、47 个未跟踪新增、0 个删除。发布集成记录和已知问题
同步完成后，PR #8 的最终范围为 19 个已跟踪修改、47 个新增、0 个删除。

最终已跟踪修改（19）：

```text
Assets/Game/Content/Authoring/ContentBaker.cs
Assets/Game/Content/Authoring/SkillAuthoring.cs
Assets/Game/Content/Runtime/BakedContentCatalogDto.cs
Assets/Game/Content/Runtime/ContentPackTopology.cs
Assets/Game/Content/Runtime/ContentRegistry.cs
Assets/Game/Content/Runtime/ContentValidator.cs
Assets/Game/Content/Runtime/RuntimeContentDefinitions.cs
Assets/Game/Simulation/CombatSystems.cs
Assets/Game/Simulation/EntityStores.cs
Assets/Game/Simulation/SimulationSystems.cs
Assets/Game/Simulation/SimulationWorld.cs
Assets/Tests/EditMode/StatusContentTests.cs
Docs/ARCHITECTURE.md
Docs/CONTENT_AUTHORING_WORKFLOW.md
Docs/CONTENT_SCHEMA.md
Docs/EFFECT_MODULES.md
Docs/EXECUTION_LOG.md
Docs/KNOWN_ISSUES.md
Docs/TEST_PLAN.md
```

新增（47）：

```text
Assets/Game/Content/Runtime/RuntimeSkillDefinition.cs
Assets/Game/Content/Runtime/RuntimeSkillDefinition.cs.meta
Assets/Game/Content/Runtime/SkillContentDtos.cs
Assets/Game/Content/Runtime/SkillContentDtos.cs.meta
Assets/Game/Editor/M4TestSkillSetup.cs
Assets/Game/Editor/M4TestSkillSetup.cs.meta
Assets/Game/Simulation/SkillDeliveryExecutors.cs
Assets/Game/Simulation/SkillDeliveryExecutors.cs.meta
Assets/Game/Simulation/SkillModuleRegistry.cs
Assets/Game/Simulation/SkillModuleRegistry.cs.meta
Assets/Game/Simulation/SkillPreviewHarness.cs
Assets/Game/Simulation/SkillPreviewHarness.cs.meta
Assets/Game/Simulation/SkillRuntime.cs
Assets/Game/Simulation/SkillRuntime.cs.meta
Assets/Game/Simulation/SkillRuntimePrimitives.cs
Assets/Game/Simulation/SkillRuntimePrimitives.cs.meta
Assets/Game/Simulation/SkillSystems.cs
Assets/Game/Simulation/SkillSystems.cs.meta
Assets/Game/Simulation/SkillTargetingExecutors.cs
Assets/Game/Simulation/SkillTargetingExecutors.cs.meta
Assets/GameAssets/Placeholder/TestSkillContent.meta
Assets/GameAssets/Placeholder/TestSkillContent/TestDamageAura.asset
Assets/GameAssets/Placeholder/TestSkillContent/TestDamageAura.asset.meta
Assets/GameAssets/Placeholder/TestSkillContent/TestGroundArea.asset
Assets/GameAssets/Placeholder/TestSkillContent/TestGroundArea.asset.meta
Assets/GameAssets/Placeholder/TestSkillContent/TestM4SkillContentPack.asset
Assets/GameAssets/Placeholder/TestSkillContent/TestM4SkillContentPack.asset.meta
Assets/GameAssets/Placeholder/TestSkillContent/TestM4SkillContentPack.baked.json
Assets/GameAssets/Placeholder/TestSkillContent/TestM4SkillContentPack.baked.json.meta
Assets/GameAssets/Placeholder/TestSkillContent/TestOrbit.asset
Assets/GameAssets/Placeholder/TestSkillContent/TestOrbit.asset.meta
Assets/GameAssets/Placeholder/TestSkillContent/TestSingleProjectile.asset
Assets/GameAssets/Placeholder/TestSkillContent/TestSingleProjectile.asset.meta
Assets/Tests/EditMode/SkillContentTests.cs
Assets/Tests/EditMode/SkillContentTests.cs.meta
Assets/Tests/EditMode/SkillPreviewTests.cs
Assets/Tests/EditMode/SkillPreviewTests.cs.meta
Assets/Tests/EditMode/SkillRuntimeTests.cs
Assets/Tests/EditMode/SkillRuntimeTests.cs.meta
Assets/Tests/EditMode/SkillTargetingDeliveryTests.cs
Assets/Tests/EditMode/SkillTargetingDeliveryTests.cs.meta
Assets/Tests/EditMode/SkillTestFactory.cs
Assets/Tests/EditMode/SkillTestFactory.cs.meta
Docs/ADR/0006-m4-modular-skill-runtime.md
Docs/CHANGE_REQUEST_M4_SKILL_SCHEMA_V3.md
Docs/Reports/2026-07-26-m4-skill-runtime.md
Docs/Reports/2026-07-26-m4-strict-review.md
```

### 2.2 本次最小修复文件

| 文件 | 修复摘要 |
|---|---|
| `Assets/Game/Content/Runtime/ContentValidator.cs` | 验证 LevelPatch 累积结果；拒绝非可执行 Secondary Skill 引用 |
| `Assets/Game/Simulation/SkillRuntime.cs` | 递归预注册 Secondary 引用；释放 Owner 实例并复用空闲槽 |
| `Assets/Game/Simulation/SkillRuntimePrimitives.cs` | SkillInstanceHandle 增加代际，阻止旧句柄命中新实例 |
| `Assets/Game/Simulation/EntityStores.cs` | 增加程序集内部受控治疗边界，集中维护 Health 不变量 |
| `Assets/Game/Simulation/SkillSystems.cs` | Heal resolver 改为调用 ActorStore，不再直接写 Health 字段 |
| `Assets/Tests/EditMode/SkillContentTests.cs` | 增加 float 非有限、int 溢出、非可执行引用复现测试 |
| `Assets/Tests/EditMode/SkillRuntimeTests.cs` | 增加三级 ProcDepth 和 Owner 删除/句柄复用复现测试 |
| `Docs/EFFECT_MODULES.md` | 同步 Secondary、LevelPatch、OnStatusApplied 契约 |
| `Docs/TEST_PLAN.md` | 登记新增边界覆盖 |
| `Docs/Reports/2026-07-26-m4-strict-review.md` | 本审查报告 |

## 3. 关键架构决定

- asmdef 依赖图无循环；`Game.Core` 无引用；`Game.Simulation` 仅引用 `Game.Core` 和
  `Game.Content.Runtime`，且两者均为 `noEngineReferences: true`。
- LevelPatch 仍只在内容验证/编译阶段求值；固定 Tick 不解析字符串、不反射。验证器按同级
  作者顺序应用 Patch，并在每个等级边界验证最终值。
- 二次技能继续使用现有通用实例和 executor；递归预注册通过 `HasInstance` 去重，因此环形
  内容引用不会无限递归。
- Skill Instance 槽采用现有数组加空闲链表和 ushort 代际；不引入集合、Service Locator 或
  每 Tick 分配。删除 Owner 会释放其全部普通/secondary-only 实例并使旧句柄失效。
- Heal 仍由统一 Effect resolver 调度，但实际 Health 变更下沉到 ActorStore 的程序集内部
  `TryApplyHealing` 边界，维持外部只读 Health API。
- 无新增长期架构方向，ADR 0006 已覆盖 M4 Schema、注册表、编译数据和固定管线；本修复
  不需要新增 ADR。

### 3.1 范围外改动

`无`。62 个审查前文件均能映射到 M4 Schema、运行时、夹具、测试或文档；本审查只新增
报告并修改上述 M4 文件。未发现第三方包、正式资产、AI 资产或后续功能。

### 3.2 架构违规审查

| 检查 | 结果 | 证据/说明 |
|---|---|---|
| Simulation 引用 UnityEngine 对象 | PASS | Simulation/Core/Content.Runtime 静态搜索 0 命中；asmdef noEngineReferences |
| asmdef 循环 | PASS | 显式图遍历输出 `NO_CYCLES` |
| GameObject.Find / FindObjectOfType | PASS | 目标程序集静态搜索 0 命中 |
| Resources.Load | PASS | 目标程序集静态搜索 0 命中 |
| 高频 LINQ / 反射 / 字符串格式化 | PASS | Skill 固定 Tick 静态搜索 0 命中；唯一 `.Select` 是 executor 方法名 |
| 高频临时集合 | PASS | 目标/命令/事件使用持久数组缓冲；扩容只在容量增长时发生 |
| 全局 Service Locator | PASS | 静态搜索 0 命中；依赖由构造函数/Composition Root 传入 |
| 高频 Instantiate / Destroy | PASS | Simulation/Presentation/UI 静态搜索 0 命中 |
| UI/View 直接写 Simulation Store | PASS | UI/Presentation 搜索写 Store、Commands、HealthCurrent 等 0 命中；本里程碑未改 UI |
| Skill 直接修改 Health | PASS | Skill 文件无 HealthCurrent 写入；Damage/Status 进入 M3，Heal 调用 ActorStore 内部边界 |
| 内容 ID 与存档边界 | PASS | baked/hash 保存稳定 ContentId；RuntimeContentIndex 只在 Registry 成功后绑定；未修改存档格式 |
| 存档版本化/原子写入/迁移 | PASS | M4 Git diff 未触及既有存档实现或格式；Schema 1/2 内容兼容测试通过 |
| Placeholder / 正式资产 / provenance | PASS | 四个夹具仅位于 Placeholder、带 `content.placeholder`；无正式/AI/第三方资产 |
| 本地化 | PASS | 四个夹具的名称和描述均为 `content.*` Key；未新增用户可见硬编码正文 |
| 第三方运行时包 | PASS | `Packages` 和 `Assets/ThirdParty` 相对 M3 无改动 |

## 4. 实际执行的命令

以下为本次审查实际执行的关键命令；文档读取还实际使用了 `Get-Content -Raw` 依次读取
AGENTS、MASTER_PLAN、ARCHITECTURE、CONTENT_SCHEMA、CODEX_WORKFLOW、EXECUTION_ORDER、
ADR、TEST_PLAN、PERFORMANCE_BUDGET、EFFECT_MODULES 和报告模板。

```text
git -c safe.directory=E:/ai/free-world -C E:\ai\free-world status --short --branch
git -c safe.directory=E:/ai/free-world -C E:\ai\free-world diff --name-status framework-m3
git -c safe.directory=E:/ai/free-world -C E:\ai\free-world ls-files --others --exclude-standard
git -c safe.directory=E:/ai/free-world -C E:\ai\free-world diff --check

Get-ChildItem -Path Assets -Recurse -Filter *.asmdef | Get-Content -Raw
rg -n --glob '*.cs' 'UnityEngine|GameObject|MonoBehaviour|ScriptableObject|Sprite|AudioClip|Animator|SceneManager|Steam|Resources\.Load|GameObject\.Find|FindObjectOfType|FindAnyObjectByType|FindFirstObjectByType|System\.Reflection|Activator\.|Assembly\.|Enumerable\.|\.Where\s*\(|\.Select\s*\(|\.ToList\s*\(|\.ToArray\s*\(|ServiceLocator' Assets/Game/Simulation Assets/Game/Core Assets/Game/Content/Runtime
rg -n --glob '*.cs' 'SimulationWorld|ActorStore|world\.Actors|\.Commands|QueueDamage|QueueStatus|TryWrite|HealthCurrent' Assets/Game/UI Assets/Game/Presentation
rg -n --glob '*.cs' 'Instantiate\s*\(|Destroy\s*\(' Assets/Game/Simulation Assets/Game/Presentation Assets/Game/UI

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M4StrictReviewBeforeFixEditMode'
.\Scripts\test.ps1 -Platform PlayMode -ResultsDirectory 'TestResults\M4StrictReviewBeforeFixPlayMode'
.\Scripts\validate.ps1 -LogPath 'TestResults\M4StrictReviewBeforeFixValidation\validation.log'
.\Scripts\build-windows.ps1 -OutputPath 'Builds\WindowsDevelopmentM4StrictReviewBeforeFix\AzureSword.exe' -LogPath 'TestResults\M4StrictReviewBeforeFixBuild\build-windows.log'

.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M4StrictReviewReproduction'
$args=@('-batchmode','-nographics','-projectPath','E:\ai\free-world','-runTests','-testPlatform','EditMode','-testFilter','Game.Tests.EditMode.SkillRuntimeTests.RemovingOwnerReleasesSkillInstancesAndInvalidatesReusedHandles','-testResults','E:\ai\free-world\TestResults\M4StrictReviewLifecycleReproduction\editmode.xml','-logFile','E:\ai\free-world\TestResults\M4StrictReviewLifecycleReproduction\editmode.log')
Start-Process -FilePath $unity -ArgumentList $args -Wait -PassThru -WindowStyle Hidden
$args=@('-batchmode','-nographics','-projectPath','E:\ai\free-world','-runTests','-testPlatform','EditMode','-testFilter','Game.Tests.EditMode.SkillRuntimeTests.RemovingOwnerReleasesSkillInstancesAndInvalidatesReusedHandles','-testResults','E:\ai\free-world\TestResults\M4StrictReviewLifecycleAfterFix\editmode.xml','-logFile','E:\ai\free-world\TestResults\M4StrictReviewLifecycleAfterFix\editmode.log')
Start-Process -FilePath $unity -ArgumentList $args -Wait -PassThru -WindowStyle Hidden

.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M4StrictReviewFinalEditMode'
.\Scripts\test.ps1 -Platform PlayMode -ResultsDirectory 'TestResults\M4StrictReviewFinalPlayMode'
.\Scripts\validate.ps1 -LogPath 'TestResults\M4StrictReviewFinalValidation\validation.log'
.\Scripts\build-windows.ps1 -OutputPath 'Builds\WindowsDevelopmentM4StrictReviewFinal\AzureSword.exe' -LogPath 'TestResults\M4StrictReviewFinalBuild\build-windows.log'
Get-FileHash -Algorithm SHA256 'Builds\WindowsDevelopmentM4StrictReviewFinal\AzureSword.exe'

.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M4StrictReviewFinal2EditMode'
.\Scripts\test.ps1 -Platform PlayMode -ResultsDirectory 'TestResults\M4StrictReviewFinal2PlayMode'
.\Scripts\validate.ps1 -LogPath 'TestResults\M4StrictReviewFinal2Validation\validation.log'
.\Scripts\build-windows.ps1 -OutputPath 'Builds\WindowsDevelopmentM4StrictReviewFinal2\AzureSword.exe' -LogPath 'TestResults\M4StrictReviewFinal2Build\build-windows.log'
Get-FileHash -Algorithm SHA256 'Builds\WindowsDevelopmentM4StrictReviewFinal2\AzureSword.exe'
```

## 5. 测试结果

### 5.1 门禁结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 修复前 Unity 编译 | PASS | EditMode、PlayMode、Validation、Build 均完成脚本编译 |
| 修复前 EditMode | PASS | 120/120，`TestResults/M4StrictReviewBeforeFixEditMode/editmode.xml` |
| 修复前 PlayMode | PASS | 5/5，`TestResults/M4StrictReviewBeforeFixPlayMode/playmode.xml` |
| 修复前内容验证 | PASS | 脚本 `Validation result: PASS` |
| 修复前 Windows Development Build | PASS | Manifest `Succeeded` |
| 新边界复现 | FAIL | 124 total / 120 passed / 4 failed；两种 Patch、旧 Skill 引用、三级链均复现 |
| 生命周期复现 | FAIL | 1 total / 0 passed / 1 failed；删除 Owner 后 InstanceCount 仍为 1 |
| 生命周期定向复测 | PASS | 1/1，`TestResults/M4StrictReviewLifecycleAfterFix/editmode.xml` |
| 最终 Unity 编译 | PASS | 最终 EditMode、PlayMode、Validation、Build 均完成脚本编译 |
| 最终 EditMode | PASS | 125/125，0 failed，`TestResults/M4StrictReviewFinal2EditMode/editmode.xml` |
| 最终 PlayMode | PASS | 5/5，0 failed，`TestResults/M4StrictReviewFinal2PlayMode/playmode.xml` |
| 最终内容/项目验证 | PASS | `[Project Validation] PASS`，最终脚本 exit 0 |
| 最终 Windows Development Build | PASS | StandaloneWindows64，Manifest `Succeeded`，日志 `[M0 Build] PASS` |
| 性能/30 分钟 Soak | NOT RUN | 按 PERFORMANCE_BUDGET 在 M10 压力门禁执行 |

中间一次修复后 EditMode 已生成 124/124 PASS XML，Unity 日志明确 code 0；外层 PowerShell
在结果落盘后未及时返回，等待单元被手动终止。最终 125/125 命令已干净返回 exit 0，因此
最终结论不依赖该中间结果。

### 5.2 M4 验收矩阵

| 验收项 | 结果 | 主要证据 |
|---|---|---|
| Authoring/Runtime 含 Trigger、Condition、Targeting、Delivery、Effects、LevelPatches、Tags、Cooldown、资源成本 | PASS | Schema 3 round-trip、baked fixture、内容验证 |
| 五类显式注册表且无运行时反射扫描 | PASS | `SkillModuleRegistry.CreateDefault` 直接注册；静态反射搜索 0 命中 |
| EffectOp[] 与紧凑运行时等级数据 | PASS | DTO/hash round-trip、RuntimeSkillCatalog tests |
| Timer、OnHit、OnKill、OnDamageTaken、OnPickup、OnStatusApplied 基本行为 | PASS | `InitialTriggerModulesActivateOnlyForMatchingOwnerContext` 六组用例 |
| Self、Nearest、Random、Circle、Cone、Line、Ring、RandomPointAroundPlayer | PASS | SpatialGrid 几何/固定随机测试 |
| Instant、Projectile、Area、Aura、Orbit | PASS | Effect 命令路径及四种非即时生命周期测试 |
| 十种初始 Effect | PASS | M3 Damage/Status 与集中命令解析测试 |
| Skill Instance、等级、冷却、上下文、目标结果、命令缓冲 | PASS | 实例复用、LevelPatch、Trigger/Target/Effect 测试；新增 Owner 生命周期测试 |
| LevelPatch 烘焙路径/类型且运行时无字符串反射 | PASS | 错误路径/类型测试；新增累计结果与溢出测试；静态反射搜索 0 命中 |
| 四个程序化 Placeholder 测试技能 | PASS | Placeholder 目录/标签、Schema 3 baked catalog、固定种子 golden |
| 纯模拟 Preview 输出 DPS、命中数、触发次数 | PASS | 四技能两次固定种子结果相等 |
| 每种 Trigger 基本测试 | PASS | 六组参数化用例 |
| Targeting 在空间网格获得正确目标 | PASS | 八种 targeting 测试 |
| Projectile、Area、Aura、Orbit 生命周期 | PASS | 四组创建、命中、清理测试 |
| EffectOp 调用 M3 伤害/状态系统 | PASS | Damage/ApplyStatus/RemoveStatus 请求管线测试 |
| LevelPatch 各等级结果 | PASS | level 1/2 immutable 值；溢出/非有限值验证失败测试 |
| 同一技能可被两个角色实例复用 | PASS | 共享 Compiled Definition、独立资源状态测试 |
| ProcDepth 在二次技能中传播 | PASS | 一层为 1、三级链为 2 |
| 缺失模块 ID 在验证阶段失败 | PASS | MissingModuleId 测试 |
| 四个测试技能固定种子稳定 | PASS | `SkillPreviewTests` golden |
| 新测试技能不需要新增 MonoBehaviour | PASS | 无专用控制器；Simulation 无 MonoBehaviour；四技能只含 Authoring 资产 |
| 运行时不引用 Authoring ScriptableObject | PASS | asmdef 方向、Runtime Definition Unity Object 审计 |
| 高频技能执行不使用反射或 LINQ | PASS | 静态审计 0 命中；持久数组缓冲 |
| Skill 不直接修改 Health | PASS | Skill 文件无 HealthCurrent 写入；Damage 经 M3；Heal 调用 ActorStore 内部边界 |
| 验证器报告无效 LevelPatch 和模块引用 | PASS | 路径/类型/溢出/非有限/缺失模块/不可执行 Secondary 引用测试 |

## 6. 构建产物

- 配置：Unity `6000.3.20f1`，StandaloneWindows64，Development
- 路径：`Builds/WindowsDevelopmentM4StrictReviewFinal2/AzureSword.exe`
- 文件 Hash（SHA-256）：`5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopmentM4StrictReviewFinal2/BuildManifest.json`
- 构建日志：`TestResults/M4StrictReviewFinal2Build/build-windows.log`

## 7. 未执行项目

- 30 分钟 Soak 与 1,500/3,000/5,000 实体压力：`NOT RUN`。这是 M10 性能门禁；M4 没有
  M5 敌人/刷怪与后续拾取生产流程。
- Release Build：`NOT RUN`。当前里程碑适用并实际执行的是 Windows Development Build。
- Steam 平台集成：`NOT RUN`。M4 不修改平台层。
- 正式 VFX/UI/音频：`NOT RUN`。M4 明确禁止；夹具只使用 Placeholder PresentationId。

## 8. 已知限制和风险

- OnPickup Trigger 有纯模拟入口与基本行为测试，实际拾取事件生产者属于后续里程碑。
- Skill Preview 是固定目标、固定窗口的确定性回归工具，不是最终构筑或性能基准。
- 本次只做静态高频分配审计和功能测试；未宣称完成最终压力预算或长期内存曲线。
- Validation/Build 日志启动阶段出现 Unity LicenseClient 握手警告，但 Unity 随后正常取得可用
  状态，脚本 exit 0，验证输出 `[Project Validation] PASS`，Build Manifest 为 `Succeeded`。

## 9. 未完成项

- 当前 M4 强制交付、验收标准、内容验证和适用构建无未完成项。
- Git 暂存、Commit、Push 未执行，因为用户未要求发布变更。

## 10. 下一步前置条件

- 项目负责人审阅本报告、ADR 0006 和 CR-2026-002，并接受 M4 Review Gate。
- 在开始 M5 前保留最终 125/125 EditMode、5/5 PlayMode、Validation PASS 和 Development
  Build 证据；不得把本报告中的 `NOT RUN` 项目描述为已通过。
- M5 应只通过已注册模块接入敌人/遭遇；若需要新底层机制，先提交 Change Request。

## 11. 结论

`COMPLETE — PASS`

严格审查发现的五个失败复现均已在 M4 范围内最小修复；最终编译、EditMode、PlayMode、
内容验证和 Windows Development Build 全部实际通过。所有未执行检查均明确标为 `NOT RUN`。
