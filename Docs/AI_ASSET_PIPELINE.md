# AI 美术资产生产流程

## 1. 目录

> Assets/GameAssets/AI/\<asset-id\>/  
> ├─ source/  
> ├─ working/  
> ├─ final/  
> ├─ prompt.txt  
> └─ provenance.json

## 2. 最低记录

- 资产 ID 和类型

- 工具/服务名称

- 模型版本

- 生成日期

- 提示词文件

- Seed（可取得时）

- 操作人员

- 输入参考列表

- 输入参考权利确认

- 人工修改列表

- 输出 SHA-256

- 商业使用复核状态

- 平台披露分类

## 3. 禁止

- 使用参考开源项目素材作为生成输入

- 使用无权使用的参考图片

- 生成明显近似知名角色、Logo、UI 或商标

- 将某个在世艺术家的姓名作为正式风格规范

- 在没有来源记录时进入 Release 标签

- 让 AI 输出的文字直接进入最终 UI 图标

## 4. 人工质量检查

- 轮廓和可读性

- 帧间一致性

- 角色比例和装备连续性

- 文字、符号、手指和边缘瑕疵

- 透明通道和像素边缘

- 动画锚点

- 色盲和亮度可读性

- 与现有商业作品的近似风险

## 5. 构建门禁

Build Preprocessor 必须阻止：

- development-only 或 placeholder 进入 Release

- 正式 AI 资源缺少 provenance

- provenance 中 commercialUseReviewed 不为 true

- 文件 Hash 与记录不一致

上线前按当时最新平台政策和法律要求复核。
