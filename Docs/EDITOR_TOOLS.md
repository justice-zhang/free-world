# M9 内容编辑器工具使用手册

## 推荐顺序

1. 打开 `Tools > Free World > M9 > Content Creation Wizard`，先创建 Pack，再创建被引用内容。
2. 为非 Pack 内容选择 Target Pack；Character/Enemy/Evolution/Encounter/Map 按界面补齐引用。
3. 点击 `Create and Validate`。向导会生成 canonical ID、类型目录、双语 Key、Addressables 标签、
   测试模板、来源占位并重新 Bake Pack。
4. 打开 `Validator Window`，修完所有带稳定错误码和资产路径的问题。
5. Encounter 用 `Wave Timeline Editor` 检查阶段与产出；Skill 用 `Skill Preview Harness` 检查等级、
   属性、目标数、范围、命中盒、DPS、触发数、分配和固定种子日志。
6. 用 `Content Pack Builder` 生成 Catalog 与审计报告。相同输入应产生相同两个 Hash。

向导支持 Pack、Character、Skill、Passive、Trait、Enemy、Status、Evolution、Synergy、Map 和
Encounter。生成物固定在 Placeholder 流程，不能直接作为正式 Release 内容。

## 自动生成内容

- ID：`<namespace>.<kind>.<technical_name>`；Pack 为 `<namespace>.pack.<technical_name>`。
- 目录：按 Characters、Skills、Passives、Traits、Enemies、Statuses、Evolutions、Synergies、Maps、
  Encounters 分类。
- 本地化：`content.<namespace>.<kind>.<name|description>`，同时写入英文和简中 Placeholder 文本。
- Addressables：Pack Label、`placeholder`、`development-only`，并设置稳定地址。
- 质量记录：每项 `.content-test.json` 和 Pack 根目录 `provenance.placeholder.json`。

如果 ID 已存在、必需引用为空或 Pack 不兼容，向导会停止且不提供忽略选项。创建正式内容时，应
先完成来源审批，并按内容流程手工或由后续获批工具迁移到非 Placeholder 目录和 Release 标签。

## Validator

窗口、CLI 和 Build Preprocessor 共用 `ProjectGovernanceValidator`，覆盖：

- canonical/重复 ID、定义引用、Pack 依赖/版本/循环、Schema 与 baked Hash；
- 等级连续性、概率、冷却、掉落、碰撞半径和各内容数值规则；
- 英文/简中 Key、Visual/Presentation Profile 地址、SpawnSecondarySkill 触发链循环；
- Placeholder/Release 标签、Third Party 登记、AI provenance 审批和输出 SHA-256。

命令行：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath <project> `
  -executeMethod Game.Editor.ProjectValidationCommand.Run `
  -logFile <validation.log>
```

Release 专用负向/预检入口为 `Game.Editor.M9ReleaseGateCommand.Run`。非 Development Player Build
会自动执行相同 Release 规则；没有“忽略全部错误”开关。

里程碑验收还可用 `Game.Editor.M9ReleaseBuildNegativeCommand.Run` 发起一次真实的非 Development
Windows Build；仅当 Build Preprocessor 因 `M9-RELEASE-PLACEHOLDER` 阻断时该负向 Harness 才 PASS。

## 预览含义

- Wave 的预算、间隔采样直接复用 Runtime Scheduler 的采样器；理论敌人数、并发、生命与经验是
  作者数据估算，不是目标规模性能结论。
- Skill Preview 运行真实 Headless Harness。分配值只描述该次固定 Tick 预览，不能代替 M10 的
  30 分钟 Soak 或 1,500/3,000/5,000 压力门禁。
- 每次对比都固定 Seed、等级、属性、目标数和持续时间；否则结果没有可比性。

## M9 覆盖 Fixture

`Tools > Free World > M9 > Configure Wizard Coverage Content` 可重复生成
`Assets/GameAssets/Placeholder/M9EditorTools`。它包含全部十种 Definition，其中第二角色、第二技能、
第二地图完全由数据扩展，没有新增核心程序集代码。该命令只用于开发验收。
