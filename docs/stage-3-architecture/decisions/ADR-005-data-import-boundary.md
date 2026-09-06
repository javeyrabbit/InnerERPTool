# ADR-005: 应收数据导入边界与一致性策略

## Status

Accepted (v2 — 应用用户调整：导入记录内存化)

## Context

用户确认应收管理模块数据来源为 **Excel 导入 + 本地库**：

- 账套管理的 `ImportRecord`（客户简称 + 业务员 + 交易日 + 品名 + 数量 + 单价 + 应收金额等）作为应收管理的原始数据来源
- 导入后数据落地 SQLite，后续查询/收款登记基于本地库
- Excel 仅作为导出格式（导出明细）

### 驱动决策的因素

- 纯 Excel 实时操作性能差（关系查询/聚合/分页）
- 需支持树状检索（客户→业务员→月份分层）
- 需支持收款登记（修改本地数据，不污染 Excel）
- DDD 要求领域模型自洽，不依赖外部文件状态
- 账套转换与应收管理数据需解耦

### v2 用户调整（2026-08-08）

用户要求导入数据**不入库**：`ImportRecord` 与 `ImportFile` 登记不落 SQLite，改为**内存临时缓存**，随会话生命周期管理，重置即清空。

## Decision

确立**应收数据导入边界与一致性策略**：

### 1. 导入边界

| 数据流 | 方向 | 机制 |
|---|---|---|
| Excel 源文件 → ImportRecord | 账套导入 | NPOI 读取（复用 ExcelProject），结果写入**内存缓存** `InMemoryImportRecordStore` |
| ImportRecord → 应收领域 | 跨上下文转换 | 应用层 AppService 编排：从内存缓存取记录，转换为 `MonthlyDebt`，经 `IReceivableRepository` 持久化至 SQLite |
| MonthlyDebt → Excel 导出 | 应收导出 | NPOI 写入（导出明细） |

### 2. 内存缓存策略（v2）

`ImportRecord` 不落库，由基础设施层 `InMemoryImportRecordStore` 管理：

| 项 | 说明 |
|---|---|
| 缓存载体 | `InMemoryImportRecordStore`（注入为 Singleton） |
| 数据结构 | `List<ImportRecord>` + `HashSet<string> UniqueKeys`（去重）+ `HashSet<string> FileNames`（文件名去重） |
| 生命周期 | 应用启动→应用关闭；重置按钮清空（复用 ExcelProject `ResetBtn_Click`） |
| 持久化 | 无；关闭程序即丢失（符合"临时缓存"定位） |
| 消费 | 账套转换/应收导入时从内存读取，转换后写入 `MonthlyDebts` 等落库表 |
| 接口隔离 | `IImportRecordStore`（应用层定义接口），实现于 Infrastructure，便于测试时替换 |

### 3. 一致性策略

| 场景 | 策略 |
|---|---|
| 同一客户+业务员+月份已存在 | 累加/更新欠款金额（而非覆盖），保留已登记收款 |
| 重复导入检测 | 内存 `HashSet<UniqueKey>` 去重（复用 ExcelProject `importedRecordKeys`） |
| 数量为 0 过滤 | 延续 ExcelProject `FilteredZeroQuantityCount` 逻辑 |
| 收款登记不影响导入源 | 收款存储于本地 `Payments` 表，Excel 源只读 |
| 导入失败 | 不写入本地库，记录日志，失败数据可导出 `loser_*.xlsx` |

### 4. 跨上下文映射

| ImportRecord 字段 | MonthlyDebt/Payment 字段 |
|---|---|
| `CustomerShortName` | `Customer.ShortName`（查找或创建） |
| `Salesman` | `Salesman.Name`（查找或创建） |
| `TradeDateValue` → `MonthKey` | `MonthlyDebt.Month` |
| `ReceivableAmount` / `Quantity × UnitPrice` | `MonthlyDebt.DebtAmount` |
| `PaymentAmount` + `PaymentDate` | `Payment`（类型=Collection，金额/日期） |

### 5. 事务边界

- 转入应收编排由应用层 AppService 在一个 `UnitOfWork` 内完成：从内存缓存取 ImportRecord → 转换 → 写入应收库
- 失败回滚整个事务，保证一致性
- 内存缓存不参与数据库事务（独立生命周期）

## Consequences

### Positive

- 应收查询/收款登记性能有保障（基于本地库）
- 账套转换与应收管理解耦，可独立演进
- 收款登记不污染 Excel 源文件
- DDD 领域模型自洽，无外部文件状态依赖
- 重复导入可控（内存去重键 + 累加策略）
- 导入数据不落库，避免缓存表污染主库 Schema（v2）

### Negative

- 导入数据关机即丢失——如需跨会话保留导入缓存，后续需补落库 ADR
- 首次导入需全量，后续增量导入需处理"已存在客户/业务员"匹配逻辑
- 跨上下文映射逻辑需测试覆盖（ImportRecord → MonthlyDebt）

## Alternatives Considered

### Alternative 1: 纯 Excel 实时操作（不落地库）

- Pros：无数据副本，Excel 即唯一真相
- Cons：查询/分页/树状检索性能差；收款登记需写回 Excel，并发/格式风险高
- Why rejected：性能与数据完整性不足

### Alternative 2: 导入记录落库（v1 方案）

- Pros：导入缓存跨会话保留；可查询导入历史
- Cons：缓存表污染主库；导入数据本质是临时态，落库语义不清
- Why rejected：用户明确要求导入数据临时存内存

### Alternative 3: 手动录入为主（Excel 仅导出）

- Pros：领域逻辑最纯粹
- Cons：用户需手动录入每条欠款，与既有 Excel 工作流冲突，效率低
- Why rejected：不符合用户实际工作流

---

**Decided by:** CTO — Dr. Kenji Nakamura (小T)
**Date:** 2026-08-08
**Referenced in:** TSD §1, Architecture-Overview §1, Domain-Model §1, Key-Flows §1/§3, Database-Schema §9