# Key Flows — PLErpTool (ERP 管理工具)

**Document Owner:** CTO — Dr. Kenji Nakamura (小T)
**Pipeline Stage:** 3 — UML Engineering Package
**Date:** 2026-08-08
**Status:** Draft — Pending User Approval

本文件以 UML 时序图描述系统关键业务流程，至少覆盖每个核心用例一个 happy path。

---

## 流程 1：账套导入源文件

**触发**：用户在账套管理界面点击"导入文件"按钮，多选 Excel 文件。
**来源**：ExcelProject `ImportBtn_Click`。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as AccountView
    participant VM as AccountViewModel
    participant App as AccountAppService
    participant Exc as ExcelImportService
    participant Store as InMemoryImportRecordStore
    participant Log as Serilog

    U->>V: 点击"导入文件"
    V->>VM: ImportFilesCommand
    VM->>App: ImportSourceFilesCommand(filePaths)
    App->>Exc: ImportExcelFile(path)

    loop 每个文件
        Exc->>Exc: FindHeaderRowIndex(sheet)
        Exc->>Exc: BuildHeaderMap(row)
        loop 每行数据
            Exc->>Exc: TryCreateRecord(row, headerMap)
            alt 数量为 0
                Exc->>Exc: 计入 FilteredZeroQuantityCount
            else 空行/汇总行
                Exc->>Exc: 跳过
            else 有效记录
                Exc->>Store: HasDuplicate(record)
                Store-->>Exc: true/false
                alt 重复
                    Exc->>Exc: 计入 DuplicateRecordCount
                else 新增
                    Exc->>Store: AddImportRecords([record])
                end
            end
        end
        Exc-->>App: ImportFileResult(提取/新增/重复/过滤)
    end

    App->>Log: 记录导入统计
    App-->>VM: ImportResultDto
    VM->>VM: 刷新预览表格
    VM-->>V: 绑定更新
    V-->>U: 显示导入结果 + 刷新预览
```

---

## 流程 2：账套转换写入主账（含主数据同步）

**触发**：用户点击"转换"按钮，将导入数据写入主账 Excel，并同步客户/业务员主数据。
**来源**：ExcelProject `CoverBtn_Click` / `AppendResultToMasterWorkbook` + 主数据同步（详见 `Business-Operations.md` §3）。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as AccountView
    participant VM as AccountViewModel
    participant App as AccountAppService
    participant Store as InMemoryImportRecordStore
    participant Repo as IReceivableRepository
    participant UoW as UnitOfWork
    participant Exc as ExcelConvertService
    participant MW as MasterWorkbook(聚合根)
    participant Log as Serilog

    U->>V: 点击"转换"
    V->>VM: ConvertCommand
    VM->>App: ConvertToMasterCommand(masterPath, records)
    App->>App: 校验 (records 非空 + masterPath 非空)
    App->>Store: GetImportRecords()
    Store-->>App: List~ImportRecord~

    rect rgb(235, 248, 255)
        Note over App,Repo: 步骤A：主数据同步（无则新增，有则不新增）
        App->>UoW: BeginTransaction
        loop 按 (CustomerShortName, Salesman) 分组
            App->>Repo: FindCustomer(ShortName)
            alt 客户不存在
                App->>Repo: AddCustomer(ShortName, FullName=ShortName)
                Repo-->>App: newCustomerId
            else 客户已存在
                Repo-->>App: existingCustomerId (不更新)
            end
            opt Salesman 非空
                App->>Repo: FindSalesman(customerId, salesmanName)
                alt 业务员不存在
                    App->>Repo: AddSalesman(customerId, name)
                    Repo-->>App: newSalesmanId
                else 业务员已存在
                    Repo-->>App: existingSalesmanId (不更新)
                end
            end
        end
        App->>UoW: SaveChanges (主数据提交)
    end

    App->>Exc: Convert(path, records, customerSalesmanMap)

    Exc->>Exc: LoadWorkbook(path)
    Exc->>MW: 构造 MasterWorkbook

    loop 按客户+业务员分组
        MW->>MW: FindOrCreateSheet(customer, salesman)
        alt Sheet 不存在
            MW->>Exc: CreateSheetFromTemplate(templateSheet)
            Exc->>MW: 返回 SheetSection
            MW->>MW: EnsureSummaryRows(sheet, template)
        end
        MW->>Exc: LoadExistingSheetKeys(sheet)

        loop 按月分组
            MW->>MW: FindMonthSection(monthKey)
            alt 月份分区不存在
                MW->>MW: CreateMonthSection(monthKey)
            end
            MW->>MW: InsertRecordsIntoSection(records)
            MW->>Exc: ShiftRows / 创建行
            MW->>Exc: WriteRecordToRow(record)
            MW->>MW: UpdateSubtotal(SUM 公式)
        end

        alt 有写入
            MW->>MW: UpdateSheetSummary(合计/应收公式)
        end
    end

    Exc->>Exc: EvaluateAllFormulas()
    Exc->>Exc: 备份原文件 → Backup/
    Exc->>Exc: 写回原文件
    Exc->>Log: 记录转换结果 + 备份路径 + 主数据同步统计
    Exc-->>App: ConvertResultDto

    App-->>VM: 结果
    VM->>VM: 追加终端日志
    VM-->>V: 更新 TerminalControl
    V-->>U: 显示转换完成 + 备份路径 + 同步统计
```

---

## 流程 3：应收管理 — 树状检索与数据加载

**触发**：用户在左侧客户树点击节点（全部/公司/业务员）。
**来源**：HTML 原型 `loadAllData` / `loadCompanyData` / `loadData`。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as ReceivableView
    participant TreeVM as CustomerTreeViewModel
    participant App as ReceivableAppService
    participant Repo as IReceivableRepository
    participant VM as ReceivableViewModel

    U->>V: 点击树节点(全部/公司/业务员)
    V->>TreeVM: OnNodeSelected(node)
    alt 全部
        TreeVM->>App: LoadAllDebts()
        App->>Repo: GetAllDebts()
    else 按公司
        TreeVM->>App: LoadDebtsByCustomer(companyName)
        App->>Repo: GetDebtsByCustomer(id)
    else 按业务员
        TreeVM->>App: LoadDebtsByCustomerAndSalesman(company, salesman)
        App->>Repo: GetDebtsByCustomerAndSalesman(id, id)
    end
    Repo-->>App: List~MonthlyDebt~
    App-->>TreeVM: List~MonthlyDebtDto~
    TreeVM->>VM: 切换数据源
    VM->>VM: 重置查询条件
    VM->>VM: 计算分页
    VM-->>V: 绑定表格 + 分页
    V-->>U: 显示数据
```

---

## 流程 4：应收管理 — 综合查询（含"当前差额为负"）

**触发**：用户在工具栏输入条件并点"查询"。
**来源**：HTML 原型 `searchData()`。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as ReceivableView
    participant VM as ReceivableViewModel
    participant App as ReceivableAppService
    participant Repo as IReceivableRepository

    U->>V: 输入客户/业务员/账期 + 勾选"当前差额为负"
    U->>V: 点击"查询"
    V->>VM: SearchCommand
    VM->>App: QueryReceivablesQuery(criteria)
    App->>App: FluentValidation 校验
    App->>Repo: Query(QueryCriteria)
    Note over Repo: 按客户名/业务员模糊<br/>+ 账期范围过滤<br/>+ NegativeBalanceOnly 过滤<br/>+ 分页
    Repo-->>App: PagedResult~MonthlyDebt~
    App-->>VM: PagedResult~MonthlyDebtDto~
    VM->>VM: 更新表格 + 分页 + 总条数
    VM-->>V: 绑定刷新
    V-->>U: 显示过滤结果
```

---

## 流程 5：应收管理 — 登记收款（弹窗）

**触发**：用户在表格行点击"收款"操作，弹窗录入金额。
**来源**：HTML 原型 `openRowActionModal` / `submitRowAction`。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as ReceivableView
    participant VM as ReceivableViewModel
    participant App as ReceivableAppService
    participant Debt as MonthlyDebt(聚合根)
    participant Repo as IReceivableRepository
    participant UoW as UnitOfWork

    U->>V: 点击行"收款"
    V->>VM: OpenPaymentModal(debtId)
    VM-->>V: 显示弹窗 (月份/金额/备注)

    U->>V: 输入金额 + 备注，点"确定"
    V->>VM: SubmitPaymentCommand
    VM->>App: RegisterPaymentCommand(debtId, amount, remark)
    App->>App: FluentValidation 校验(金额>0)
    App->>Repo: GetById(debtId)
    Repo-->>App: MonthlyDebt
    App->>Debt: RecordPayment(amount, remark, date)

    Debt->>Debt: 校验金额 > 0
    Debt->>Debt: 创建 Payment 实体
    Debt->>Debt: 追加到 Payments 列表
    Debt->>Debt: 发布 PaymentRegisteredDomainEvent

    App->>Repo: Save(debt)
    Repo->>UoW: SaveChanges()
    UoW-->>Repo: 成功

    App-->>VM: 成功
    VM->>VM: 关闭弹窗 + 刷新该行差额
    VM-->>V: 绑定更新
    V-->>U: 显示成功提示 + 差额刷新
```

---

## 流程 6：应收管理 — 新增月份欠款

**触发**：用户点击"＋ 新增月份欠款"，弹窗录入月份+金额。
**来源**：HTML 原型 `openNewMonthModal` / `submitNewMonth`。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as ReceivableView
    participant VM as ReceivableViewModel
    participant App as ReceivableAppService
    participant Debt as MonthlyDebt(聚合根)
    participant Repo as IReceivableRepository

    U->>V: 点击"＋ 新增月份欠款"
    V->>VM: OpenNewMonthModal
    VM-->>V: 显示弹窗(月份/欠款金额)

    U->>V: 选月份 + 输入金额，点"新增"
    V->>VM: SubmitNewMonthCommand
    VM->>App: RecordDebtCommand(month, amount, customer?, salesman?)
    App->>App: FluentValidation 校验
    App->>Repo: 查找/创建 MonthlyDebt(month, customer, salesman)
    App->>Debt: UpdateDebt(amount) / 新建聚合根
    Debt->>Debt: 发布 DebtRecordedDomainEvent
    App->>Repo: Save(debt)
    App-->>VM: 成功
    VM->>VM: 关闭弹窗 + 刷新列表
    VM-->>V: 绑定更新
    V-->>U: 显示新增成功
```

---

## 流程 7：应收管理 — 导出 Excel 明细

**触发**：用户点击"导出Excel明细"。
**来源**：HTML 原型 `exportExcel()` + ExcelProject `ExportFailedRecords` 模式。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as ReceivableView
    participant VM as ReceivableViewModel
    participant App as ReceivableAppService
    participant Exc as ExcelExportService
    participant Repo as IReceivableRepository

    U->>V: 点击"导出Excel明细"
    V->>VM: ExportCommand
    VM->>App: ExportReceivablesCommand(currentCriteria)
    App->>Repo: Query(criteria)
    Repo-->>App: List~MonthlyDebt~
    App->>Exc: ExportReceivables(records)
    Exc->>Exc: NPOI 创建 Workbook
    Exc->>Exc: 写表头 + 数据行
    Exc->>Exc: 设置列宽
    Exc->>Exc: 写文件
    Exc-->>App: 文件路径
    App-->>VM: 路径
    VM->>VM: 追加终端日志
    VM-->>V: 提示完成
    V-->>U: 显示导出路径
```

---

## 流程 8：账套管理 — 重置导入数据

**触发**：用户点击"重置导入数据"。
**来源**：ExcelProject `ResetBtn_Click`。

```mermaid
sequenceDiagram
    actor U as 用户
    participant V as AccountView
    participant VM as AccountViewModel
    participant App as AccountAppService
    participant Store as InMemoryImportRecordStore
    participant Log as Serilog

    U->>V: 点击"重置导入数据"
    V->>VM: ResetCommand
    VM->>App: ResetImportCommand
    App->>Store: ClearImportRecords()
    App->>Log: "[重置] 已清空导入数据"
    App-->>VM: 成功
    VM->>VM: 清空预览表格 + 终端日志
    VM-->>V: 绑定更新
    V-->>U: 显示已重置
```

---

## 流程 9：应用启动与 DI 装配

**触发**：程序启动。对应 `App.xaml.cs` 组合根。

```mermaid
sequenceDiagram
    participant App as App.xaml.cs
    participant DI as ServiceCollection
    participant Db as PlErpDbContext
    participant Migr as DbMigrator
    participant Log as SerilogConfigurator
    participant Main as MainWindow

    App->>Log: 配置 Serilog (File + Console)
    App->>DI: AddInfrastructure() → 注册 DbContext/Repo/ExcelService
    App->>DI: AddApplication() → 注册 AppService
    App->>DI: AddPresentation() → 注册 ViewModel
    App->>DI: Build ServiceProvider
    App->>Db: 构造 DbContext
    App->>Migr: EnsureMigrated(plerp.db)
    Migr->>Db: 应用 EF Core 迁移
    App->>Main: 解析 MainWindow + MainViewModel
    Main-->>App: 显示主窗口
```

---

_本文档为 Stage 3 UML Engineering Package 的一部分。时序图在用户批准后锁定，Stage 5 实现必须覆盖所有列出的流程。_