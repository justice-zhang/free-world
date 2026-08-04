# Codex 结果报告：Qinglan Demo G0.4 资产生产计划

- 任务：冻结正式视觉、音频、字体、本地化、预算与权利生产清单
- 里程碑：Qinglan Demo G0.4
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-04

## 1. 实现范围

完成 41 个机器可读生产批次、视觉/音频/字体/本地化预算、目录/Addressables/Release 标签、来源状态机、
provenance 模板、第三方字体候选和 G3 执行顺序。G0.4 不生成或导入任何正式二进制，不修改 Scene、
Addressables、Localization Table、Package、代码或 Release 输入。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Docs/DemoDevelopment/09_G0_4_ASSET_PRODUCTION_PLAN.md` | 完整艺术圣经、预算、来源/权利、字体、本地化和分包门禁 |
| `Docs/DemoDevelopment/Assets/G0_4_ASSET_MANIFEST.csv` | 27 ART、9 AUDIO、2 FONT、3 LOC 批次 |
| `Docs/DemoDevelopment/Assets/G0_4_PROVENANCE_TEMPLATE.json` | Schema 1 合规记录模板，安全的未批准默认值 |
| `Docs/DemoDevelopment/01_ARCHITECTURE_AND_REPOSITORY.md` | 正式组补充现有 Validator 识别的基础 `release` 标签 |
| `Docs/AI_ASSET_PIPELINE.md` | 回链 Qinglan 计划并暴露 FirstParty 校验缺口 |
| M13/M15/README、执行日志、已知问题 | 同步生产范围、自动门禁前置和下一包 |

## 3. 关键架构决定

- 正式画面为清朗东方幻想、2D/轻斜俯视；P0 危险与玩家攻击分离且至少双通道可读。
- AI 图只接收本仓库文字设计，不使用参考项目或无权图片；UI 正文不烘进图片。
- 音频使用第一方程序化合成/原创编曲并保留脚本、参数、Seed、源/输出 Hash。
- 字体只锁 Noto CJK SC 官方候选；G3.3 才固定版本/文件/Hash/Notice/TMP 证据。
- 所有正式 Addressables 同时具有 `release` 与类别标签，运行时只由 Profile/ContentId 间接引用。
- FirstParty 与 AI 使用同等级 provenance；当前自动扫描缺口必须在 G3.1 先修复。

## 4. 实际执行的命令

```text
Get-Content / rg（审计 M13/M15/M16、AI pipeline、Addressables、Localization、现有 Validator）
Web 官方来源核对（Noto CJK 官方仓库/OFL 文件、OpenAI 当前条款）
New-Item Docs/DemoDevelopment/Assets
Import-Csv + PowerShell 唯一性/必填/数量/工作包校验
ConvertFrom-Json + PowerShell provenance 必需字段/安全默认值校验
PowerShell Markdown/链接/空白校验
git diff --check
```

首次在受限上下文创建清单目录被拒绝；经明确工作区写权限批准后重试成功。该失败未产生半成品文件。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| Manifest | PASS | 41 行；ID 唯一；必填/数量/Owner 有效；ART 27、AUDIO 9、FONT 2、LOC 3 |
| Provenance Template | PASS | JSON 可解析；必需字段存在；`planned`、`commercialUseReviewed=false` |
| 官方来源预检 | PASS | Noto CJK 官方 OFL 文件/仓库与 OpenAI 2026-01-01 Terms URL 可访问 |
| 文档/链接/空白 | PASS | 变更文档 H1、围栏、相对链接、尾随空白和 `git diff --check` |
| 编译 | NOT RUN | 纯计划文档，不修改可执行输入 |
| EditMode/PlayMode | NOT RUN | 同上 |
| 内容/Project Validation | NOT RUN | 未创建或导入资产/标签 |
| Build | NOT RUN | Release 输入不变 |
| GPU/音频/目标硬件 | NOT RUN | 没有正式二进制；必须在 G3 实测 |

## 6. 构建产物

- 配置：NOT RUN
- 路径：无
- 文件 Hash：无
- Build Manifest：无

## 7. 未执行项目

未生成视觉/音频、未下载字体、未写本地化正文、未导入 Addressables，也未运行 Unity/GPU/Player。
官方许可/条款仅用于计划时来源预检；最终权利结论必须以实际取得日版本、文件 Hash 和审核签字为准。

## 8. 已知限制和风险

- 41 批都尚无实际文件/provenance/Hash，QD-KI-003 继续阻断 Release。
- FirstParty 自动 provenance 扫描尚未实现，QD-KI-007 阻断正式标签。
- 字体未固定版本/Hash/Notice，QD-KI-008 阻断 Release。
- 纹理/音频/GPU 阈值是预算/警报，不能代替目标硬件 PASS。

## 9. 未完成项

- G1.1—G2.8 先完成通用运行时和可玩 Placeholder 垂直切片。
- G3.1—G3.3 按清单逐批生产/导入并真实验证。

## 10. 下一步前置条件

- G1.1 仅实现 G0.3 批准通用骨架，不导入正式资产。
- G3.1 开始前先扩 FirstParty Release provenance 自动门禁。

## 11. 结论

`COMPLETE`。G0.4 生产计划与权利流程可执行；不代表任何正式资产已完成或获 Release 批准。
