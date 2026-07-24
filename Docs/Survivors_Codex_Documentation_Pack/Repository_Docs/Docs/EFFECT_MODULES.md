# 技能与效果模块规范

## 1. 设计目的

绝大多数新技能应通过已有模块组合完成。只有无法表达的新机制才增加 C# 模块。

## 2. 烘焙操作码

> public struct EffectOp  
> {  
> public EffectOpCode Code;  
> public float Value0;  
> public float Value1;  
> public float Value2;  
> public int Int0;  
> public int Int1;  
> public RuntimeContentIndex Reference0;  
> public RuntimeContentIndex Reference1;  
> public EffectOpFlags Flags;  
> }

作者数据在构建或内容烘焙时转换为 EffectOp\[\]。高频执行路径不反射 ScriptableObject 类型。

## 3. 注册表

- ITriggerExecutor

- ITargetingExecutor

- IDeliveryExecutor

- IEffectExecutor

- IConditionEvaluator

- IMapModule

注册表必须显式、可测试、无运行时程序集扫描。每种模块拥有稳定模块 ID。

## 4. 初始模块清单

### Trigger

- Timer

- OnHit

- OnKill

- OnDamageTaken

- OnPickup

- OnDistanceMoved

- OnStatusApplied

### Targeting

- Self

- Nearest

- Random

- Circle

- Cone

- Line

- Ring

- RandomPointAroundPlayer

### Delivery

- Instant

- Projectile

- Area

- Aura

- Orbit

### Effect

- Damage

- Heal

- ApplyStatus

- RemoveStatus

- Knockback

- Pull

- ModifyStat

- SpawnSecondarySkill

- GrantShield

- GainResource

## 5. 调用链保护

每个伤害包和技能触发上下文必须携带 ProcDepth、来源 ContentId 和调用链标记。达到上限后中止二次触发并写入诊断计数，避免无限递归。

## 6. 新模块准入

新增模块前必须回答：

1\. 现有模块组合为何不能表达？

2\. 新模块能否被至少两个未来内容复用？

3\. 是否改变模拟管线顺序？

4\. 是否影响存档格式或内容 Schema？

5\. 是否需要 Burst/Jobs 兼容？

6\. 是否有单元测试和性能测试？

新增模块需附 ADR 或 Change Request。
