# 09 G0.4 正式资产、音频、字体与本地化生产计划

- 状态：`APPROVED FOR PRODUCTION`
- 日期：2026-08-04
- 适用工作包：G2.7、G3.1—G3.6
- 机器清单：[Assets/G0_4_ASSET_MANIFEST.csv](Assets/G0_4_ASSET_MANIFEST.csv)
- Provenance 模板：[Assets/G0_4_PROVENANCE_TEMPLATE.json](Assets/G0_4_PROVENANCE_TEMPLATE.json)
- 架构依据：ADR 0004、0011、0012；M13、M15、M16
- 实现状态：只完成生产/权利/预算计划，尚未导入正式资产

## 1. 范围与非范围

本计划覆盖 Demo 必需的角色、敌人、Boss、地图、目标、事件、地标、技能/VFX、UI、叙事、音频、
字体、简中/英文和伪本地化。G0.4 不生成/导入二进制资产，不修改 Package、Addressables、Scene、
Localization Table 或 Release Hash；这些操作按 G3.1、G3.2、G3.3 分开提交和 Push。

不制作完整版其他角色/境域，不导入参考开源项目、旧项目或来源不明资产，不使用知名角色/品牌/
在世艺术家姓名作风格提示，不把 AI 图中生成的文字直接用作 Logo 或 UI 文本。

## 2. 视觉圣经

### 2.1 画面语言

- 2D/轻斜俯视，轮廓优先；角色与敌人至少在灰阶轮廓上可分。
- 主题为“清朗东方幻想的旧庭复苏”：青绿、暖石、米白，少量朱红；拒绝阴沉尸骸和末日废土。
- 中央练剑场简洁，地图边缘、地标和五区材质细节逐步增加，但 Walkable/危险边界始终清楚。
- 角色/友方技能使用青白、金白和冷青；普通敌人偏木褐/石灰/草绿；敌方危险使用暖红/橙红。
- UI 不依赖图片内文字；所有标题、按钮和数字由 Localization/TMP 渲染。

### 2.2 调色板与可访问性

| 角色 | 主色 | 辅色/轮廓 | 约束 |
|---|---|---|---|
| 玩家/乘风 | `#59C7C1` | `#F4EFD8` / `#163D45` | 不与危险红共用发光核心 |
| 友方显化 | `#A7E6DD` | `#F2D27B` | 高亮保留边缘而非全屏泛白 |
| 普通敌人 | `#607A52` | `#3D332B` | Affix 仍需形状/纹理覆盖 |
| P0 危险 | `#E45D45` | `#FFD0A3` / 深边线 | 色觉模式增加条纹、方向和音频 |
| 交互/奖励 | `#D7B85C` | `#FFF2B8` | 不冒充敌方预警 |
| 环境 | `#789F85` | `#B69B78` / `#E8E1CC` | 饱和度低于角色与危险 |

任一关键信息至少同时使用颜色以外的形状、方向、纹理或声音。低闪模式将发光/闪屏强度降至 30%，
但不得缩短 P0 前摇、移除边界或静音唯一危险提示。

### 2.3 动画最低集合

- 陆青野：4 方向 Idle/Move/Hit/Down/Victory/ImperialSword，外加四档乘风 Overlay。
- 六普通敌人：4 方向 Move/AttackWindup/Hit/Death；差异由体型、节奏和攻击轮廓共同表达。
- 折枝/听风：移动、受击、死亡/斩结、每阶段转换、每个独立高危技能前摇。
- 地标/风脉台：Idle/Available/Active/Complete 或 Undiscovered/Discovered/Claimed。

若 AI 输出帧间一致性不足，优先改为少量关键姿势＋Unity 2D 骨骼/程序化补间；禁止把瑕疵帧通过
缩小、强模糊或高闪 VFX 隐藏。

## 3. 生产清单与预算

CSV 每行是一个可审查交付批次，不是占位符。最低范围共 41 个批次，包括 27 个视觉批次、9 个音频
批次、2 个字体批次和 3 个本地化批次。数量为最低值，增加范围必须说明内存、包体、工期和权利影响。

### 3.1 纹理与 GPU

| 项目 | 预算/警报 |
|---|---|
| 单张 Runtime Texture | 通常 ≤2048；Key Art 源可 4096，导入后按用途降采样 |
| 角色/敌人 Atlas | 单 Atlas ≤2048，MipMap 关闭，Sprite 边缘留 4 px |
| UI Atlas | ≤2048，透明边缘/九宫格验证；图片内无正文 |
| 同屏 VFX | 沿用 200 活动池；P0 保留，先降 P3/P2 |
| 透明 Overdraw | 1080p 平均警报 3×，P0 局部峰值单独说明 |
| Resident Texture | 推荐目标 ≤384 MiB；UI ≤64 MiB、VFX ≤96 MiB |
| Draw Calls | 1080p 高压场景警报 500；必须附 Batches/SetPass/Frame Debugger |

Windows Runtime 首选 BC7/BC3；线稿/小图标在实际对比无明显劣化时可用 BC7 或合并 Atlas。任何压缩选择
都在 G3.5 以目标硬件和截图判定，不为减包体破坏 P0 轮廓。

### 3.2 音频

源文件统一 48 kHz/24-bit WAV；短 SFX 导入后 PCM/ADPCM 或 Vorbis，音乐/长环境使用 Vorbis Streaming。
音乐明亮、流动，通过打击乐/弦乐 Stem 增压，不变成恐怖音景。

| 项目 | 预算/规则 |
|---|---|
| 总 AudioSource 活动 | 32；预留 8 个 P0/机制通道 |
| 同一普通 SFX | 最多 4，冷却 40—120 ms，按 Profile 配置 |
| UI | 最多 4；错误/确认优先于 Hover |
| Music/Boss Stem | 最多 8，DSP 同步循环，Pause/Story 用 Mixer Snapshot |
| Resident 短 SFX | ≤64 MiB；长音乐/环境必须 Streaming |
| Duck | P0 危险对普通命中 -6 dB；对白/故事对环境 -4 dB |

音频以第一方程序化合成/原创编曲生产，保存生成脚本、参数、Seed、源 WAV、编辑清单和输出 Hash。
不得采样未授权唱片、影视、游戏、样本包或人声。

### 3.3 字体

候选为 Noto Sans CJK SC Regular/Bold 与 Noto Serif CJK SC SemiBold，来源只允许官方
`notofonts/noto-cjk` 发布/文件，许可证候选为 SIL OFL 1.1。G3.3 导入前必须：

1. 固定 Git tag/release、下载 URL、原始文件 SHA-256 和下载日期；
2. 保存对应 `LICENSE`，登记 `THIRD_PARTY_NOTICES.md`，确认 Reserved Font Name 约束；
3. 生成包含全部实际简中、ASCII、标点和 fallback 的 TMP Font Asset，执行缺字扫描；
4. 原字体与派生字体均按 OFL 要求随许可分发；法务/Release Owner 签字后才标记批准。

官方候选来源：

- `https://github.com/notofonts/noto-cjk`
- `https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE`

若下载时官方许可、文件或版本与本计划不一致，立即阻断并重新评审，不能用系统字体替代 Release 字体。

## 4. 目录、命名与 Addressables

```text
Assets/GameAssets/AI/QinglanDemo/<asset-id>/{source,working,final,prompt.txt,provenance.json}
Assets/GameAssets/FirstParty/QinglanDemo/<asset-id>/{source,final,provenance.json}
Assets/ThirdParty/Fonts/NotoCJKSC/{font,LICENSE,source-record.json}
Assets/GameContent/QinglanDemo/Profiles/{Visual,Audio,Vfx,Camera}
Assets/GameContent/QinglanDemo/Localization/{UI,Content,Narrative}
```

Runtime 地址格式：`qinglan/<category>/<short-name>/<variant>`，全小写、斜杠分段。AI/FirstParty 的
source/working 文件不加入 Addressables；只有 final 和 Profile 入组。组与标签固定为：

| Group | 必需标签 |
|---|---|
| `QinglanDemo-Visual` | `pack.qinglan_demo`、`release`、`visual.release` |
| `QinglanDemo-Audio` | `pack.qinglan_demo`、`release`、`audio.release` |
| `QinglanDemo-Localization` | `pack.qinglan_demo`、`release`、`localization.release` |
| `ThirdParty-Fonts` | `pack.qinglan_demo`、`release`、`localization.release` |

正式 Group 不得含 `placeholder`/`development-only`；Profile/ContentId 是唯一间接引用入口。每个 Scene、
Run、Boss Phase 的句柄 Owner 和释放时机必须可追踪。

单二进制文件建议 <25 MiB，单资产工作包建议 <50 MiB。若超过，先提交 Git LFS/仓库容量评审；G0.4
不静默启用 LFS，也不把源文件压缩成不可审计归档来规避限制。

## 5. 来源与权利流水线

### 5.1 状态机

```text
planned → generated/acquired → technical_qa → visual_or_audio_qa
→ rights_reviewed → approved-for-release
```

任一步失败回到 generated/acquired；文件变化使后续审核全部失效。只有最后状态允许 Release 标签。

### 5.2 每个正式文件的最低记录

- assetId、relativePath、Owner、sourceCategory；
- 工具/服务、模型/版本或生成脚本 Git SHA、日期、操作者、Seed；
- 完整 prompt 文件、输入引用列表和逐项权利确认；无输入时必须显式 `[]`；
- 人工修改、源文件 SHA-256、最终文件 SHA-256；
- license/terms URL 与下载/生成日快照、允许平台/用途、AI 披露分类；
- technical/creative/rights reviewer 与时间；`commercialUseReviewed=true`；
- 状态必须为 `approved-for-release`。

AI 图只使用本仓库设计文档的文字方向，不上传参考项目素材或无权图片。OpenAI 服务当前条款可作为
生成日权利审查输入之一，但输出可能不唯一，项目仍负责相似性、商标、肖像和第三方权利复核：
`https://openai.com/policies/terms-of-use/`。每批生成必须保存实际生成日条款 URL/日期，G3.1 再复核。

第一方程序化资产也必须登记脚本、参数、Seed、人工修改和 Hash；“我们生成的”不能替代记录。
第三方字体必须同时登记 `THIRD_PARTY_NOTICES.md`、许可文件和 provenance。缺记录、Hash 不符、未复核、
条款不明或来源输入权利不明一律 FAIL。

G3.1 已将 `AssetProvenanceValidator` 主动扫描扩展到 `Assets/GameAssets/AI`、
`Assets/GameAssets/FirstParty` 和所有实际 `release` Addressables 输入；Schema 2 强制 Owner、生成信息、
源/输出 Hash、条款 URL/日期、允许用途和三类审核人。G3.3 再增加第三方许可文件/版本/Hash
一致性专用检查。只在文档中登记而不通过验证器不能进入 Release。

## 6. 本地化与叙事生产

- 简中为创作源，英文由第二轮语义翻译/复核；Pseudo 由正式英文自动生成，只作布局测试。
- 最低清单：UI/诊断 180 Key、内容名称/描述/等级变化 296 Key、叙事/目标/Boss 120 Key；实际 Catalog
  或页面产生更多 Key 时清单自动上调，不能删文案来满足数量。
- 所有 Key 使用 `LOCALIZATION_KEYS.md` 规范；正文不进入 ContentId、图片、音频文件名或模拟事件。
- 术语表至少锁定人名、境域、六武器、六心诀、六显化、敌人/Boss、风脉台、资源和设施。
- G3.3 覆盖 zh-Hans/en/Pseudo、100/125/150% UI Scale、1920×1080、长文本、换行、缺字、字幕与
  手柄焦点。机翻或向导占位正文不得进入 Release。

## 7. 分包执行顺序

1. G2.7 只完成程序化 Placeholder Profile、池和 P0 可读性，不把占位标成正式。
2. G3.1 按 CSV 的 ART 行生成/导入、逐批 provenance、Addressables 和视觉 QA；每个独立批次提交 Push。
3. G3.2 按 AUDIO 行生成/导入、混音/并发/循环/危险提示验证；逐批提交 Push。
4. G3.3 固定字体版本/许可并完成正式双语、Pseudo、裁切/缺字；字体和本地化分别提交 Push。
5. G3.5 在目标硬件测 GPU/CPU/内存/包体/池；必要降级只改表现，不改模拟真值。
6. G3.6 运行 Release Validator、Manifest、离线 Player、合规签字；任何缺失阻断发布。

## 8. G0.4 验证清单

- [x] 每个 Demo 视觉、音频、字体、本地化范围都有交付批次、数量、格式、预算和工作包。
- [x] AI、第一方、第三方分别有目录、来源字段、Hash、审核和回滚流程。
- [x] Addressables Group/Label、Profile 间接引用和 Release 阻断规则明确。
- [x] 字体只锁候选和官方来源；没有在未下载/未 Hash 时伪称许可证已最终批准。
- [x] M10 Null Device 数据没有被当作正式 GPU/音频证据。
- [x] G0.4 不导入资产、不改变 Release 输入、不提前实施 G3。

因此 G0.4 允许进入 G1.1；所有实际资产仍需在对应 G3 工作包取得真实文件、Hash、许可证和验证证据。
