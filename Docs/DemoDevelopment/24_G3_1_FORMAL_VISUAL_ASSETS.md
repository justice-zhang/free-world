# 24 G3.1 正式视觉资产、Provenance 与 Addressables

- 状态：`IN PROGRESS — GOVERNANCE GATE`
- 日期：2026-08-09
- 输入：G2.8 垂直切片、G0.4 Manifest、M13、M15、ADR 0004/0011/0012/0026
- 非范围：G3.2 音频、G3.3 字体/正文、G3.4 平衡、G3.5 目标硬件性能、G3.6 Release

## 1. 目标

按 `Assets/G0_4_ASSET_MANIFEST.csv` 的 27 个 ART 行依次完成正式视觉生产。CSV 每一行是独立可审查
批次；每批只在文件、provenance、导入设置、Addressables 和视觉 QA 全部通过后提交并 Push 一次。

## 2. 前置治理门禁

- 正式记录统一使用 ADR 0026 的 Provenance Schema 2。
- Project Validation 同时扫描 AI、FirstParty 和实际 Release Addressables 输入。
- AI 目录固定为 `Assets/GameAssets/AI/QinglanDemo/<asset-id>/`；第一方固定为
  `Assets/GameAssets/FirstParty/QinglanDemo/<asset-id>/`。
- `source/`、`working/`、Prompt 与 provenance 不加入 Addressables；仅 `final/` 和正式 Profile 入组。
- 视觉文件固定进入 `QinglanDemo-Visual`，地址使用 `qinglan/<category>/<short-name>/<variant>`，
  标签固定为 `pack.qinglan_demo`、`release`、`visual.release`。
- 图片内不得含 UI 正文、Logo 字样、水印或第三方品牌；AI 只消费本仓库文字设计，不上传参考素材。

## 3. 批次顺序

严格按 Manifest 行号执行：ART-CHAR-001 → ART-CHAR-002 → ART-CHAR-003 → ART-ENEMY-001 →
ART-AFFIX-001 → ART-BOSS-001 → ART-BOSS-002 → ART-SKILL-001 → ART-SKILL-002 → ART-STATUS-001 →
ART-PICKUP-001 → ART-RELIC-001 → ART-MAP-001 → ART-MAP-002 → ART-OBJECTIVE-001 → ART-EVENT-001 →
ART-LANDMARK-001 → ART-HUB-001 → ART-META-001 → ART-META-002 → ART-COLLECT-001 → ART-STORY-001 →
ART-UI-001 → ART-UI-002 → ART-UI-003 → ART-UI-004 → ART-UI-005。

第一方 vector/procedural 批次由仓库内确定性脚本生成；AI-assisted 批次使用 ImageGen 且每个独立资产
单独调用。任何混合批次仍以一个 ART 行为提交边界。

## 4. 单批次完成定义

1. 数量、格式、尺寸和 Runtime 预算达到 Manifest 最低值。
2. Prompt/生成规格、源文件、工作文件（如有）、最终文件与 Schema 2 provenance 同批保存。
3. 无参考输入时 `referenceInputs=[]`；工具未提供 Seed 时记录明确的不可用原因，不伪造 Seed。
4. 源与最终 SHA-256 实际匹配；生成日条款 URL/日期和商业使用复核完整。
5. Alpha、边缘、锚点、帧间连续性、轮廓、灰阶、色觉/高对比和图片内文字检查通过。
6. Runtime Texture 通常不超过 2048，动画/UI Atlas 关闭 MipMap 并保留 4 px 边缘。
7. 只有 final/Profile 是明确文件级 Addressables，Group、地址和三个标签正确。
8. Focused EditMode、Project Validation 和适用 PlayMode 通过；随后该 ART 行单独提交、单独 Push。

## 5. 最终 G3.1 门禁

27 个 ART 批次全部通过后，运行全量 EditMode/PlayMode、Project Validation、Addressables/Pack 验证和
Windows x64 Development Build，并对正式内容下的 1080p 标准/高对比截图执行人工视觉复核。
GPU/1% Low、正式音频、字体、Release Manifest 和平台合规不得在 G3.1 宣称通过。
