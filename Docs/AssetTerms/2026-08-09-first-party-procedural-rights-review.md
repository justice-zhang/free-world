# FirstParty 程序化资产权利审查（2026-08-09）

- 审查日期：2026-08-09
- 权利依据：`repository://AGENTS.md`
- 适用范围：由本仓库内已提交脚本与规格从零确定性生成、且 provenance 指向本记录的
  `Assets/GameAssets/FirstParty/QinglanDemo/` 资产
- Owner：Qinglan Demo Visual Owner
- 审核人：Codex（技术、创意、权利）

## 审查结论

项目规则要求本框架从零创建，禁止导入或改造参考项目和来源不明的美术、角色、场景、音效、字体、
Prefab、动画、材质、Shader、Logo 或品牌资源。标记为 FirstParty procedural 的批次必须仅由仓库内
原创规格、数值、几何图元与确定性脚本生成，不包含第三方素材、样本包、商标、品牌、外部参考图片或
生成式模型输出。

满足下列条件的批次批准用于 Windows x64 / Steam 商业游戏运行时、商店与营销截图、内部开发和测试：

1. 保存生成规格、脚本版本 Hash、固定 Seed、源文件与最终文件 Hash；
2. 每个正式文件通过 Schema 2、实际 Release Addressables 路由和人工视觉复核；
3. 生成脚本与最终文件均不存在第三方输入或引用；
4. 后续任何文件、规格或脚本版本变化都会触发 Hash 失配并要求重新审核。

本记录不是对第三方软件许可证的替代；仓库工具链依赖仍由其既有开发环境许可约束，但生成资产本身是
项目原创 FirstParty 输出。若未来加入外部素材、字体、模型、插件或输入引用，本记录立即不再适用，
必须改走对应第三方/AI provenance 与许可证审查。
