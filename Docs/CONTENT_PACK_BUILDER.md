# Content Pack Builder

## 用途

M9 Builder 把一个 `ContentPackAuthoring` 通过 canonical Baker 写为可审计 Catalog 和构建报告。
它不修改作者数据，也不把成功打包等同于允许 Release。

## 编辑器与命令行

编辑器菜单：`Tools > Free World > M9 > Content Pack Builder`。选择 Pack 和输出根目录后执行
`Build Catalog and Report`。

命令行会按作者资产路径稳定排序并构建全部 Pack：

```powershell
$env:CONTENT_PACK_OUTPUT = '<output-root>'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath <project> `
  -executeMethod Game.Editor.ContentPackBuildCommand.Run `
  -logFile <pack-build.log>
```

默认输出根目录为 `Builds/ContentPacks`，每个 Pack 写入：

```text
<root>/<pack-id>/<version>/catalog.json
<root>/<pack-id>/<version>/pack-build-report.json
```

## 报告与确定性

报告列出 Pack ID、版本、Content Schema、游戏版本范围、Official 标记、依赖版本范围、Catalog
Address、全部资源标签、Definition 数量、Content Hash、Catalog Hash 和 Catalog 文件名。

- Content Hash：现有 Baker 对 canonical 运行时内容计算的稳定 Hash。
- Catalog Hash：实际输出 `catalog.json` 文件的 SHA-256。

相同项目版本、相同作者输入和相同序列化实现必须得到相同两个 Hash。任何输入、依赖、排序或
Wire 输出变化都应通过代码评审解释；不要手工编辑输出文件。

## 发布边界

Builder 会在报告中保留 `placeholder` / `development-only` 标签。正式 Release 仍必须通过完整
Project Validation、provenance Hash、Third Party 登记和 Release Build Preprocessor；Builder 没有
绕过门禁的参数。
