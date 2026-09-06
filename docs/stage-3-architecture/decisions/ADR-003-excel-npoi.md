# ADR-003: Excel 读写库选型 NPOI 2.8

## Status

Accepted

## Context

账套管理模块的核心能力是 Excel 读写：

- **导入**：读取源文件（如"总账单7月份账单.xlsx"），自动定位表头、映射列、去重
- **转换**：按客户+业务员分 Sheet，复制模板（含合并单元格），插入数据行，生成小计/合计/应收公式
- **导出**：导出失败数据为 `loser_*.xlsx`

ExcelProject (`Form1.cs`) 已使用 **NPOI 2.8.0** 实现全部上述能力，团队熟悉。

### 驱动决策的因素

- 必须支持：公式读写、合并单元格复制、模板 Sheet 复制、列宽设置
- 许可证需合规（内部/商用无障碍）
- 团队已有 NPOI 实战经验（ExcelProject 1500+ 行）
- 迁移成本最小化

## Decision

采用 **NPOI 2.8**（Apache 2.0 许可证）作为 Excel 读写库。

| 项 | 选定 |
|---|---|
| 库 | NPOI 2.8.0 (`NPOI` NuGet 包) |
| 许可证 | Apache 2.0 |
| 封装位置 | `Infrastructure/Excel/` |
| 服务 | `ExcelImportService` / `ExcelConvertService` / `ExcelExportService` / `TemplateSheetBuilder` |
| 接口隔离 | 领域层通过 `IImportRecordStore`/应用层 Command 调用，不直接依赖 NPOI |

## Consequences

### Positive

- 复用 ExcelProject 全部成熟逻辑（表头映射、去重、公式生成、备份、失败导出）
- Apache 2.0 许可证，商用无障碍
- 支持公式/合并单元格/模板复制，账套转换场景全覆盖
- 团队零学习成本

### Negative

- API 略冗长（相比 ClosedXML），但已被现有代码吸收
- 性能在大文件（>10 万行）时不如 EPPlus，但本场景数据量在万级以内

## Alternatives Considered

### Alternative 1: EPPlus 5.0+

- Pros：API 现代，性能好
- Cons：5.0+ 采用 Polyform Noncommercial 许可证，商用需购买商业许可证；许可证风险
- Why rejected：许可证合规风险

### Alternative 2: ClosedXML

- Pros：MIT 许可证，API 友好
- Cons：合并单元格复制、公式生成能力弱于 NPOI；账套模板的合并单元格/公式场景可能受限
- Why rejected：模板/公式能力不足

### Alternative 3: Open XML SDK

- Pros：微软官方
- Cons：API 极底层，样板代码多，无现成模板复制能力
- Why rejected：开发成本过高

---

**Decided by:** CTO — Dr. Kenji Nakamura
**Date:** 2026-08-08
**Security review:** Pending CIO (Dr. Priya Mehta) — 本地文件读写，无网络
**Referenced in:** TSD §1, §2.2, Architecture-Overview §3, §7