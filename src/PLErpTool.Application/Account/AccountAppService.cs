using System.IO;
using PLErpTool.Domain.Account;
using PLErpTool.Domain.Receivable;
using PLErpTool.Domain.Shared;
using PLErpTool.Application.Account.Dtos;

namespace PLErpTool.Application.Account;

public sealed class AccountAppService
{
    private readonly IImportRecordStore _store;
    private readonly IExcelImportService _importService;
    private readonly IExcelConvertService _convertService;
    private readonly IExcelExportService _exportService;
    private readonly IReceivableRepository _repo;
    private readonly ICustomerTreeChangedNotifier _treeNotifier;

    public AccountAppService(
        IImportRecordStore store,
        IExcelImportService importService,
        IExcelConvertService convertService,
        IExcelExportService exportService,
        IReceivableRepository repo,
        ICustomerTreeChangedNotifier treeNotifier)
    {
        _store = store;
        _importService = importService;
        _convertService = convertService;
        _exportService = exportService;
        _repo = repo;
        _treeNotifier = treeNotifier;
    }

    public async Task<ImportResultDto> ImportSourceFilesAsync(string[] filePaths, CancellationToken ct = default)
    {
        var messages = new List<string>();
        int addedFiles = 0, addedRecords = 0, extracted = 0, duplicates = 0, filteredZero = 0;

        foreach (var path in filePaths)
        {
            var fileName = Path.GetFileName(path);
            if (_store.HasDuplicateFileName(fileName))
            {
                messages.Add($"[重复] {fileName} 已经重复选择。");
                continue;
            }

            try
            {
                var result = await _importService.ImportExcelFileAsync(path, _store, ct);
                _store.AddFileName(fileName);
                addedFiles++;
                extracted += result.ExtractedRecordCount;
                addedRecords += result.AddedRecordCount;
                duplicates += result.DuplicateRecordCount;
                filteredZero += result.FilteredZeroQuantityCount;
                messages.Add($"[导入成功] {fileName}，提取 {result.ExtractedRecordCount} 条，新增 {result.AddedRecordCount} 条，重复 {result.DuplicateRecordCount} 条，过滤数量为 0 的数据 {result.FilteredZeroQuantityCount} 条。");
            }
            catch (Exception ex)
            {
                messages.Add($"[导入失败] {fileName}，原因：{ex.Message}");
            }
        }

        messages.Add($"本次新增文件 {addedFiles} 个，新增数据 {addedRecords} 条。");
        messages.Add($"累计已选择文件 {_store.FileNames.Count} 个，累计有效数据 {_store.Count} 条。");

        // 导入源文件后同步明细到应收账款（按月汇总写入 MonthlyDebt/Payments）
        if (addedRecords > 0)
        {
            var (syncedDebts, syncedLines, filteredZeroReceivable) = await SyncImportRecordsToReceivableAsync(ct);
            messages.Add($"[应收同步] 新增/更新月份欠款 {syncedDebts} 条，明细 {syncedLines} 条，过滤应收金额为 0 的数据 {filteredZeroReceivable} 条。");
            if (syncedDebts > 0 || syncedLines > 0)
                _treeNotifier.NotifyChanged();
        }

        return new ImportResultDto(addedFiles, addedRecords, extracted, duplicates, filteredZero,
            _store.FileNames.Count, _store.Count, messages);
    }

    // 把导入的明细同步到应收账款：按(客户+业务员+月份)汇总成 MonthlyDebt，每条作为一条 Debt 明细
    private async Task<(int SyncedDebts, int SyncedLines, int FilteredZeroReceivable)> SyncImportRecordsToReceivableAsync(CancellationToken ct)
    {
        int syncedDebts = 0, syncedLines = 0;
        var allRecords = _store.GetImportRecords().ToList();
        // “应收金额”是可选列；缺失时 ImportRecord.Amount 会按数量 × 单价计算。
        // 因此同步判定必须使用统一的有效金额，不能因该列为空而丢失整条应收明细。
        int filteredZeroReceivable = allRecords.Count(r => r.Amount.Value == 0m);
        var records = allRecords.Where(r => r.Amount.Value != 0m).ToList();

        // 先同步客户/业务员主数据，拿到 ID 映射
        var (_, _, custSpMap) = await SyncCustomerSalesmanAsync(records, ct);

        var groups = records
            .GroupBy(r => (r.CustomerShortName, r.Salesman, r.MonthKey));

        foreach (var g in groups)
        {
            if (!custSpMap.TryGetValue((g.Key.CustomerShortName, g.Key.Salesman), out var ids)) continue;
            long customerId = ids.CustomerId;
            long? salesmanId = ids.SalesmanId;

            // 查找该(客户+业务员+月份)的 MonthlyDebt
            var debts = await _repo.GetDebtsByCustomerAndSalesmanAsync(customerId, salesmanId, ct);
            var existing = debts.FirstOrDefault(d => d.Month == g.Key.MonthKey);
            MonthlyDebt? debt;
            bool isNew = existing is null;
            if (isNew)
            {
                debt = new MonthlyDebt(customerId, salesmanId, g.Key.MonthKey, Money.Zero);
            }
            else
            {
                // 已存在：重新加载带 Payments 的实体用于编辑
                debt = await _repo.GetByIdAsync(existing!.Id, ct);
                if (debt is null) continue;
            }

            // 收集已存在的明细键，避免重复导入
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in debt.Payments.Where(p => p.Type == PaymentType.Debt))
            {
                existingKeys.Add($"{p.TradeDate:yyyy/M/d}|{p.DocumentNo}|{p.ProductName}|{p.SpecDetails}|{p.Quantity}|{p.UnitPrice}");
            }

            int added = 0;
            foreach (var rec in g.OrderBy(r => r.TradeDateValue))
            {
                string key = $"{rec.TradeDateValue:yyyy/M/d}|{rec.DocumentNumber}|{rec.ProductName}|{rec.Spec}|{rec.Quantity}|{rec.UnitPrice}";
                if (existingKeys.Contains(key)) continue;
                debt.AddDebtLine(rec.TradeDateValue, rec.Quantity, rec.UnitPrice,
                    rec.DocumentNumber, rec.ProductName, rec.Spec, rec.CustomerMaterial);
                added++;
            }

            if (added > 0)
            {
                await _repo.SaveAsync(debt, ct);
                syncedDebts += isNew ? 1 : 0;
                syncedLines += added;
            }
        }

        return (syncedDebts, syncedLines, filteredZeroReceivable);
    }

    public async Task<ConvertResultDto> ConvertToMasterAsync(string masterPath, CancellationToken ct = default)
    {
        var messages = new List<string>();
        var records = _store.GetImportRecords().ToList();

        if (records.Count == 0)
        {
            messages.Add("[转换] 没有可写入的导入数据。");
            return new ConvertResultDto(0, 0, 0, 0, "", masterPath, messages);
        }

        if (string.IsNullOrWhiteSpace(masterPath))
        {
            messages.Add("[转换] 请先导入主账文件。");
            return new ConvertResultDto(0, 0, 0, 0, "", masterPath, messages);
        }

        int syncedCustomers = 0, syncedSalesmen = 0;
        var customerSalesmanMap = new Dictionary<(string, string), (long CustomerId, long? SalesmanId)>();

        messages.Add("[转换] 开始主数据同步（客户/业务员：无则新增，有则不新增）。");

        (syncedCustomers, syncedSalesmen, customerSalesmanMap) =
            await SyncCustomerSalesmanAsync(records, ct);

        messages.Add($"[主数据同步] 新增客户 {syncedCustomers} 个，新增业务员 {syncedSalesmen} 个。");

        var convertResult = await _convertService.ConvertAsync(masterPath, records, customerSalesmanMap, ct);
        messages.AddRange(convertResult.Messages);

        // 转换过程中可能新增了客户/业务员，通知应收账款页刷新客户树
        if (syncedCustomers > 0 || syncedSalesmen > 0)
            _treeNotifier.NotifyChanged();

        return new ConvertResultDto(
            convertResult.TotalWritten, convertResult.TotalSkipped,
            syncedCustomers, syncedSalesmen,
            convertResult.BackupPath, masterPath, messages);
    }

    public async Task<ImportResultDto> ImportCustomerSalesmanAsync(string[] filePaths, CancellationToken ct = default)
    {
        var messages = new List<string>();
        int addedFiles = 0, syncedCustomers = 0, syncedSalesmen = 0;

        messages.Add("[导入客户业务员关系] 开始解析源文件并同步客户/业务员（无则新增，有则跳过）。");

        foreach (var path in filePaths)
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var result = await _importService.ImportExcelFileAsync(path, _store, ct);
                addedFiles++;
                messages.Add($"[解析] {fileName}，提取 {result.ExtractedRecordCount} 条。");
            }
            catch (Exception ex)
            {
                messages.Add($"[导入失败] {fileName}，原因：{ex.Message}");
            }
        }

        var records = _store.GetImportRecords().ToList();
        if (records.Count > 0)
        {
            (syncedCustomers, syncedSalesmen, _) = await SyncCustomerSalesmanAsync(records, ct);
            messages.Add($"[同步完成] 新增客户 {syncedCustomers} 个，新增业务员 {syncedSalesmen} 个。");

            if (syncedCustomers > 0 || syncedSalesmen > 0)
                _treeNotifier.NotifyChanged();
        }
        else
        {
            messages.Add("[同步完成] 没有可同步的客户/业务员数据。");
        }

        // 仅导入关系，不写入主账；清空导入缓存避免影响后续"转换"操作
        _store.ClearImportRecords();
        messages.Add($"本次解析文件 {addedFiles} 个。");

        return new ImportResultDto(addedFiles, records.Count, records.Count, 0, 0, 0, 0, messages);
    }

    private async Task<(int SyncedCustomers, int SyncedSalesmen, Dictionary<(string, string), (long CustomerId, long? SalesmanId)> Map)>
        SyncCustomerSalesmanAsync(IReadOnlyList<ImportRecord> records, CancellationToken ct)
    {
        int syncedCustomers = 0, syncedSalesmen = 0;
        var customerSalesmanMap = new Dictionary<(string, string), (long CustomerId, long? SalesmanId)>();

        var groups = records.GroupBy(r => (r.CustomerShortName, r.Salesman));
        foreach (var group in groups)
        {
            var (custName, salesmanName) = group.Key;
            var customer = await _repo.FindCustomerByShortNameAsync(custName, ct);
            if (customer is null)
            {
                customer = new Customer(custName);
                await _repo.AddCustomerAsync(customer, ct);
                await _repo.SaveChangesAsync(ct);
                syncedCustomers++;
                customer = await _repo.FindCustomerByShortNameAsync(custName, ct);
            }

            long? salesmanId = null;
            if (!string.IsNullOrWhiteSpace(salesmanName) && customer is not null)
            {
                var salesman = await _repo.FindSalesmanByCustomerIdAndNameAsync(customer.Id, salesmanName, ct);
                if (salesman is null)
                {
                    salesman = new Salesman(customer.Id, salesmanName);
                    await _repo.AddSalesmanAsync(salesman, ct);
                    await _repo.SaveChangesAsync(ct);
                    syncedSalesmen++;
                    salesman = await _repo.FindSalesmanByCustomerIdAndNameAsync(customer.Id, salesmanName, ct);
                }
                salesmanId = salesman?.Id;
            }

            if (customer is not null)
                customerSalesmanMap[(custName, salesmanName)] = (customer.Id, salesmanId);
        }

        return (syncedCustomers, syncedSalesmen, customerSalesmanMap);
    }

    public void ResetImportData()
    {
        _store.ClearImportRecords();
    }

    public List<ImportRecordPreviewDto> GetPreviewRecords()
    {
        return _store.GetImportRecords()
            .Select(r => new ImportRecordPreviewDto(
                r.CustomerShortName,
                r.TradeDateValue.ToString("yyyy/M/d"),
                r.DocumentNumber, r.ProductName, r.Spec,
                r.Quantity, r.UnitPrice, r.CustomerMaterial, r.Salesman))
            .ToList();
    }

    public async Task<string> ExportFailedRecordsAsync(CancellationToken ct = default)
    {
        return await _exportService.ExportFailedRecordsAsync(_store.GetImportRecords(), ct);
    }
}
