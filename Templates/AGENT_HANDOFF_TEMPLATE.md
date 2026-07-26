# Agent 交接模板

## 1. 所有权

- 原 Owner：
- 新 Owner：
- 任务/里程碑：
- 所有权生效说明：从现在起，`<新 Agent>` 是 `<任务>` 的 Owner；`<原 Agent>` 停止写入该分支。

## 2. 仓库状态

- 规范仓库：`https://github.com/free-world-team/free-world.git`
- 当前分支：`main`
- 基线 tag / peeled SHA：
- HEAD SHA：
- origin/main SHA：
- PR：
- 工作树：`CLEAN`（正常交接必须为 CLEAN）
- Unity 版本：
- GitHub 权限：

## 3. 已完成范围

- （填写）

## 4. 未完成范围

- （填写）

## 5. 新增和修改文件

| 文件/目录 | 状态 | 说明 |
|---|---|---|
|  |  |  |

## 6. 实际证据

| 检查 | 结果 | 命令/路径 | Commit SHA |
|---|---|---|---|
| 编译 | PASS / FAIL / NOT RUN |  |  |
| EditMode | PASS / FAIL / NOT RUN |  |  |
| PlayMode | PASS / FAIL / NOT RUN |  |  |
| 内容验证 | PASS / FAIL / NOT RUN |  |  |
| Development Build | PASS / FAIL / NOT RUN |  |  |
| Release Build | PASS / FAIL / NOT RUN |  |  |
| 性能/Soak | PASS / FAIL / NOT RUN |  |  |

## 7. 已知问题与风险

| ID | 状态 | 影响 | 下一处理阶段 |
|---|---|---|---|
|  | OPEN / ACCEPTED / PLANNED / RESOLVED |  |  |

## 8. 正在运行或外部状态

- Unity/Test/Build 进程：无
- Git/GitHub 操作：无
- 等待中的审批或用户决定：无 / 说明

## 9. 下一步精确顺序

1. （填写）

## 10. 禁止提前执行

- （填写）

## 11. 接管确认

新 Owner 必须独立核验：

- [ ] origin fetch/push 指向规范组织仓库
- [ ] 已 `fetch --prune --tags`
- [ ] HEAD、origin/main 和基线/最终 tag 符合交接
- [ ] 工作树无来源不明改动
- [ ] Unity 版本一致
- [ ] GitHub 权限满足任务需要
- [ ] 报告、执行日志和已知问题已阅读
- [ ] 当前环境基线已实际运行，或明确记录 `NOT RUN`

结论：`READY` / `BLOCKED`

正常轮换只有在 `main` 干净、最终 tag 已推送、功能分支已删除且没有运行中操作时才能填写
`READY`。中途紧急交接必须填写 `BLOCKED`，并在第 4、5、8 节完整记录现场。
