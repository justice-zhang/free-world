# 本地化 Key 规范

## 范围

M8 使用 Unity Localization 的 `UI` String Table Collection，登记英文 `en`、简体中文
`zh-Hans` 和扩展伪本地化 `qps-ploc`。英文是 Project/Startup Locale，伪语言回退英文后再应用
扩展变换。

## 命名

- 页面与控件：`ui.<page>.<element>`，例如 `ui.main_menu.start`。
- 内容显示：`content.<namespace>.<kind>.<name|description>`；内容定义只保存 Key。
- 存档诊断：`save.<error|warning>.<reason>`。
- 平台诊断：`platform.<reason>`。

Key 必须为稳定、小写、点分隔的技术标识。不得把英文或中文正文当作 Key；不得把本地化正文
写进 baked 内容作为唯一真值。重命名 Key 属于兼容性变化，需同时迁移引用和语言表。

## 运行时边界

Application、Content 和 Presenter 只传 Key。`UnityLocalizationService` 是 Unity UI 适配器，
`RuntimeUiRoot` 才把 Key 解析为当前语言文字。设置文件只保存 Locale Code，不保存翻译文本。
Windows Placeholder UI 从系统 CJK 字体候选创建动态字体，不把系统字体复制进仓库。

## 验证与作者流程

`Tools/AzureSword/Setup M8 Save Localization Platform` 可重复创建三种 Locale 和表。它会收集所有
Placeholder baked Catalog 的 `localizedNameKey` / `localizedDescriptionKey`，为当前测试内容生成
双语占位条目。

正式 `Project Validation` 要求：

- active Localization Settings、英文、简中和 Pseudo Locale 均存在；
- `UI` Collection 的英文、简中表均存在；
- 固定 UI/存档/平台 Key 和所有 baked 内容 Key 在两张表中都有非空值。

新增用户可见文字时必须先增加语义 Key，并同时提供英文、简中；伪语言用于发现展开和布局问题。

M9 Content Creation Wizard 按 `content.<namespace>.<kind>.<name|description>` 自动登记英文和简中
Placeholder 条目。自动正文仅用于开发验证；内容人员必须在正式发布前完成翻译复核，不能把向导
占位正文当成正式文案。Validator、命令行和 Build Preprocessor 共用缺 Key/空值检查。
