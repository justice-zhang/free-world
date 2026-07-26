# M9 严格里程碑审查

- 审查基线：`framework-m8` / `00351d2b0c860c98b5d21d64648c4fc514356ac3`
- 审查分支：`codex/m9-editor-tools`
- 日期：2026-07-26
- 结论：`PASS`

## 1. 范围与文件

相对 M8 基线最终共有 19 个修改文件、96 个新增文件、0 个删除文件：

- 修改：Addressables Settings/Default Group、三张 UI Localization 资产、4 个 Editor/asmdef 文件、
  2 个 Simulation 文件、Assembly Governance Test，以及 Architecture、Authoring、AI Pipeline、
  Localization、Test Plan、Execution Log、Known Issues。
- 新增：13 个 M9 Editor C# 文件及 `.meta`、M9 EditMode Test 及 `.meta`、完整
  `Assets/GameAssets/Placeholder/M9EditorTools/**` Fixture、ADR 0011、CR-2026-003、两份作者文档
  和两份 M9 报告。
- Scene、Package、Save Schema、Content Schema、ProjectSettings 和正式/第三方资源无改动。

所有改动均属于 M9 工具、测试、程序化 Placeholder 或同步文档；范围外改动：无。

## 2. 架构与禁用模式

| 检查 | 结果 | 说明 |
|---|---|---|
| asmdef 方向/循环 | PASS | ADR 0011 接受 `Game.Editor → Game.Simulation`；Simulation 不反向引用，Assembly Test 通过 |
| Core/Simulation Unity 隔离 | PASS | `Game.Simulation` 仍 `noEngineReferences: true`；新增纯值 API 不含 Unity Object |
| 查找/Resources/Service Locator | PASS | `GameObject.Find`、FindObject、`Resources.Load`、Service Locator 零命中 |
| 高频 LINQ/反射/字符串/临时集合 | PASS | 新增 Simulation Tick 路径零命中；仅 Editor 错误信息低频读取一次 `GetType().Name` |
| Instantiate/Destroy/逐敌 Update | PASS | M9 无运行时 View 或逐实体 MonoBehaviour |
| UI 直接写 Store | PASS | M9 Editor 只 Bake/解析并调用公开纯模拟 Harness |
| Scene/ProjectSettings | PASS | 无最终差异；Unity 测试/构建生成的临时 Resources、link.xml 和预加载项已清理 |

## 3. 内容、存档、资产与本地化

- Content/Save Schema 仍为 5/2；没有运行时索引或 Unity Object 进入存档/纯模拟。
- 向导生成稳定 ID、依赖和双语 Key；Catalog/Registry 不靠硬编码列表。
- 所有新增测试内容位于 Placeholder，带 `placeholder`、`development-only` 和 Pack 标签；没有
  `release` 标签。
- AI 正式文件必须通过来源、工具/版本、权利、条款、商业复核、状态和 SHA-256；Third Party 仍需
  notices 登记。
- Release 真实负向构建由 Build Preprocessor 阻断，且没有忽略开关。

## 4. 验收矩阵

| M9 验收项 | 结果 | 证据 |
|---|---|---|
| 向导覆盖 Pack 与十种 Definition | PASS | Fixture definitions=10；定向测试逐类型检查并 Bake |
| 自动 ID/目录/双语 Key/标签/测试/来源 | PASS | `WizardAutomationCreatesLocalizationLabelsTestsAndSourceRecord` |
| Validator Window 与 CLI 共用规则 | PASS | Window 调用同一 Validator；最终 Project Validation PASS |
| 重复 ID、缺引用、Pack/触发循环可定位 | PASS | 既有 Content Tests + M9 稳定触发循环路径测试 |
| 等级/概率/冷却/掉落/半径/Profile | PASS | Content Validator + M9 Visual Profile 管线；全量 EditMode/Validation PASS |
| Wave 阶段与运行时抽样一致 | PASS | 共用 `EncounterTimelineSampler`；M9 测试比对三个输出 |
| Skill Editor 与 Headless 一致 | PASS | 相同等级/属性/目标请求的 Summary/Geometry 精确一致 |
| Pack 报告和相同输入 Hash | PASS | 双次单元测试一致；最终 CLI 构建 6 Pack |
| Release 遇 Placeholder 失败 | PASS | 实际 Windows 非 Development Build Failed，无输出 EXE |
| provenance 缺失/权利不清/Hash 错误失败 | PASS | 三种失败路径自动测试，正式验证复用同一服务 |
| 第二角色/技能/地图不改核心程序集 | PASS | 三个 ID 进入 Registry；Fixture 树无 `.cs` |
| 内容人员简明文档 | PASS | `EDITOR_TOOLS.md`、`CONTENT_PACK_BUILDER.md` 和 Authoring 更新 |

## 5. 实际命令与结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 基线 | PASS | 重试 EditMode 172/172、PlayMode 9/9、Validation PASS；首次 Unity Crash 独立记录 |
| 最终编译 | PASS | 所有最终 Unity 门禁无编译错误 |
| EditMode | PASS | `TestResults/M9Final/editmode.xml`：181/181 |
| PlayMode | PASS | `TestResults/M9Final/playmode.xml`：9/9 |
| Project Validation | PASS | `TestResults/M9Final/validation.log` |
| Pack CLI | PASS | `TestResults/M9Final/pack-build.log`：6 Pack |
| Release 负向 Build | PASS | `TestResults/M9Final/release-build-negative.log`：预期阻断 |
| Windows Development Build | PASS | Manifest `Succeeded`；EXE SHA-256 `5D7EEB...C9C6` |
| 成功 Release Build | NOT RUN | Placeholder 按设计阻止 Release |
| 30 分钟 Soak/目标规模压力 | NOT RUN | M10 计划项 |

执行过 `git diff --check`；仅 Unity 序列化文件存在仓库既有换行转换提示，手写 C#/Markdown/JSON
无尾随空白。最终 Git diff 无 Scene、Package、ProjectSettings 或删除项。

## 6. 审查中最小修复

- Release 门禁从重复输出每个 Placeholder 收敛为一个可定位代表问题。
- provenance CSV/JSON 从“记录存在 + Hash”加强为同时验证来源、工具/版本、参考权利、条款、商业
  复核和批准状态。
- 真实 Release 负向 Harness 增加诊断捕获与 Unity 生成文件清理，防止非目标工作树污染。
- 补齐新公共 API 的 XML 摘要和 Culture-invariant SHA-256 文本输出。

## 7. 未解决问题

无阻止 M9 验收的问题。已接受限制和 M10 计划项见 `Docs/KNOWN_ISSUES.md`。
