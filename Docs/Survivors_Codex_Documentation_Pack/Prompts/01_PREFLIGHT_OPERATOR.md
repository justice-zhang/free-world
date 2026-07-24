# 人工预检步骤：在调用 Codex 前执行

这不是让 Codex 自动完成的里程碑。负责人先完成环境和仓库准备。

## 步骤

1\. 安装团队选定的 Unity 6 LTS 版本。

2\. 使用 URP 模板创建空项目。

3\. 打开项目一次，等待所有包和 Shader 导入完成。

4\. 关闭编辑器，初始化 Git。

5\. 添加适用的 Unity .gitignore。

6\. 提交空工程基线，标签 pre-framework-baseline。

7\. 把 Repository_Docs/ 内容复制到仓库根目录。

8\. 填写 Templates/PROJECT_VARIABLES.md，并把确认后的变量同步到仓库文档。

9\. 设置环境变量 UNITY_PATH 指向 Unity Editor 可执行文件。

10\. 手工验证以下命令能启动批处理模式：

> & \$env:UNITY_PATH -batchmode -nographics -quit -projectPath . -logFile -

11\. 建立 main 保护规则和 milestone/\* 分支约定。

12\. 给 Codex 提供 00_MASTER_CONTROL.md，然后开始 M0。

## 预检完成标准

- 空工程可打开。

- Git 工作区干净。

- Unity CLI 可调用。

- 准确 Unity 版本已记录。

- 文档已进入仓库。

- 未导入任何参考项目资源。
