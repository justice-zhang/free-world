# 01 架构与仓库目标结构

## 1. 设计原则

Demo 复用已冻结 M0—M10 框架，不建立第二套运行时。所有新内容先进入 Authoring/Pack，再经
Validate/Bake 转为纯运行时定义。表现资源由稳定 Profile ID 和 Addressables 间接解析。

```text
Qinglan Authoring + Scene + Localization + Profiles
  → Validate / Bake / Addressables Build
  → Runtime Content Registry
  → Application Run Coordinator
  → 30 Hz Simulation
  → Snapshot / Events
  → Presentation / UI / Audio
  → Save / Null Platform / future Steam adapter
```

## 2. 目标目录

以下为 G1—G3 的目标结构，不表示本任务已创建资产：

```text
Assets/
├─ Game/                                      # 冻结框架与经批准的通用扩展
│  ├─ Application/
│  ├─ Content/{Authoring,Runtime}/
│  ├─ Core/
│  ├─ Infrastructure/
│  ├─ Presentation/
│  ├─ Simulation/
│  ├─ UI/
│  └─ Editor/
├─ GameContent/QinglanDemo/
│  ├─ Authoring/
│  │  ├─ Packs/
│  │  ├─ Characters/
│  │  ├─ Skills/
│  │  ├─ Passives/
│  │  ├─ Traits/
│  │  ├─ Statuses/
│  │  ├─ Enemies/
│  │  ├─ Encounters/
│  │  ├─ Maps/
│  │  ├─ Synergies/
│  │  ├─ Evolutions/
│  │  └─ Meta/                              # 仅在 CR 获批后启用
│  ├─ Baked/
│  ├─ Scenes/QinglanOldCourt.unity
│  ├─ Profiles/{Visual,Audio,Vfx,Camera}/
│  ├─ Localization/{UI,Content,Narrative}/
│  ├─ Addressables/
│  └─ Tests/{Fixtures,Golden,Scenes}/
├─ GameAssets/
│  ├─ Placeholder/QinglanDemo/               # 程序化、development-only
│  ├─ AI/QinglanDemo/                        # 正式且有 provenance
│  └─ FirstParty/QinglanDemo/                # 自制非 AI 正式资产
└─ ThirdParty/                               # 需 notices 与许可证登记

Docs/DemoDevelopment/                        # 本文档集
Docs/ChangeRequests/                         # 获批前的通用能力申请
Docs/ADR/                                    # 架构/Schema/API/Save 变更决定
Docs/Reports/                                # 每个工作包的结果报告
```

不得在 `Assets/GameAssets/Placeholder` 中放正式资源，不得通过把 Placeholder 改名或改标签绕过
Release 门禁。

## 3. 内容包建议

| Pack | 责任 | Schema | 依赖 |
|---|---|---:|---|
| `com.freeworld.qinglan.demo.core` | 角色、武器、心诀、状态、候选、显化 | 5 或获批新版本 | 基础框架 Pack |
| `com.freeworld.qinglan.demo.region` | 地图、敌人、Encounter、Boss、事件 | 5 或获批新版本 | demo.core |
| `com.freeworld.qinglan.demo.meta` | 行脉、嵌片、收藏、故事、据点 | 待 CR | demo.core、demo.region |
| `com.freeworld.qinglan.demo.presentation` | Profile 与 Addressables 归属清单 | 不进入 Simulation Schema | 前三包的稳定表现 ID |

若现有工具要求一个 Authoring Pack，G1 可先以 `com.freeworld.qinglan.demo` 合包；拆包只在依赖和
构建门禁可验证后执行，不能为目录美观制造额外加载复杂度。

## 4. Assembly 所有权

| Assembly | Demo 可做 | Demo 禁止 |
|---|---|---|
| `Game.Core` | 仅经 ADR 接受的通用值类型/API | 角色、武器、地图 ID 分支 |
| `Game.Content.Runtime` | 经 CR/ADR 的通用 Schema 扩展 | Unity Object、表现资源 |
| `Game.Simulation` | 通用、可复用、显式注册模块 | 专用 Controller、逐敌 Update |
| `Game.Application` | 通用流程/存档协调扩展 | 候选、伤害、掉落真值 |
| `Game.Content.Authoring` | 新 Definition/Baker（若获批） | 运行时访问 AssetDatabase |
| `Game.Infrastructure` | 组合根、Scene/Addressables 适配 | 全局 Service Locator |
| `Game.Presentation` | Profile、池化 View、VFX/Audio 请求 | 写模拟 Store |
| `Game.UI` | UI-safe 投影、本地化与页面 | 复制候选或 Boss 规则 |
| `Game.Editor` | 向导、验证、预览和构建门禁 | 第二套运行时公式 |

## 5. Addressables 组和标签

| 组 | 标签 | Development | Release |
|---|---|---:|---:|
| `QinglanDemo-Placeholder` | `pack.qinglan_demo`, `placeholder`, `development-only` | 允许 | 阻断 |
| `QinglanDemo-Content` | `pack.qinglan_demo`, `release`, `content.release` | 允许 | 必须 |
| `QinglanDemo-Visual` | `pack.qinglan_demo`, `release`, `visual.release` | 允许 | 必须 |
| `QinglanDemo-Audio` | `pack.qinglan_demo`, `release`, `audio.release` | 允许 | 必须 |
| `QinglanDemo-Localization` | `pack.qinglan_demo`, `release`, `localization.release` | 允许 | 必须 |

每个 Addressables 地址只由内容或 Profile 间接引用；禁止 `Resources.Load`、重复地址和无 Owner 句柄。

## 6. Scene 结构

`QinglanOldCourt.unity` 只承载静态表现、预设锚点、碰撞代理和显式配置引用。刷怪时间、Boss 阶段、
风脉台真值和奖励不得写在散落的 Scene MonoBehaviour 中。

```text
QinglanOldCourt
├─ SceneCompositionRoot
├─ StaticEnvironment
├─ MapAnchors
│  ├─ Altars/{ListenWind,GuideWind,StopBalance}
│  ├─ Regions/{TrainingGround,HerbGarden,SwordGallery,OldGate,GuestCourt}
│  ├─ Boss/{Mid,Final}
│  └─ Events/{WindRiot,GardenRevival,SwordResonance}
├─ PresentationBounds
├─ NavigationProxy                            # 烘焙为数值边界/障碍
└─ LightingAndAudioZones                      # 仅表现
```

## 7. 配置与环境

- 精确 Unity 版本只读 `ProjectVersion.txt`；
- 目标平台首发为 Windows x64，Steam 后端不进入 G1/G2；
- 正式内容运行必须支持 `NullPlatformFacade`；
- 角色/地图/语言选择均通过显式构造和 UI-safe ViewModel，不做场景查找；
- 每个正式资源必须有唯一来源记录、SHA-256、Owner 和审核状态。
