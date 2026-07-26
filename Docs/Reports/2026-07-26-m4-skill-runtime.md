# Codex 结果报告

- 任务：模块化技能运行时
- 里程碑：M4
- 分支：`codex/m4-skill-runtime`
- Git Commit：未创建
- 日期：2026-07-26

## 1. 实现范围

完成 M4 指定范围：Schema 3 Skill Authoring/Runtime Definition、显式五类模块注册表、
EffectOp 烘焙与 ContentId/RuntimeContentIndex 两阶段绑定、Skill Instance/等级/冷却/资源、
六种 Trigger、八种 Targeting、五种 Delivery、十种 Effect、typed LevelPatch、可复用目标和
命令缓冲、二次技能 ProcDepth，以及输出 DPS/命中数/触发次数的纯模拟预览 Harness。

创建单体投射物、环绕物、地面区域和伤害光环四个 Schema 3 Placeholder Fixture；它们共享
通用 executor，没有专用 MonoBehaviour、Prefab 或正式表现资产。

没有实现敌人 AI、拾取系统生产者、构筑选择、正式 UI/VFX、正式内容或后续存档。OnPickup
Trigger 的通用提交入口和行为已实现、已测试；实际拾取事件生产者留给后续里程碑。

## 2. 新增和修改文件

| 文件/目录 | 变更摘要 |
|---|---|
| `Assets/Game/Content/Runtime/RuntimeSkillDefinition.cs`（及 `.meta`） | Schema 3 Skill、模块/Effect/LevelPatch 紧凑定义和稳定 ID 表 |
| `Assets/Game/Content/Runtime/SkillContentDtos.cs`（及 `.meta`） | 模块、EffectOp、LevelPatch 显式 JSON DTO 与 wire token |
| `Assets/Game/Content/Runtime/RuntimeContentDefinitions.cs` | 移出旧最小 Skill 定义，其他内容类型保持不变 |
| `Assets/Game/Content/Runtime/BakedContentCatalogDto.cs` | Schema 1/2 兼容和 Schema 3 Skill round-trip |
| `Assets/Game/Content/Runtime/ContentPackTopology.cs` | 支持的最高 Schema 提升为 3 |
| `Assets/Game/Content/Runtime/ContentRegistry.cs` | Registry 验证成功后第二阶段绑定 Effect 内容索引 |
| `Assets/Game/Content/Runtime/ContentValidator.cs` | 模块、Effect、引用类型及 LevelPatch 路径/索引/类型验证 |
| `Assets/Game/Content/Authoring/SkillAuthoring.cs` | 五类模块、Effects、LevelPatches、Tags、冷却和资源成本作者数据 |
| `Assets/Game/Content/Authoring/ContentBaker.cs` | Schema 3 可执行技能/旧技能形态门禁 |
| `Assets/Game/Simulation/SkillRuntimePrimitives.cs`（及 `.meta`） | Trigger/Target/Effect 上下文、编译等级、实例与 Runtime Catalog |
| `Assets/Game/Simulation/SkillModuleRegistry.cs`（及 `.meta`） | 五类显式 executor 接口和默认直接注册 |
| `Assets/Game/Simulation/SkillTargetingExecutors.cs`（及 `.meta`） | 八种 SpatialGrid Targeting |
| `Assets/Game/Simulation/SkillDeliveryExecutors.cs`（及 `.meta`） | Instant/Projectile/Area/Aura/Orbit 交付创建 |
| `Assets/Game/Simulation/SkillRuntime.cs`（及 `.meta`） | 冷却、事件触发、资源、Delivery 生命周期和可复用命令缓冲 |
| `Assets/Game/Simulation/SkillSystems.cs`（及 `.meta`） | M4 三阶段系统及 Effect 到 M3/模拟真值的集中路由 |
| `Assets/Game/Simulation/SkillPreviewHarness.cs`（及 `.meta`） | 固定种子纯模拟技能预览 |
| `Assets/Game/Simulation/SimulationWorld.cs`、`SimulationSystems.cs` | M4 固定 Pipeline、世界装配与 Cleanup 创建点 |
| `Assets/Game/Simulation/CombatSystems.cs` | Damage/Status/Death 生成 M4 事件 TriggerContext |
| `Assets/Game/Editor/M4TestSkillSetup.cs`（及 `.meta`） | 四个测试技能及 Pack 的可重复创建/Bake 命令 |
| `Assets/GameAssets/Placeholder/TestSkillContent.meta` 及目录内 4 个 `.asset`、Pack、baked JSON 和各 `.meta` | Schema 3 Placeholder 作者资产和确定性 Catalog |
| `Assets/Tests/EditMode/SkillTestFactory.cs`（及 `.meta`） | M4 测试装配夹具 |
| `Assets/Tests/EditMode/SkillContentTests.cs`（及 `.meta`） | Schema、Hash、模块/LevelPatch 验证和 Unity Object 审计 |
| `Assets/Tests/EditMode/SkillRuntimeTests.cs`（及 `.meta`） | 六 Trigger、十 Effect、等级、实例复用和 ProcDepth |
| `Assets/Tests/EditMode/SkillTargetingDeliveryTests.cs`（及 `.meta`） | 八 Targeting 和四种非即时 Delivery 生命周期 |
| `Assets/Tests/EditMode/SkillPreviewTests.cs`（及 `.meta`） | 四技能固定种子预览稳定性 |
| `Assets/Tests/EditMode/StatusContentTests.cs` | Schema 3 支持边界回归 |
| `Docs/ADR/0006-m4-modular-skill-runtime.md` | Schema 3、注册表、LevelPatch 与固定 Pipeline 决策 |
| `Docs/CHANGE_REQUEST_M4_SKILL_SCHEMA_V3.md` | Schema 3 Change Request 与影响/回滚 |
| `Docs/EFFECT_MODULES.md` | 全部稳定模块 ID、参数槽和 LevelPatch 路径登记 |
| `Docs/ARCHITECTURE.md`、`CONTENT_SCHEMA.md`、`CONTENT_AUTHORING_WORKFLOW.md`、`TEST_PLAN.md` | M4 架构、Schema、作者流程和覆盖同步 |
| `Docs/Reports/2026-07-26-m4-skill-runtime.md` | 本结果报告 |

所有新增 Unity C# 与资产均有对应 `.meta`。未引入第三方代码、运行时包、AI 或正式资产。

## 3. 关键架构决定

- Schema 1/2 的旧技能继续按非可执行元数据加载；完整模块技能必须使用 Schema 3 并重 Bake。
- 稳定 ContentId 保留于 Catalog/Hash/未来存档；Registry 成功后才绑定当前 Run 的
  RuntimeContentIndex，避免存档或内容格式泄漏运行时索引。
- Composition Root 通过 `SkillModuleRegistry.CreateDefault()` 直接注册 executor；Runtime
  Catalog 只解析一次，固定 Tick 不进行反射、程序集扫描或字符串模块查找。
- LevelPatch 只在作者/DTO 边界解析显式路径，转换为 enum target、effect index 和数值类型；
  运行时不保存路径字符串或反射修改对象。
- Skill Instance 只持 Owner、Level、Cooldown 和共享 Compiled Definition。两个角色复用
  同一内容时，等级和冷却相互独立。
- M4 管线固定为 SkillTrigger → Movement → SkillDelivery → SkillEffectResolution → M3
  Damage/Status/Death → Lifetime → Cleanup → EventFlush → SnapshotBuild。
- Skill/Executor 只写 Effect 命令。Damage/Status 经 M3 请求；治疗等由统一 EffectResolution
  处理，实例与具体模块不直接写 Health。
- 结构创建继续只在 Cleanup；Projectile 使用扫掠碰撞避免 Tick 穿透。
- 对应长期决定：`Docs/ADR/0006-m4-modular-skill-runtime.md`。

## 4. 实际执行的命令

```text
git -c safe.directory=E:/ai/free-world status --short --branch
git -c safe.directory=E:/ai/free-world switch -c codex/m4-skill-runtime

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M4Baseline'
.\Scripts\test.ps1 -Platform PlayMode -ResultsDirectory 'TestResults\M4BaselinePlayMode'
.\Scripts\validate.ps1 -LogPath 'TestResults\M4Baseline\validation.log'

& $env:UNITY_PATH -batchmode -nographics -projectPath 'E:\ai\free-world' `
  -executeMethod Game.Editor.M4TestSkillSetup.RunFromCommandLine `
  -logFile 'E:\ai\free-world\TestResults\M4FixtureSetup.log'

.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M4FinalEditMode'
.\Scripts\test.ps1 -Platform PlayMode -ResultsDirectory 'TestResults\M4FinalPlayMode'
.\Scripts\validate.ps1 -LogPath 'TestResults\M4FinalValidation\validation.log'
.\Scripts\build-windows.ps1 `
  -OutputPath 'Builds\WindowsDevelopmentM4\AzureSword.exe' `
  -LogPath 'TestResults\M4FinalBuild\build-windows.log'

Get-FileHash -Algorithm SHA256 'Builds\WindowsDevelopmentM4\AzureSword.exe'
$files=Get-ChildItem 'Assets\Game\Simulation' -Filter 'Skill*.cs' | ForEach-Object FullName
rg -n 'System\.Linq|System\.Reflection|Enumerable\.|GetType\(|Activator\.|Assembly\.' $files
rg -n 'GameObject|MonoBehaviour|ScriptableObject|UnityEngine|Resources\.Load|FindObjectOfType' $files
git -c safe.directory=E:/ai/free-world diff --check
git -c safe.directory=E:/ai/free-world status --short
```

基线第一次在沙箱身份下启动 Unity 因 SourceAssetDB 锁失败；在获准的主机进程中重跑。
`-Platform All` 基线包装器在 600 秒超时前已产出 EditMode 97/97，但没有完成 PlayMode；随后
用独立 PlayMode 命令得到 5/5。基线验证独立执行并通过。

实现迭代中实际出现并修复的失败：旧 Schema 支持断言导致 96/97；Targeting out 参数触发
CS0177；M3 ProcDepth 诊断被无关事件增加；高速 Projectile 预览发生 Tick 穿透（119/120）。
固化预览 golden 时第一次把请求时长 `3f` 误写为实际 Tick 时长，得到 119/120；改为实际
模拟时长 `3.0000002f` 后通过。每项修复后均重跑，最终结果见下一节；没有删除断言、注释
测试或绕过验证器。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| M4 前 EditMode 基线 | PASS | 97/97，`TestResults/M4Baseline/editmode.xml` |
| M4 前 PlayMode 基线 | PASS | 5/5，`TestResults/M4BaselinePlayMode/playmode.xml` |
| M4 前内容验证 | PASS | `TestResults/M4Baseline/validation.log` |
| Unity 编译 | PASS | 最终 EditMode、PlayMode 与 Windows Build 均完成脚本编译 |
| 最终 EditMode | PASS | 120/120，0 failed，`TestResults/M4FinalEditMode/editmode.xml` |
| 最终 PlayMode | PASS | 5/5，0 failed，`TestResults/M4FinalPlayMode/playmode.xml` |
| 内容/项目验证 | PASS | `Validation result: PASS`，`TestResults/M4FinalValidation/validation.log` |
| Windows Development Build | PASS | StandaloneWindows64，Manifest result `Succeeded`，Build Log 含 `[M0 Build] PASS` |
| 静态架构审计 | PASS | Skill 热路径 LINQ/反射/Unity API 为 0 命中；实例/executor Health 写入为 0 命中；asmdef `noEngineReferences: true` |
| 性能/30 分钟 Soak | NOT RUN | 按 `PERFORMANCE_BUDGET.md` 在 M10 压力门禁执行 |

四技能 baked catalog：Schema 3，4 definitions，Hash
`546484b9f3da78212cfd842b6ed26a76d18cf3b70724a75ab275820b525a1385`。

固定种子 `0x4D34554C`、实际模拟 `3.0000002` 秒的预览 golden：

| Skill | DPS | 命中数 | 触发数 |
|---|---:|---:|---:|
| `test.skill.single_projectile` | 23.9999981 | 6 | 6 |
| `test.skill.orbit` | 189.333313 | 142 | 1 |
| `test.skill.ground_area` | 189.999985 | 114 | 4 |
| `test.skill.damage_aura` | 159.999985 | 160 | 3 |

## 6. 构建产物

- 配置：Unity `6000.3.20f1`，StandaloneWindows64，Development
- 路径：`Builds/WindowsDevelopmentM4/AzureSword.exe`
- SHA-256：`5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopmentM4/BuildManifest.json`
- 构建日志：`TestResults/M4FinalBuild/build-windows.log`

## 7. 未执行项目

- 30 分钟 Soak、1,500 敌人/3,000 投射物/5,000 拾取物压力 JSON：`NOT RUN`。M4 尚未实现
  M5 敌人/刷怪或后续拾取流程，完整预算和长期内存趋势按 M10 门禁执行。
- Release Build：`NOT RUN`；当前里程碑实际生成并验证的是 Windows Development Build。
- 正式 VFX/UI/音频检查：`NOT RUN`；M4 明确禁止实现，四个 Fixture 只含稳定 Placeholder ID。

## 8. 已知限制和风险

- OnPickup Trigger 已有可测试入口，但实际拾取系统尚未实现；不能据此宣称完整拾取流程可用。
- 预览 Harness 使用固定种子、静止 Owner/目标和有限窗口，适合内容确定性回归，不等同于
  最终角色构筑、移动场景或高并发性能基准。
- 技能资源是纯模拟 Actor 侧存储，尚无 UI、存档或具体资源类型；这些属于后续里程碑。
- Area/Aura/Orbit 当前使用通用 Actor SpatialGrid 查询和每实例命中间隔；完整高并发成本
  尚未测量，不能宣称达到最终性能预算。
- Schema 1/2 旧技能不可执行；内容迁移必须显式升级 Pack 到 Schema 3 并重新 Bake。

## 9. 未完成项

- 当前 M4 强制交付、测试、内容验证和适用构建无未完成项。
- Git 暂存、Commit、Push 未执行，因为用户未要求发布变更。

## 10. 下一步前置条件

- 项目负责人审查并接受 ADR 0006、CR-2026-002、Schema 3 兼容规则和本报告。
- M4 Review Gate 应复跑最终 EditMode/PlayMode、内容验证和 Development Build，并检查四个
  Fixture 的固定种子预览摘要。
- Review Gate 通过后才可开始 M5；M5 需要通过现有 Trigger/Targeting/Delivery/Effect
  注册边界接入敌人、刷怪、遭遇和地图，不得为具体技能修改核心运行时。

## 11. 结论

`COMPLETE`

M4 指定的强制交付、测试、内容验证和适用 Windows Development Build 均已实际通过；
性能/Soak 和明确禁止的后续内容均标为 `NOT RUN`，没有表述为通过。
