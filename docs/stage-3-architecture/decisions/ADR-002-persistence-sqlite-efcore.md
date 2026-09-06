# ADR-002: 持久化选型 SQLite + EF Core 8

## Status

Accepted (v2 — 应用用户调整)

## Context

PLErpTool 需持久化以下数据：

- **应收管理**：客户、业务员、月度欠款、收款记录（关系型，需树状检索、聚合、分页）
- **账套管理**：导入记录缓存（去重键、文件名去重）

### 驱动决策的因素

- 单机桌面工具，无网络/多用户/并发写入
- 应收管理需关系查询（按客户→业务员→月份分层）、聚合（差额 SUM）、分页——纯 Excel 实时读取性能与表达力不足
- DDD 仓储模式需可切换的持久化实现
- 团队熟悉 C#/.NET
- 部署需零配置，单文件

### v2 用户调整（2026-08-08）

用户对持久化方案提出 4 项调整：

1. **时间戳无时区**：所有表的 `CreatedAt`/`UpdatedAt` 用无时区 timestamp（本地 ISO8601，不带 `Z`/偏移）
2. **月份用 Date**：`MonthlyDebts.Month` 改为 Date 类型（存月首日 `yyyy-MM-01`）
3. **移除物理外键**：所有表不建 `FOREIGN KEY`，引用完整性由应用层/领域层保证
4. **导入记录不入库**：`ImportRecord` 改为内存临时缓存，不落 SQLite（详见 ADR-005 v2）

## Decision

采用 **SQLite** 作为嵌入式数据库，**EF Core 8** 作为 ORM。仅应收管理的 4 张表落库。

| 项 | 选定 |
|---|---|
| 数据库 | SQLite（`Microsoft.Data.Sqlite`） |
| ORM | EF Core 8（`Microsoft.EntityFrameworkCore.Sqlite`） |
| 数据库文件 | `plerp.db`（应用目录，单文件） |
| 落库表 | `Customers` / `Salesmen` / `MonthlyDebts` / `Payments`（4 张） |
| 不落库 | `ImportRecord` / `ImportFile` → 内存缓存（ADR-005 v2） |
| 迁移 | EF Core Migrations，启动时 `EnsureMigrated` |
| 仓储接口 | Domain 层 `IReceivableRepository` |
| 仓储实现 | Infrastructure 层 `ReceivableRepository` |
| 工作单元 | `UnitOfWork`（封装 `DbContext.SaveChanges`） |
| 测试 | SQLite in-memory（`:memory:`），同一 Provider |
| 时间戳 | `TEXT` ISO8601 本地时间 `yyyy-MM-dd HH:mm:ss`，**无时区**，EF 拦截器 `DateTime.Now` 填充 |
| 月份 | `MonthlyDebts.Month` → `TEXT` 存 `yyyy-MM-01`（月首日），值转换 `MonthKey ↔ DateTime` |
| 外键 | **不建物理外键**；引用完整性由应用层校验 + 领域不变量保证 |
| 查询追踪 | `NoTrackingWithIdentityResolution`，写操作显式 `Attach`/`Update` |

## Consequences

### Positive

- 零配置单文件部署，契合桌面工具
- EF Core LINQ 强类型查询，表达力强，与 DDD 仓储契合
- 迁移版本化，Schema 演进可控
- 测试用内存库，无外部依赖，CI 友好
- 未来切 PostgreSQL/SQL Server 只换 Provider + 迁移，领域/应用层不变

### Negative

- EF Core 追踪开销对单机工具略重（用 `AsNoTracking` 缓解）
- SQLite 并发写入弱（单用户桌面无影响，记录为已知限制）
- EF Core 迁移学习曲线（团队首次完整实践）

## Alternatives Considered

### Alternative 1: 纯 Excel（NPOI 实时读写）

- Pros：无数据库，主账即数据源；与 ExcelProject 一致
- Cons：关系查询/聚合/分页性能差；树状检索难实现；并发写冲突
- Why rejected：查询与数据完整性需求无法满足

### Alternative 2: SQLite + Dapper

- Pros：轻量，性能高
- Cons：手写 SQL，仓储样板多，无迁移工具，DDD 仓储实现底层
- Why rejected：开发效率与可维护性不如 EF Core

### Alternative 3: LocalDB (SQL Server Express)

- Pros：SQL Server 生态
- Cons：部署依赖重（需 LocalDB 运行时），CI 环境复杂
- Why rejected：单文件零配置目标不符

---

**Decided by:** CTO — Dr. Kenji Nakamura
**Date:** 2026-08-08
**Security review:** Pending CIO (Dr. Priya Mehta) — 本地数据存储，无网络传输
**Referenced in:** TSD §1, §2.1, Architecture-Overview §3, Deployment §3