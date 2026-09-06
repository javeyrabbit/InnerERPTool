# Architecture Navigation Guide — PLErpTool (ERP 管理工具)

> **Document Owner:** CTO — Dr. Kenji Nakamura (小T)
> **Pipeline Stage:** 3 — Prototype → UML Engineering Package
> **Last Updated:** 2026-08-08
> **Status:** Draft — Pending User Approval

本目录是 PLErpTool 项目 **Stage 3 UML Engineering Package** 的完整产物。Stage 3 用户批准后，其中所有 ADR 与 TSD 将被**锁定**；任何后续技术栈变更需提交新 ADR 并完整重返 Stage 3。

---

## 阅读顺序

| # | 文件 | 内容 | 类型 |
|---|---|---|---|
| 1 | `Architecture-Overview.md` | 系统上下文、分层架构、组件图、工程结构、命名约定 | 架构总览 |
| 2 | `TSD.md` | 技术选型汇总表、对比分析、供应商评估 | 技术决策 |
| 3 | `Domain-Model.md` | 领域模型类图、聚合根、实体、值对象、领域事件 | UML 类图 |
| 4 | `Key-Flows.md` | 关键业务流程时序图（导入/转换/应收查询/收款登记） | UML 时序图 |
| 5 | `Deployment.md` | 部署拓扑、运行环境、备份策略 | UML 部署图 |
| 6 | `Business-Operations.md` | 业务操作梳理、主数据同步规则（客户/业务员无则新增） | 业务流程 |
| 7 | `Database-Schema.md` | SQLite 表结构 DDL、ER 图、EF Core 配置、索引策略 | 数据库 Schema |
| 8 | `Excel-File-Analysis.md` | 主账/总账单 Excel 物理结构分析、表头映射、公式模式 | Excel 结构 |
| 9 | `decisions/ADR-001-wpf-ddd-layering.md` | 采用 WPF + DDD 四层架构 | ADR |
| 10 | `decisions/ADR-002-persistence-sqlite-efcore.md` | 持久化选型 SQLite + EF Core | ADR |
| 11 | `decisions/ADR-003-excel-npoi.md` | Excel 读写选型 NPOI 2.8 | ADR |
| 12 | `decisions/ADR-004-ui-mvvm.md` | UI 层采用 MVVM + 紫罗兰主题 | ADR |
| 13 | `decisions/ADR-005-data-import-boundary.md` | 应收数据导入边界与一致性策略 | ADR |

---

## Stage 3 必交付物核对清单

| 交付物 | 状态 |
|---|---|
| UML 类图（领域模型） | ✅ `Domain-Model.md` |
| UML 时序图（关键流程） | ✅ `Key-Flows.md` |
| UML 组件图 | ✅ `Architecture-Overview.md` |
| UML 部署图 | ✅ `Deployment.md` |
| 业务操作梳理 | ✅ `Business-Operations.md` |
| 数据库 Schema | ✅ `Database-Schema.md` |
| Excel 结构分析 | ✅ `Excel-File-Analysis.md` |
| 架构决策记录 (ADRs) | ✅ `decisions/` 全部 `Accepted` |
| 技术选型文档 (TSD) | ✅ `TSD.md` |
| CIO 安全签收 | ⏳ 待 Dr. Priya Mehta 签收 (ADR-002/003 安全相关) |

> ⚠️ **Technology Decision Lock**: 本包经用户批准后，ADR 与 TSD 全部锁定。Stage 4+ 阶段发现实现使用了 TSD 外的技术，CTO 将其分类为 **P1 治理缺陷**，必须更换或补提 ADR。

---

## 关键技术决策摘要

| 领域 | 选定技术 | 关键 ADR |
|---|---|---|
| 应用平台 | WPF, .NET 8 (net8.0-windows) | ADR-001 |
| 架构模式 | DDD 四层 (Domain / Application / Infrastructure / Presentation) | ADR-001 |
| UI 模式 | MVVM (ViewModel 双向绑定, 紫罗兰主题) | ADR-004 |
| 持久化 | SQLite + EF Core 8 | ADR-002 |
| Excel 读写 | NPOI 2.8 (延续 ExcelProject) | ADR-003 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | ADR-001 |
| 日志 | Serilog (本地文件 + 控制台 sink) | TSD §1 |
| 单元测试 | xUnit + Moq | TSD §1 |
| MVVM 框架 | CommunityToolkit.Mvvm (ObservableObject/RelayCommand) | ADR-004 |