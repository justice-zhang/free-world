# 内容定义验收清单

适用于角色、技能、被动、Trait、状态、敌人、Boss、Synergy、Evolution、地图、Encounter 和局外升级。

## 基本信息

- 内容类型：
- ContentId：
- 所属 ContentPack：
- 作者：
- 日期：

## 稳定 ID 与依赖

- [ ] ID 全小写并使用命名空间点号格式
- [ ] ID 未被使用或发布过
- [ ] 引用的内容均存在
- [ ] 内容包依赖完整且无循环
- [ ] 不依赖运行时数组下标

## 数据与模块

- [ ] 使用已有模块时未修改核心程序集
- [ ] 数值范围合法
- [ ] 概率位于允许范围
- [ ] 等级或阶段连续
- [ ] 触发链有 ProcDepth 或等价保护
- [ ] 不产生无法终止的递归或循环生成
- [ ] 不在高频路径引入反射、LINQ 或临时集合

## 内容类型专项

### 角色

- [ ] 基础属性完整
- [ ] 初始技能和 Trait 合法
- [ ] 解锁条件可达

### 技能

- [ ] Trigger、Targeting、Delivery、Effect 完整
- [ ] LevelPatch 路径有效
- [ ] 目标为空时行为明确
- [ ] 性能预算明确

### Synergy / Evolution

- [ ] 条件可达
- [ ] 不与其他规则形成循环
- [ ] 消耗和替换策略明确

### 地图 / Encounter

- [ ] Scene 地址有效
- [ ] Map Runtime 已注册
- [ ] Spawn Position 可采样
- [ ] Enemy Pool 和 Loot Table 有效
- [ ] Boss 和阶段时间合法

## 表现与本地化

- [ ] 名称、描述和 UI 文本使用本地化 Key
- [ ] `zh-CN` 和 `en` 条目存在
- [ ] 伪本地化无布局阻断
- [ ] VisualProfile 和 AudioProfile 存在或有合法 Placeholder
- [ ] 正式资产 provenance 完整

## 测试

- [ ] 内容验证通过
- [ ] EditMode 测试通过
- [ ] PlayMode 预览通过
- [ ] 固定种子结果符合预期
- [ ] 性能预览无异常分配

## 审核结论

- 结果：PASS / FAIL
- 审核人：
- 证据：
- 备注：
