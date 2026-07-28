# Windows 构建、性能与 CI 门禁

本项目锁定 Unity `6000.3.20f1`。以下命令均从仓库根目录运行；任何缺失的 XML、JSON、PASS
标记、Manifest 或 Player 输出都按 `FAIL` 处理，不能从历史里程碑结果推定通过。

## 本地完整门禁

```powershell
./Scripts/test.ps1 -Platform EditMode -ResultsDirectory TestResults/M10Final
./Scripts/test.ps1 -Platform PlayMode -ResultsDirectory TestResults/M10Final
./Scripts/validate.ps1 -LogPath TestResults/M10Final/validation.log
./Scripts/run-performance.ps1 -OutputPath TestResults/M10Final/performance.json -LogPath TestResults/M10Final/performance.log
./Scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopment/AzureSword.exe -LogPath TestResults/M10Final/build-development.log -EvidenceRoot TestResults/M10Final
./Scripts/build-windows-release.ps1 -OutputPath Builds/WindowsRelease/AzureSword.exe -LogPath TestResults/M10Final/build-release.log -EvidenceRoot TestResults/M10Final
./Scripts/run-player-smoke.ps1 -PlayerPath Builds/WindowsRelease/AzureSword.exe -OutputPath TestResults/M10Final/release-player.json -LogPath TestResults/M10Final/release-player.log
```

`run-performance.ps1` 默认推进 54,000 Tick；预热不计入分位数据。输出记录 Tick/渲染 CPU 的
average/p95/p99/max、分系统计时、托管/Native/GC 内存采样、GC 次数、热路径分配、实体峰值、
对象池命中/扩容/失败/丢弃、触发截断、无效句柄和确定性 Checksum。

## 构建配置

- Development：使用正常 Bootstrap Scene 和开发内容，允许程序化 Placeholder，生成
  `WindowsDevelopment` Manifest。
- Release verification：非 Development Player，使用临时生成的纯程序化 Smoke Scene；本次构建
  输入显式排除 placeholder/development-only Addressables Group，构建后恢复原设置。实际纳入的
  Scene/Addressables 仍执行 Release 门禁，Manifest 的 `placeholderCount` 必须为 0。
- Release verification 只证明冻结框架能生成并启动 Release Player，不代表已有正式可销售内容。
  正式内容仍必须通过 provenance、许可证、本地化、Addressables 和 Release 标签审核。

每份 `BuildManifest.json` 记录实际构建结果、配置、Git 来源状态、Unity、Schema、Pack 与 Catalog
Hash、Package/Addressables Hash、Placeholder/未批准资产计数、测试证据、UTC 和 EXE SHA-256。
Git “clean” 使用内容差异而非仅依赖文件 stat，避免 Unity YAML 换行时间戳噪声误报。

## 干净克隆验证

```powershell
./Scripts/verify-clean-clone.ps1 -SourceRepository . -EvidenceOutput TestResults/M10CleanCloneEvidence -KeepClone
```

脚本创建唯一的本地克隆，核对 Unity 精确版本，然后运行完整测试、验证、目标规模性能、两个
Build 和 Release Player Smoke。它复制结果与 Manifest 到指定证据目录，并保留克隆路径供审计。
不得使用当前工作区的 Library 或未跟踪输出补全干净克隆结果。

## GitHub Actions

`.github/workflows/windows-self-hosted.yml` 需要同时具有以下标签的 Runner：

```text
self-hosted, Windows, X64, unity
```

Runner 必须预装且已激活 Unity `6000.3.20f1`，并通过 `UNITY_PATH` 指向 `Unity.exe`。工作流不读取、
写入或上传 Unity License Secret。门禁顺序与本地一致，并上传 XML、日志、性能 JSON、Player Smoke
和两个 Build Manifest。工作流只有实际在 GitHub Runner 上执行成功后才可报告 `PASS`；仅提交 YAML
时状态是 `NOT RUN`。

## 失败处理

- 性能预算、确定性、目标数量或持续内存增长任一失败：保留 JSON/日志，停止发布。
- API Freeze 漂移：先按 `Docs/PUBLIC_API_FREEZE.md` 提交 ADR，不得直接刷新 Hash。
- Release 发现 Placeholder、未知来源或未登记第三方：停止构建，不得通过重命名或移除标签绕过。
- Unity Crash、无 XML/JSON、超时或 Runner 离线：结果为 `FAIL` 或 `NOT RUN`，根据是否实际启动区分。
