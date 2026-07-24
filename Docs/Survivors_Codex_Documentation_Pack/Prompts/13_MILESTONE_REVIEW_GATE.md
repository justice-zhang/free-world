# 里程碑审查门禁提示词

对当前里程碑执行一次严格的只读审查；发现问题后再进行最小修复。不要扩张功能范围。

## 审查步骤

1\. 读取当前里程碑提示词和验收标准。

2\. 检查 Git diff，列出所有新增、删除和修改文件。

3\. 标记任何超出当前里程碑范围的改动。

4\. 检查 asmdef 依赖和循环。

5\. 检查以下禁用模式：

- Simulation 引用 UnityEngine 对象

- GameObject.Find / FindObjectOfType

- Resources.Load

- 高频 LINQ、反射、临时集合

- 全局 Service Locator

- 高频 Instantiate/Destroy

- UI/View 直接写 Simulation Store

6\. 检查内容、存档、资产和本地化规则。

7\. 运行编译、EditMode、PlayMode、验证和适用构建。

8\. 将每条验收标准标记为 PASS、FAIL 或 NOT RUN。

9\. 对 FAIL 添加最小复现和根因。

10\. 只修复本里程碑范围内的失败，重新运行相关测试。

## 输出

- 里程碑结论：PASS / FAIL

- 验收矩阵

- 范围外改动

- 架构违规

- 实际命令与结果

- 修复文件

- 未解决问题

未执行的检查必须写 NOT RUN，不能按 PASS 处理。
