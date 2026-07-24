# Codex 结果报告

- 任务：Unity 空工程迁移与人工预检基线
- 里程碑：Preflight（尚未开始 M0）
- 分支：`main`
- Git Commit：空工程基线 `694381103704f8bc9ed3a701df4e903b049dc2aa`；预检提交 `e8b451ccb2fc27473476c43daa805480460c60e6`
- 日期：2026-07-24

## 1. 实现范围

本次完成以下工作：

- 将 Unity 工程的 `Assets/`、`Packages/`、`ProjectSettings/` 和 `.vsconfig` 迁移到仓库根目录。
- 排除并忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、测试结果和构建产物。
- 初始化 Git，关联 `https://github.com/justice-zhang/free-world.git`。
- 创建空工程基线提交和 `pre-framework-baseline` 标签。
- 将 `main` 和 `pre-framework-baseline` 发布到 `justice-zhang/free-world`。
- 里程碑开发约定使用 `milestone/*` 分支；远端 `main` 保护规则等待仓库管理员配置。
- 将 `Repository_Docs/` 内容复制到仓库根目录，并复制 `Templates/`。
- 填写项目变量并锁定 Unity `6000.3.20f1`。
- 设置用户级 `UNITY_PATH`。
- 实际运行 Unity CLI、空 EditMode、空 PlayMode 和 Windows x64 Development Build。

本次没有开始 M0，也没有创建游戏程序集、Bootstrap、运行时代码或正式内容。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `.gitignore` | 添加 Unity、IDE、构建和测试生成目录忽略规则 |
| `Assets/` | 迁移干净的 Universal 2D/URP 模板资产 |
| `Packages/` | 迁移并锁定 Unity 官方包清单 |
| `ProjectSettings/` | 迁移 Unity 项目设置并记录精确编辑器版本 |
| `AGENTS.md`、`README.md`、`Docs/` | 安装仓库治理、架构和执行文档 |
| `Templates/PROJECT_VARIABLES.md` | 填写 AzureSword 项目变量 |
| `Docs/ADR/0001-unity-version.md` | 接受并记录 Unity `6000.3.20f1` |
| `Docs/MASTER_PLAN.md` | 同步精确 Unity 技术基线 |

## 3. 关键架构决定

- Unity 版本锁定为 `6000.3.20f1`，以 `ProjectSettings/ProjectVersion.txt` 为唯一事实来源。
- 首发平台保持 Windows x64 / Steam。
- 项目名和根命名空间暂定为 `AzureSword`。
- 模拟频率保持 30 Hz，目标渲染帧率保持 60 FPS。
- 只发现 Unity registry/builtin 包，没有发现第三方包或参考项目资产。

长期版本约束记录在 `Docs/ADR/0001-unity-version.md`。

## 4. 实际执行的命令

```text
git init -b main
git remote add origin https://github.com/justice-zhang/free-world.git
git add -- .gitignore .vsconfig Assets Packages ProjectSettings
git commit -m "chore: establish clean Unity 6 LTS URP baseline"
git tag -a pre-framework-baseline -m "Clean Unity 6 LTS URP baseline"
git push -u origin main
git push origin pre-framework-baseline
gh api --method PUT repos/justice-zhang/free-world/branches/main/protection

Unity.exe -batchmode -nographics -quit -projectPath F:\Code\AzureSword -logFile -
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testResults Artifacts\Preflight\editmode-results.xml
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testResults Artifacts\Preflight\playmode-results.xml
Unity.exe -batchmode -nographics -quit -projectPath F:\Code\AzureSword -buildWindows64Player Builds\Preflight\AzureSword.exe -development
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| Unity CLI / 编译 | PASS | `Artifacts/Preflight/cli-import.log`、`cli-smoke-stdout.log`；返回码 0 |
| EditMode | PASS | `editmode-results.xml`：执行 0 项、失败 0 项；这是空工程基线 |
| PlayMode | PASS | `playmode-results.xml`：执行 0 项、失败 0 项；这是空工程基线 |
| 内容验证 | NOT RUN | M0/M1 尚未创建内容验证器 |
| Windows Development Build | PASS | 构建日志记录 `Build Finished, Result: Success.` 和返回码 0 |
| 性能/Soak | NOT RUN | 不适用于 Preflight，M10 执行 |

## 6. 构建产物

- 配置：Windows x64 Development Build
- 路径：`Builds/Preflight/AzureSword.exe`
- 主程序 SHA-256：`34C4E304E53E56499267DFD9C975C63DC279ED3011A69A8CA16EB207F1856A8F`
- Build Manifest：NOT RUN；M0/M10 构建脚本尚未实现

构建产物和原始日志已保留在本机并被 `.gitignore` 排除。

## 7. 未执行项目

- 尚未运行内容验证，因为验证器尚不存在。
- 尚未运行性能或 Soak Test，因为当前是空工程预检。
- 尚未启动 M0。
- 尚未配置 GitHub `main` 分支保护；当前登录账号只有 `WRITE` 权限，保护 API 需要 `ADMIN`。

## 8. 已知限制和风险

- EditMode 和 PlayMode 测试命令确实执行成功，但当前测试数均为 0；这只证明空测试基线成立。
- `AzureSword` 根命名空间在 M0 验收后将冻结，项目负责人应在开始 M0 前确认。
- 最低目标硬件是当前预检基线，后续需要用实际目标硬件验证性能预算。
- GitHub CLI `2.96.0` 已登录，远端基线已发布。
- 当前 GitHub 账号 `CodeFluxRiver` 对仓库的权限为 `WRITE`；调用分支保护 API 返回 HTTP 404。

## 9. 未完成项

- 项目负责人确认 `Templates/PROJECT_VARIABLES.md`。
- 使用仓库管理员账号配置 `main` 保护规则。
- 向 Codex 提供 `Prompts/00_MASTER_CONTROL.md` 和 `Prompts/02_M0_CLEAN_PROJECT.md` 后再开始 M0。

## 10. 下一步前置条件

- 确认项目变量，特别是 `<ROOT_NAMESPACE>` 和 `<MIN_TARGET_HARDWARE>`。
- GitHub CLI 切换到 `justice-zhang` 管理员账号，或为 `CodeFluxRiver` 授予 Admin 权限。
- 保持 Git 工作区干净。

## 11. 结论

`INCOMPLETE`

Unity 本地工程、Git 远端、测试和构建基线均已成立；完成 `main` 分支保护后才关闭 Preflight。M0 必须在独立的 `milestone/m0-clean-project` 分支执行。
