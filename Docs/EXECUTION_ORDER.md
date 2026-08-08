# Codex 完整执行顺序

## 1. 操作原则

- 一次只执行一个里程碑。

- 每个里程碑使用独立分支或 PR。

- 先运行基线测试，再实现，再运行完整测试。

- 先给 Codex 总控提示词，再给当前里程碑提示词。

- 完成后运行里程碑审查提示词。

- 未通过验收，不进入下一里程碑。

- 不让 Codex 在同一个改动中同时实现框架、正式内容和美术。

## 2. 人工预检

1\. 安装并记录准确 Unity 6 LTS 版本。

2\. 用 URP 模板创建干净工程。

3\. 初始化 Git，提交未修改的空工程基线。

4\. 填写 Templates/PROJECT_VARIABLES.md。

5\. 把 Repository_Docs/ 复制到仓库。

6\. 确认命令行可调用 Unity，设置 UNITY_PATH。

7\. 建立保护分支，禁止直接在主分支开发。

8\. 将 Prompts/00_MASTER_CONTROL.md 作为 Codex 的长期规则。

## 3. 每个里程碑的固定循环

> A. 创建分支 milestone/mX-short-name  
> B. 运行当前主分支全部测试和验证  
> C. 向 Codex 提供总控提示词  
> D. 向 Codex 提供当前 Mx 提示词  
> E. Codex 输出计划并实现  
> F. Codex 运行测试、验证和构建  
> G. 提供 13_MILESTONE_REVIEW_GATE.md  
> H. 人工检查 Git diff、日志和场景  
> I. 通过后提交、合并、打标签 framework-mX  
> J. 更新执行日志和已知问题

### 3.1 双 Agent 推荐循环

默认采用完整里程碑轮流接力：

```text
Agent A：独立完成 Mx 预检 → 实现 → 审查 → PR → merge → tag → 分支清理
Agent A：在干净 main 上提交交接
Agent B：接管预检后独立完成 M(x+1) 的完整闭环
Agent B：在干净 main 上提交交接
Agent A：接管 M(x+2)
```

一般只有当前这一棒的 Agent 在工作。实现—审查双 Agent 同时参与或不同 worktree 并行只在
用户明确要求时启用。完整角色、Git 所有权、证据和冲突规则见
`Docs/AGENT_COLLABORATION.md`；交接消息使用 `Templates/AGENT_HANDOFF_TEMPLATE.md`。

## 4. 依赖顺序

> Preflight  
> -\> M0 工程治理  
> -\> M1 内容系统  
> -\> M2 模拟内核  
> -\> M3 战斗状态  
> -\> M4 技能运行时  
> -\> M5 敌人与地图  
> -\> M6 构筑与成长  
> -\> M7 表现、输入、UI  
> -\> M8 存档、本地化、平台边界  
> -\> M9 编辑器工具  
> -\> M10 性能、CI、冻结

M1 依赖 M0 的程序集和文档。M2 依赖 M1 的运行时定义。M3 依赖 M2 的 Store 和事件。M4 依赖 M3 的伤害与状态。M5 依赖 M2-M4。M6 依赖 M1-M5。M7 依赖稳定的模拟快照。M8 可部分并行，但建议在 Run 流程稳定后完成。M9 依赖全部内容 Schema。M10 必须最后执行。

## 5. 里程碑输出纪律

每次 Codex 必须报告：

1\. 修改文件列表。

2\. 实现的范围。

3\. 关键架构决定。

4\. 实际执行的命令。

5\. 测试、验证和构建结果。

6\. 性能数据（适用时）。

7\. 已知限制。

8\. 未完成事项。

9\. 下一里程碑前置条件。

没有实际日志时，不能写“测试通过”。

## 6. 建议 Git 标签

> framework-m0  
> framework-m1  
> framework-m2  
> framework-m3  
> framework-m4  
> framework-m5  
> framework-m6  
> framework-m7  
> framework-m8  
> framework-m9  
> framework-m10-rc1

实际执行统一使用 framework-m0 至 framework-m10。

## 7. 失败处理

- 编译失败：先恢复到最小可编译状态，再继续。

- 测试失败：要求 Codex定位根因，不允许跳过测试。

- 里程碑范围膨胀：撤销无关改动，拆成后续 Change Request。

- Unity CLI 不可用：记录环境问题，人工执行同一命令；不得伪造结果。

- 架构边界被破坏：运行架构审计提示词，修复后重新验收。

- 性能未达标：先保留正确性，建立基准和热点证据，再优化。

## 8. 框架冻结后

先执行三项扩展性测试，再进入正式内容生产：

- 新增第二角色

- 新增第二技能

- 新增第二地图

三项都不得修改 Core、Simulation 的既有接口或已有内容定义。若必须修改，说明框架抽象尚未完成。

# 各里程碑目标摘要

## M0

干净工程、程序集、Bootstrap、占位资产、验证和构建脚本。

## M1

稳定 ID、内容包、作者数据、运行时定义、烘焙、注册表和验证。

## M2

固定 Tick、实体句柄、数据 Store、空间网格和快照。

## M3

属性、修正、伤害、护盾、状态和死亡。

## M4

模块化技能运行时。

## M5

敌人、刷怪、遭遇和地图。

## M6

经验、升级、构筑、联动和进化。

## M7

表现、输入和 UI。

## M8

存档、本地化和平台边界。

## M9

内容制作工具和构建门禁。

## M10

性能、CI、构建与框架冻结。

## 9. 《剑起青岚》Demo 当前顺序

G0.1—G2.8 已按 `Docs/DemoDevelopment/02_DELIVERY_ROADMAP.md` 完成单工作包门禁。当前只进入 G3.1：
扩展全部 Release 输入的 provenance/Hash 验证，并按 G0.4 Manifest 导入正式角色、敌人、Boss、地图、
UI 与 VFX Profile。G3.1 未通过前不得开始 G3.2 音频、G3.3 字体/正文或 G3.4 平衡。
