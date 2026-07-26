# Codex 结果报告

- 任务：版本化存档、本地化与平台边界
- 里程碑：M8
- 分支：`codex/m8-save-localization-platform`
- Git Commit：`baddd6914c07a173cc4a6091886f1580c9a1f29d`
- 日期：2026-07-26

## 1. 实现范围

完成 `settings.json`、`profile.json`、`run_recovery.json` 三种独立 Schema 2 纯数据模型；实现
`ISaveStorage`、同目录 temp/flush/backup/atomic replace、取消、SHA-256 信封、主文件失败后的备份
恢复、连续迁移注册表和三类 v1→v2 迁移。存档只记录稳定 ContentId、Pack 版本和纯值。

完成 Unity Localization 的英文、简体中文和扩展 Pseudo Locale、103 个双语 Key、运行时 Key 解析、
语言设置保存、Windows CJK 系统字体 fallback，以及正式 Project Validation 的 Locale/表/Key 非空
门禁。完成 Achievements、Stats、Cloud、RichPresence、Identity 平台子服务、Null 实现、Cloud
Revision/保守冲突策略和 Application Event 路由；未引入真实 Steam SDK。

未实现真实云传输、Steam SDK、用户冲突选择页、任意 Tick 的完整局中快照恢复、正式字体资产、
Release Build、30 分钟 Soak 或目标实体规模性能测试。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Application/Save*.cs` | 三种模型、存储/Codec 结果、协调器、诊断和显式迁移注册表 |
| `Assets/Game/Application/ApplicationEvents.cs`、`PlatformApplicationEventRouter.cs` | 低频应用事件与非阻塞平台统计/成就路由 |
| `Assets/Game/Infrastructure/LocalFileSaveStorage.cs`、`UnityJsonSaveCodec.cs`、`M8RuntimeServices.cs` | 原子本地文件、SHA-256 Json 信封、加载默认值和文件生命周期 |
| `Assets/Game/Infrastructure/GameBootstrapper.cs`、M7 Flow/Host/Factory | Bootstrap 组合 M8，设置/开局/结算事件接入 |
| `Assets/Game/Platform/**` | 五个子服务、Null 实现、Cloud Revision 与冲突策略 |
| `Assets/Game/UI/UnityLocalizationService.cs`、Presenter/View/Input 相关文件 | 三语言解析、语言切换、CJK 字体和 Binding 设置持久化 |
| `Assets/Game/Editor/M8ProjectSetup.cs`、`LocalizationProjectValidator.cs` | 可重复生成语言资产并验证 103 个双语 Key |
| `Assets/GameAssets/Localization/**`、`Assets/AddressableAssetsData/**` | Unity 生成的 Locale、String Table 和 Addressables 组 |
| `Assets/Tests/EditMode/M8SaveLocalizationPlatformTests.cs` | 9 个存档、本地化、平台、云冲突和 Assembly 测试 |
| `Assets/Tests/PlayMode/M8SaveLocalizationFlowPlayModeTests.cs` | Null 平台下语言、设置、恢复和 Profile 完整流程 |
| `Docs/ADR/0010-*.md`、`Docs/ChangeRequests/CR-2026-002-*.md` | 存档/本地化/平台长期决定和 Save Schema 审批记录 |
| `Docs/SAVE_FORMAT.md`、`LOCALIZATION_KEYS.md`、`STEAM_INTEGRATION_BOUNDARY.md` | Wire 格式、Key 规范和平台/云边界 |
| `Docs/ARCHITECTURE.md`、`TEST_PLAN.md`、`KNOWN_ISSUES.md` | Assembly、组合、测试事实和限制同步 |

实现提交相对 `framework-m7` 新增/修改 100 个文件，删除 0 个文件。

## 3. 关键架构决定

- 采用 ADR 0010：Application 拥有纯模型/合约，Infrastructure 拥有 Unity/文件实现；本地原子文件
  始终为真值，云只同步 Revision 和文件。
- SHA-256 校验 payload 后才迁移；主文件失败才读取上一版本备份。Profile 缺失可选内容告警，
  RunRecovery 缺失必需内容明确拒绝。
- Presenter/Content/诊断只传 Key；View 通过 Unity Localization 解析。Project Validation 把新增
  内容 Key 未翻译变成构建前失败。
- 平台更新只消费 Application Event；Simulation 无平台引用。真实异步后端不会被事件路由同步
  阻塞，异常转换为 `platform.failed`。
- Save Schema 首次建立属于长期兼容性变化，已由 CR-2026-002 授权并记录回滚边界。

## 4. 实际执行的命令

```text
git fetch --prune --tags origin
git switch main
git pull --ff-only origin main
git switch -c codex/m8-save-localization-platform

Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testResults TestResults/M8Baseline/editmode.xml -logFile TestResults/M8Baseline/editmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testResults TestResults/M8Baseline/playmode.xml -logFile TestResults/M8Baseline/playmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.ProjectValidationCommand.Run -logFile TestResults/M8Baseline/validation.log

Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.M8ProjectSetup.RunFromCommandLine -logFile TestResults/M8/setup-final.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.M8SaveLocalizationPlatformTests -testResults TestResults/M8/editmode-targeted-final.xml -logFile TestResults/M8/editmode-targeted-final.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.M8SaveLocalizationFlowPlayModeTests -testResults TestResults/M8/playmode-targeted-final2.xml -logFile TestResults/M8/playmode-targeted-final2.log

Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testResults TestResults/M8Final/editmode.xml -logFile TestResults/M8Final/editmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testResults TestResults/M8Final/playmode.xml -logFile TestResults/M8Final/playmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.ProjectValidationCommand.Run -logFile TestResults/M8Final/validation.log
$env:BUILD_OUTPUT='F:\Code\AzureSword\Builds\WindowsDevelopment\AzureSword.exe'; Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.WindowsDevelopmentBuild.BuildFromCommandLine -logFile TestResults/M8Final/build.log

rg -n <禁用模式> Assets/Game/Application Assets/Game/Infrastructure Assets/Game/Platform Assets/Game/Simulation Assets/Game/UI
git diff --check
git diff --stat
git commit -m "feat: implement M8 save localization and platform boundaries"
git push -u origin codex/m8-save-localization-platform
gh pr create --repo free-world-team/free-world --base main --head codex/m8-save-localization-platform
```

一次完整 M8 测试类初跑因测试用 `.Result` 等待捕获 Unity Context 的异步文件 I/O 而停滞；只终止
该次启动的批处理进程，随后将生产 I/O 续延改为 `ConfigureAwait(false)`，测试改为 async 并通过。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| M7 基线 | PASS | `TestResults/M8Baseline`：EditMode 163/163、PlayMode 8/8、Project Validation PASS |
| 编译 | PASS | 最终测试、验证和 Development Build 均完成脚本编译，无 C# 错误 |
| M8 定向 EditMode | PASS | `editmode-targeted-final.xml`：9/9 |
| M8 定向 PlayMode | PASS | `playmode-targeted-final2.xml`：1/1 |
| EditMode | PASS | `TestResults/M8Final/editmode.xml`：172/172，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M8Final/playmode.xml`：9/9，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M8Final/validation.log`：`[Project Validation] PASS`；103 个双语 Key |
| Windows Development Build | PASS | `BuildManifest.json`：`Succeeded`、`StandaloneWindows64`、Development |
| 性能/Soak | NOT RUN | 30 分钟和 1,500/3,000/5,000 压力门禁按计划在 M10 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopment/AzureSword.exe`
- 文件 Hash：SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopment/BuildManifest.json`，`result: Succeeded`，
  `generatedAtUtc: 2026-07-26T14:56:30.8238982Z`

## 7. 未执行项目

- Release Build：`NOT RUN`。当前适用门禁为 Windows Development Build。
- 真实 Steam/Cloud 联调：`NOT RUN`，M8 明确禁止引入真实 Steam SDK。
- 30 分钟 Soak、1,500 敌人、3,000 投射物、5,000 拾取物压力和性能 JSON：`NOT RUN`，M10 项。

## 8. 已知限制和风险

- RunRecovery 当前是开局恢复锚点，不是任意 Tick 的完整模拟快照，也没有继续本局页面。
- Cloud 只有边界和冲突策略；无远端传输或用户选择 UI。
- Placeholder 中文字体依赖 Windows 系统字体候选；正式字体需单独验证嵌入许可。
- Unity 生成 Localization `.asset/.meta` 的空 YAML 字段含尾随空格；手写 C#/Markdown/JSON/asmdef
  通过 whitespace 检查，生成文件按 M0-KI-007 保留 Unity 格式。

## 9. 未完成项

- 当前 M8 强制交付无未完成项。
- `Docs/KNOWN_ISSUES.md` 已登记 M8-KI-001 至 M8-KI-004，没有阻止 M9 开始的 `OPEN` 问题。

## 10. 下一步前置条件

- GitHub PR #14 合并，`main` 与合并提交一致并创建/推送 `framework-m8` 标签。
- 后续平台任务只能通过现有 Facade/事件/Cloud Conflict 边界接入，不得让 SDK 进入 Simulation。
- 若下一里程碑需要真正续局，先定义可重建且版本化的完整 RunRecovery Snapshot，不得序列化 World。

## 11. 结论

`COMPLETE`
