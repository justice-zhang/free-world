# Codex 结果报告

- 任务：核心类型与内容系统
- 里程碑：M1
- 分支：`codex/m1-content-system`
- Git Commit：`8edcfadee2f2d3824dee5db0a401146e51e39f22`
- 日期：2026-07-25

## 1. 实现范围

本次只实现 M1：

- Core 稳定 ID/Tag、版本、运行时索引以及结构化 Result/Error。
- 内容 Pack Manifest、依赖版本范围、稳定拓扑排序和纯运行时定义。
- 确定性 SHA-256 Baked Catalog、DTO、Validator 和事务式 Registry。
- ContentPack、Character、Skill、Enemy、Map 最小 ScriptableObject 作者数据与 Baker。
- 一个包含四个定义的程序化 Placeholder 测试 Pack。
- Bootstrap 对 baked 测试 Catalog 的 Hash 校验、注册和摘要输出。
- 作者内容、baked 文件、跨 Pack 引用、命令行和构建前验证。

没有实现实体、战斗、技能执行、刷怪、地图运行时、正式资源或后续里程碑功能。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Core/ContentId.cs`、`ContentTag.cs` | canonical 字符串 ID/Tag、完整字符串比较和稳定 Hash |
| `Assets/Game/Core/ContentVersion.cs`、`RuntimeContentIndex.cs` | 严格版本与仅本次加载有效的紧凑索引 |
| `Assets/Game/Core/ContentError.cs`、`Result.cs` | 带 ID、Pack 和作者路径的结构化错误 |
| `Assets/Game/Content/Runtime/*.cs` | Manifest、拓扑、四类运行时定义、Catalog/DTO/Hash、Validator、Registry |
| `Assets/Game/Content/Authoring/*.cs` | 五类作者资产、路径解析接口和 ContentBaker |
| `Assets/Game/Editor/ContentBakeUtility.cs` | 显式 Pack 发现和确定性 JSON Bake |
| `Assets/Game/Editor/ContentProjectValidator.cs` | 作者/baked/依赖/引用项目门禁 |
| `Assets/Game/Editor/M1TestContentSetup.cs` | 生成测试 Pack、Bake 并配置 Bootstrap |
| `Assets/Game/Application/GameApplication.cs` | 应用拥有 ContentRegistry，并在进入 MainMenu 前加载 |
| `Assets/Game/Infrastructure/GameBootstrapper.cs` | 解析 Scene 引用的 TextAsset、校验并输出摘要 |
| `Assets/Game/**.asmdef`、`Assets/Tests/**.asmdef` | 增加 M1 所需的单向直接依赖 |
| `Assets/GameAssets/Placeholder/TestContent/**` | 五个作者资产和一个 baked JSON 测试 Catalog |
| `Assets/Scenes/Bootstrap.unity` | 显式引用 baked 测试 Catalog |
| `Assets/Tests/EditMode/Content*Tests.cs` | ID、碰撞、拓扑、Baker、Hash、Validator、Registry 测试 |
| `Assets/Tests/PlayMode/BootstrapPlayModeTests.cs` | Pack/条目摘要和 Registry 启动验证 |
| `Assets/Game/Editor/ProjectGovernanceValidator.cs`、`Scripts/validate.ps1` | M0+M1 统一命令行验证 |
| `Docs/CONTENT_SCHEMA.md`、`Docs/ADR/0003-content-packs.md` | 固化 M1 Schema 和内容包决策 |
| `Docs/ARCHITECTURE.md`、`Docs/TEST_PLAN.md` | 更新实际依赖和测试覆盖 |
| `Docs/CONTENT_AUTHORING_WORKFLOW.md`、`README.md` | 记录新增内容、Bake 和验证流程 |

对应 Unity `.meta` 文件随新增资产和源码一并生成。

## 3. 关键架构决定

- `ContentId` 永远保留 canonical 完整字符串；FNV-1a Hash 只用于查找桶。测试使用
  已知碰撞 `test.collision.3629` / `test.collision.21d94` 证明碰撞不改变身份。
- 作者 ID 必须已经 canonical；外部输入工厂可 trim/lowercase，但构建不静默修正作者资产。
- Pack 版本为严格 `major.minor.patch`，拓扑在可选节点间保留输入顺序。
- Registry 不枚举 Character/Skill/Enemy/Map 类型，只遍历基类定义；加载失败不改变旧状态。
- 磁盘 DTO 显式编码已支持的 Schema 类型，不使用反射扫描；新增磁盘类型属于 Schema 变更。
- Hash 输入使用固定字段顺序、长度前缀字符串、invariant 数字格式和定义顺序。
- Unity Object 只存在于作者层和最外层 Bootstrap TextAsset 引用；Runtime Catalog 无 Unity 依赖。

长期约束记录在更新后的 ADR 0003。

## 4. 实际执行的命令

```text
git -c safe.directory=E:/ai/free-world status --short --branch
git -c safe.directory=E:/ai/free-world tag --list --sort=-creatordate
git -c safe.directory=E:/ai/free-world ls-remote --tags origin
git -c safe.directory=E:/ai/free-world switch -c codex/m1-content-system

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform All -ResultsDirectory TestResults/M1Baseline
.\Scripts\validate.ps1 -LogPath TestResults/M1Baseline/validation.log

Unity.exe -batchmode -nographics -projectPath E:\ai\free-world
  -executeMethod Game.Editor.M1TestContentSetup.RunFromCommandLine
  -logFile E:\ai\free-world\TestResults\m1-setup.log

.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory TestResults/M1Iteration1
.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory TestResults/M1Iteration2
.\Scripts\test.ps1 -Platform PlayMode -ResultsDirectory TestResults/M1Iteration2
.\Scripts\validate.ps1 -LogPath TestResults/M1Iteration2/validation.log

Unity.exe -batchmode -nographics -quit -projectPath E:\ai\free-world
  -logFile E:\ai\free-world\TestResults\M1Final\compile.log
.\Scripts\test.ps1 -Platform All -ResultsDirectory TestResults/M1Final
.\Scripts\validate.ps1 -LogPath TestResults/M1Final/validation.log
.\Scripts\build-windows.ps1
  -OutputPath Builds/WindowsDevelopment/AzureSword.exe
  -LogPath TestResults/M1Final/build-windows.log

Builds\WindowsDevelopment\AzureSword.exe -batchmode -nographics
  -logFile TestResults\M1Final\player-smoke.log

rg -n "UnityEngine|UnityEditor|GameObject|ScriptableObject|Sprite|AudioClip|AssetReference|MonoBehaviour"
  Assets/Game/Core Assets/Game/Content/Runtime
rg -n "Resources\.Load|GameObject\.Find|FindObjectOfType|System\.Linq|Assembly\.GetTypes"
  Assets/Game Assets/Tests
git -c safe.directory=E:/ai/free-world diff --check -- '*.cs' '*.asmdef' '*.json' '*.md' '*.ps1'
```

真实迭代记录：

- 第一次沙箱基线因 Unity License/Library 锁未生成 XML；改用宿主上下文后基线通过。
- Fixture 首两次编译分别暴露辅助类型可见性和缺失直接 asmdef 引用，修复后 Unity 退出码 0。
- 第一次 M1 EditMode 为 30/32；两个失败是 NUnit 数组 Count 写法和来源路径断言错误，
  未放宽产品校验，修正测试后重跑为 32/32。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| M0 基线 EditMode | PASS | `TestResults/M1Baseline/editmode.xml`：6/6 |
| M0 基线 PlayMode | PASS | `TestResults/M1Baseline/playmode.xml`：4/4 |
| 编译 | PASS | `TestResults/M1Final/compile.log`：Unity 退出码 0，CS 错误匹配 0 |
| EditMode | PASS | `TestResults/M1Final/editmode.xml`：32/32，失败 0 |
| PlayMode | PASS | `TestResults/M1Final/playmode.xml`：5/5，失败 0 |
| 内容/治理验证 | PASS | `TestResults/M1Final/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | `TestResults/M1Final/build-windows.log`：`Build Finished, Result: Success.` |
| 构建启动冒烟 | PASS | `TestResults/M1Final/player-smoke.log`：`packs=1, entries=4` |
| 静态边界审计 | PASS | Core/Runtime 无 Unity 类型；产品代码无禁用 API/反射类型扫描 |
| 性能/Soak | NOT RUN | M1 没有模拟或高频运行负载；按 M2-M10 对应门禁执行 |

最终测试包含全部 M0 原有测试，M0 回归未失败。

## 6. 构建产物

- 配置：Windows x64 Development Build
- 路径：`Builds/WindowsDevelopment/AzureSword.exe`
- EXE SHA-256：
  `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopment/BuildManifest.json`
- Manifest SHA-256：
  `88D85ABBCA1A895EE06E2FCF3EE719BA6B9F785BB4090B9D5C85E95637D0E3A7`
- Manifest 结果：Unity `6000.3.20f1`、`StandaloneWindows64`、Development、
  `Succeeded`
- 测试 Catalog 内容 Hash：
  `533691a7aa7017568acd1f965c7d320cda755ee90d129d93c6c59a7ea67e3509`
- 测试 Catalog 文件 SHA-256：
  `6307514BD0BCCD4A6D5018F34554F05E1C43787B02F641CE849772C9C07BFCA3`

构建产物和测试日志由 `.gitignore` 排除，保留在本机工作区。

## 7. 未执行项目

- 性能/Soak：M1 没有模拟 Tick、实体或高频路径，不适用。
- Release Build：本任务要求适用构建，已执行 Windows Development Build；正式 Release
  资产门禁和打包属于后续发布里程碑。
- Steam、远程 Addressables Catalog 和 DLC：不属于 M1。

## 8. 已知限制和风险

- M1 磁盘 DTO 仅支持 Character、Skill、Enemy 和 Map；新增磁盘类型必须显式升级 Schema。
- Bootstrap 当前只加载 Scene 显式引用的测试 TextAsset。正式 Addressables Pack 生命周期、
  异步句柄和 DLC 加载不在本里程碑。
- Localization Key 已强制非空，但 Locale 表和缺 Key 验证要到本地化里程碑落地。
- 后续技能等级、Evolution、概率、Encounter 等验证尚无对应 Schema，因此未提前实现。

## 9. 未完成项

当前 M1 强制交付项无未完成内容。

## 10. 下一步前置条件

- 人工审查 M1 Git diff、测试 XML、验证日志和构建日志。
- 通过 M1 审查门禁后提交、合并并创建 `framework-m1` 标签。
- 在标签前不得开始 M2。

## 11. 结论

`COMPLETE`

## 12. 审查门禁补充

同日严格审查发现并最小修复了被引用资产错误路径归因、运行时 backing array 泄漏和
新增 public API XML 文档缺口。最终门禁为 EditMode 33/33、PlayMode 5/5、命令行验证
PASS、Windows Development Build PASS。权威审查记录见
`Docs/Reports/2026-07-25-m1-review-gate.md`。

## 13. GitHub 集成

- 实现 PR：[GitHub PR #4](https://github.com/justice-zhang/free-world/pull/4)
- 实现 merge commit：`268e3f23535e304dfc4843d85ada5d2a47f642a0`
- 执行日志、已知问题和最终标签由 M1 收尾 PR 固化。
