# ADR-001: 采用 WPF + DDD 四层架构

## Status

Accepted

## Context

PLErpTool 需整合两个既有资产：

1. **应收管理**模块（来自 HTML 原型 `docs/gemini-code-1786199581879.html`）——富 UI，含树状检索、表格、分页、弹窗、终端日志
2. **账套管理**模块（来自 ExcelProject WinForms 项目）——Excel 导入/转换/导出

现有 PLErpTool 为空壳 WPF 项目（`net8.0-windows`，仅空 `MainWindow`）。用户明确要求采用 **DDD 设计理念**。

### 驱动决策的因素

- HTML 原型的紫罗兰主题（表格 sticky 表头、分页、树、弹窗）需富 UI 框架
- ExcelProject 现有业务逻辑需剥离 WinForms UI，领域概念需清晰化
- DDD 要求领域层零基础设施依赖，便于测试与演进
- 单机桌面工具，无网络/多用户需求
- 团队熟悉 C#/.NET（ExcelProject 即 .NET 8）

## Decision

采用 **WPF + .NET 8** 作为 UI 平台，并实施 **DDD 四层架构**（四独立项目）：

| 层 | 项目 | 职责 | 依赖方向 |
|---|---|---|---|
| Presentation | `PLErpTool` | Views/ViewModels/主题 | → Application, → Infrastructure |
| Application | `PLErpTool.Application` | AppService/Command/Query/DTO/Validator | → Domain |
| Domain | `PLErpTool.Domain` | 聚合根/实体/值对象/领域事件/仓储接口 | 无外部依赖 |
| Infrastructure | `PLErpTool.Infrastructure` | EF Core 仓储实现/NPOI Excel 服务/Serilog/DbMigrator | → Domain, → Application |

依赖严格单向向内（Clean Architecture）。仓储接口定义在 Domain 层，实现在 Infrastructure 层（依赖倒置）。

## Consequences

### Positive

- 领域层零基础设施依赖，可独立单元测试，覆盖率易达标
- WPF 数据绑定 + MVVM 天然适配 DDD 应用层编排
- 既有 ExcelProject 逻辑可封装进 Infrastructure，接口隔离
- 未来持久化换库（SQLite → PostgreSQL）只动 Infrastructure，领域不变
- XAML 资源字典精准复刻紫罗兰主题

### Negative

- 四项目增加初始工程脚手架成本（约 +1 人日搭建）
- 桌面端无 ORM 读写分离需求，EF Core 追踪对单机工具略重（可 `AsNoTracking` 优化）
- 领域事件需自建轻量发布机制（无 MediatR，避免过度工程化）

## Alternatives Considered

### Alternative 1: WinForms（延续 ExcelProject）

- Pros：ExcelProject 已用，零迁移学习成本
- Cons：数据绑定弱，无法实现紫罗兰主题（表格 sticky/分页/树）；不符合富 UI 需求
- Why rejected：UI 能力不足

### Alternative 2: 单项目内分文件夹分层

- Pros：脚手架最少，上手快
- Cons：层间依赖无法用项目引用强制约束，易出现 Domain 误引用 EF/NPOI；DDD 纯度无法保证
- Why rejected：DDD 约束力不足，长期维护风险

### Alternative 3: MAUI

- Pros：未来可跨平台
- Cons：当前无跨平台需求；MAUI 桌面端成熟度不如 WPF；紫罗兰主题改造成本高
- Why rejected：过度工程化

---

**Decided by:** CTO — Dr. Kenji Nakamura
**Date:** 2026-08-08
**Referenced in:** TSD §1, Architecture-Overview §2, §4