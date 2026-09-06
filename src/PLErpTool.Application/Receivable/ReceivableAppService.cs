using FluentValidation;
using PLErpTool.Domain.Receivable;
using PLErpTool.Domain.Receivable.Events;
using PLErpTool.Domain.Shared;
using PLErpTool.Application.Receivable.Commands;
using PLErpTool.Application.Receivable.Dtos;
using PLErpTool.Application.Receivable.Queries;
using PLErpTool.Application.Receivable.Validators;

namespace PLErpTool.Application.Receivable;

public sealed class ReceivableAppService
{
    private readonly IReceivableRepository _repo;
    private Dictionary<long, string>? _customerNames;
    private Dictionary<long, string>? _salesmanNames;

    public ReceivableAppService(IReceivableRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<CustomerTreeNodeDto>> GetCustomerTreeAsync(CancellationToken ct = default)
    {
        var customers = await _repo.GetCustomerTreeAsync(ct);
        CacheNames(customers);
        return customers.Select(c => new CustomerTreeNodeDto(
            c.Id, c.ShortName, true,
            c.Salesmen.Select(s => new CustomerTreeNodeDto(s.Id, s.Name, false, new())).ToList()
        )).ToList();
    }

    public async Task<List<PersonnelInfoDto>> GetPersonnelAsync(
        string? customerKeyword, string? salesmanKeyword, CancellationToken ct = default)
    {
        var customerTerm = customerKeyword?.Trim() ?? string.Empty;
        var salesmanTerm = salesmanKeyword?.Trim() ?? string.Empty;
        var customers = await _repo.GetCustomerTreeAsync(ct);

        return customers
            .Where(customer => string.IsNullOrEmpty(customerTerm)
                || customer.ShortName.Contains(customerTerm, StringComparison.OrdinalIgnoreCase)
                || customer.FullName.Contains(customerTerm, StringComparison.OrdinalIgnoreCase))
            .SelectMany(customer => customer.Salesmen.DefaultIfEmpty(), (customer, salesman) => new PersonnelInfoDto(
                customer.Id,
                customer.ShortName,
                customer.FullName,
                customer.CreatedAt,
                salesman?.Id,
                salesman?.Name,
                salesman?.CreatedAt))
            .Where(item => string.IsNullOrEmpty(salesmanTerm)
                || item.SalesmanName?.Contains(salesmanTerm, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(item => item.CustomerShortName)
            .ThenBy(item => item.SalesmanName)
            .ToList();
    }

    public async Task AddCustomerAsync(string shortName, string? fullName, CancellationToken ct = default)
    {
        var normalizedShortName = shortName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedShortName))
            throw new ArgumentException("客户简称不能为空。", nameof(shortName));
        if (await _repo.FindCustomerByShortNameAsync(normalizedShortName, ct) is not null)
            throw new InvalidOperationException("该客户已存在。");

        await _repo.AddCustomerAsync(new Customer(normalizedShortName,
            string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim()), ct);
        await _repo.SaveChangesAsync(ct);
        InvalidateNameCache();
    }

    public async Task AddSalesmanAsync(long customerId, string name, CancellationToken ct = default)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("业务员姓名不能为空。", nameof(name));
        if (await _repo.FindSalesmanByCustomerIdAndNameAsync(customerId, normalizedName, ct) is not null)
            throw new InvalidOperationException("该客户下已存在同名业务员。");

        var customerExists = (await _repo.GetCustomerTreeAsync(ct)).Any(customer => customer.Id == customerId);
        if (!customerExists)
            throw new InvalidOperationException("所选客户不存在或已被删除。");

        await _repo.AddSalesmanAsync(new Salesman(customerId, normalizedName), ct);
        await _repo.SaveChangesAsync(ct);
        InvalidateNameCache();
    }

    public async Task<List<MonthlyDebtDto>> LoadAllDebtsAsync(CancellationToken ct = default)
    {
        var debts = await _repo.GetAllDebtsAsync(ct);
        return ToDtos(debts);
    }

    public async Task<List<MonthlyDebtDto>> LoadDebtsByCustomerAsync(long customerId, CancellationToken ct = default)
    {
        var debts = await _repo.GetDebtsByCustomerAsync(customerId, ct);
        return ToDtos(debts);
    }

    public async Task<List<MonthlyDebtDto>> LoadDebtsByCustomerAndSalesmanAsync(long customerId, long salesmanId, CancellationToken ct = default)
    {
        var debts = await _repo.GetDebtsByCustomerAndSalesmanAsync(customerId, salesmanId, ct);
        return ToDtos(debts);
    }

    public async Task<(List<MonthlyDebtDto> Items, int TotalCount, int TotalPages)> QueryReceivablesAsync(QueryReceivablesQuery query, CancellationToken ct = default)
    {
        var criteria = ToCriteria(query);
        var result = await _repo.QueryAsync(criteria, query.Page, query.PageSize, ct);
        var names = await GetCustomerSalesmanNamesAsync(result.Items, ct);
        return (ToDtos(result.Items, names), result.TotalCount, result.TotalPages);
    }

    public Task<decimal> GetTotalDebtAsync(QueryReceivablesQuery query, CancellationToken ct = default)
        => _repo.GetTotalDebtAsync(ToCriteria(query), ct);

    private static QueryCriteria ToCriteria(QueryReceivablesQuery query)
    {
        DateRange? range = null;
        if (query.StartMonth is { } s && query.EndMonth is { } e)
            range = new DateRange(s, e);
        else if (query.StartMonth is { } s2)
            range = new DateRange(s2, s2);
        else if (query.EndMonth is { } e2)
            range = new DateRange(e2, e2);

        return new QueryCriteria(query.CustomerName, query.SalesmanName, range, query.NegativeBalanceOnly,
            query.CustomerId, query.SalesmanId);
    }

    public async Task<MonthlyDebtDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var debt = await _repo.GetByIdAsync(id, ct);
        if (debt is null) return null;
        return ToDtos(new[] { debt }).First();
    }

    public async Task<(long DebtId, decimal DebtAmount, decimal CollectedAmount, PaymentDto NewPayment)> RegisterPaymentAsync(RegisterPaymentCommand cmd, CancellationToken ct = default)
    {
        var validator = new RegisterPaymentValidator();
        await validator.ValidateAndThrowAsync(cmd, ct);

        var debt = await _repo.GetByIdAsync(cmd.MonthlyDebtId, ct)
            ?? throw new InvalidOperationException($"未找到欠款记录 {cmd.MonthlyDebtId}");
        var payment = debt.RecordPayment(new Money(cmd.Amount), cmd.Remark);
        await _repo.SaveAsync(debt, ct);
        var dto = new PaymentDto(payment.Id, debt.Id, payment.TradeDate,
            payment.Type == PaymentType.Collection ? "收款" : "欠款",
            payment.Quantity, payment.UnitPrice, payment.Amount.Value, payment.Remark,
            payment.DocumentNo, payment.ProductName, payment.SpecDetails, payment.CustomerMaterial,
            payment.ReconciliationRemark, payment.ReceivableRemark, payment.DebtType);
        return (debt.Id, debt.DebtAmount.Value, debt.CollectedAmount.Value, dto);
    }

    // 编辑明细行：数量/单价变更后重算该明细金额，并刷新父行(月汇总)DebtAmount
    public async Task<(long DebtId, decimal DebtAmount, decimal CollectedAmount, decimal Balance)> UpdateLineItemAsync(long monthlyDebtId, long paymentId, decimal quantity, decimal unitPrice, CancellationToken ct = default)
    {
        var debt = await _repo.GetByIdAsync(monthlyDebtId, ct)
            ?? throw new InvalidOperationException($"未找到欠款记录 {monthlyDebtId}");
        var payment = debt.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new InvalidOperationException($"未找到明细 {paymentId}");

        decimal oldQty = payment.Quantity, oldPrice = payment.UnitPrice, oldAmount = payment.Amount.Value;
        payment.EditLineItem(quantity, unitPrice);
        debt.RecomputeDebtAmount();
        await _repo.SaveAsync(debt, ct);

        // 记录变更日志（数量、单价、金额分别记录）
        var customerName = await GetCustomerNameAsync(debt.CustomerId, ct);
        var logs = new List<PaymentChangeLog>();
        void AddLog(string field, string oldV, string newV)
            => logs.Add(new PaymentChangeLog(payment.Id, debt.Id, customerName, debt.Month.Value,
                payment.DocumentNo, payment.Type == PaymentType.Debt ? "欠款" : "收款",
                "编辑", field, oldV, newV, oldAmount, payment.Amount.Value));
        if (oldQty != quantity) AddLog("数量", oldQty.ToString(), quantity.ToString());
        if (oldPrice != unitPrice) AddLog("单价", oldPrice.ToString(), unitPrice.ToString());
        if (oldAmount != payment.Amount.Value) AddLog("金额", oldAmount.ToString("F2"), payment.Amount.Value.ToString("F2"));
        if (logs.Count > 0) await _repo.AddChangeLogsAsync(logs, ct);

        return (debt.Id, debt.DebtAmount.Value, debt.CollectedAmount.Value, debt.Balance.Value);
    }

    public async Task UpdateReconciliationRemarkAsync(long monthlyDebtId, long paymentId, string reconciliationRemark,
        CancellationToken ct = default)
    {
        var debt = await _repo.GetByIdAsync(monthlyDebtId, ct)
            ?? throw new InvalidOperationException($"未找到欠款记录 {monthlyDebtId}");
        var payment = debt.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new InvalidOperationException($"未找到明细 {paymentId}");

        payment.EditRemarks(reconciliationRemark, payment.ReceivableRemark);
        await _repo.SaveAsync(debt, ct);
    }

    // 编辑收款明细金额：重算父行(月汇总)CollectedAmount
    public async Task<(long DebtId, decimal DebtAmount, decimal CollectedAmount, decimal Balance)> UpdateCollectionAmountAsync(long monthlyDebtId, long paymentId, decimal amount, CancellationToken ct = default)
    {
        var debt = await _repo.GetByIdAsync(monthlyDebtId, ct)
            ?? throw new InvalidOperationException($"未找到欠款记录 {monthlyDebtId}");
        var payment = debt.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new InvalidOperationException($"未找到明细 {paymentId}");

        decimal oldAmount = payment.Amount.Value;
        payment.EditAmount(amount);
        debt.RecomputeCollectedAmount();
        await _repo.SaveAsync(debt, ct);

        if (oldAmount != amount)
        {
            var customerName = await GetCustomerNameAsync(debt.CustomerId, ct);
            var log = new PaymentChangeLog(payment.Id, debt.Id, customerName, debt.Month.Value,
                payment.DocumentNo, "收款", "编辑", "金额",
                oldAmount.ToString("F2"), amount.ToString("F2"), oldAmount, amount);
            await _repo.AddChangeLogsAsync(new[] { log }, ct);
        }

        return (debt.Id, debt.DebtAmount.Value, debt.CollectedAmount.Value, debt.Balance.Value);
    }

    // 删除单条明细（软删除）：置 IsDeleted 标记并重算汇总，落库，返回父行新汇总
    public async Task<(long DebtId, decimal DebtAmount, decimal CollectedAmount)> DeleteLineItemAsync(long monthlyDebtId, long paymentId, CancellationToken ct = default)
    {
        var debt = await _repo.GetByIdAsync(monthlyDebtId, ct)
            ?? throw new InvalidOperationException($"未找到欠款记录 {monthlyDebtId}");
        var payment = debt.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null || payment.IsDeleted)
            throw new InvalidOperationException($"未找到明细 {paymentId}");

        decimal oldAmount = payment.Amount.Value;
        if (!debt.RemovePayment(paymentId))
            throw new InvalidOperationException($"未找到明细 {paymentId}");
        await _repo.SaveAsync(debt, ct);

        // 记录删除日志
        var customerName = await GetCustomerNameAsync(debt.CustomerId, ct);
        var log = new PaymentChangeLog(payment.Id, debt.Id, customerName, debt.Month.Value,
            payment.DocumentNo, payment.Type == PaymentType.Debt ? "欠款" : "收款",
            "删除", "明细", oldAmount.ToString("F2"), "0", oldAmount, 0m);
        await _repo.AddChangeLogsAsync(new[] { log }, ct);

        return (debt.Id, debt.DebtAmount.Value, debt.CollectedAmount.Value);
    }

    private async Task<string> GetCustomerNameAsync(long customerId, CancellationToken ct)
    {
        var tree = await _repo.GetCustomerTreeAsync(ct);
        return tree.FirstOrDefault(c => c.Id == customerId)?.ShortName ?? customerId.ToString();
    }

    public async Task RecordDebtAsync(RecordDebtCommand cmd, CancellationToken ct = default)
    {
        var validator = new RecordDebtValidator();
        await validator.ValidateAndThrowAsync(cmd, ct);

        var all = await _repo.GetDebtsByCustomerAndSalesmanAsync(cmd.CustomerId, cmd.SalesmanId, ct);
        var existing = all.FirstOrDefault(d => d.Month == cmd.Month);
        if (existing is not null)
        {
            existing.AddDebt(new Money(cmd.Amount), debtType: cmd.DebtType, reconciliationRemark: cmd.Remark);
            await _repo.SaveAsync(existing, ct);
        }
        else
        {
            var debt = new MonthlyDebt(cmd.CustomerId, cmd.SalesmanId, cmd.Month, new Money(cmd.Amount),
                debtType: cmd.DebtType, reconciliationRemark: cmd.Remark);
            await _repo.SaveAsync(debt, ct);
        }
    }

    private async Task<Dictionary<long, (string Customer, string Salesman)>> GetCustomerSalesmanNamesAsync(
        IReadOnlyCollection<MonthlyDebt> debts, CancellationToken ct)
    {
        if (_customerNames is null || _salesmanNames is null)
            CacheNames(await _repo.GetCustomerTreeAsync(ct));

        var customerNames = _customerNames!;
        var salesmanNames = _salesmanNames!;
        var map = new Dictionary<long, (string, string)>();
        foreach (var debt in debts)
            map[debt.Id] = (
                customerNames.GetValueOrDefault(debt.CustomerId, ""),
                debt.SalesmanId is { } salesmanId
                    ? salesmanNames.GetValueOrDefault(salesmanId, "")
                    : "");
        return map;
    }

    private void CacheNames(IEnumerable<Customer> customers)
    {
        _customerNames = customers.ToDictionary(customer => customer.Id, customer => customer.ShortName);
        _salesmanNames = customers
            .SelectMany(customer => customer.Salesmen)
            .ToDictionary(salesman => salesman.Id, salesman => salesman.Name);
    }

    private void InvalidateNameCache()
    {
        _customerNames = null;
        _salesmanNames = null;
    }

    private static List<MonthlyDebtDto> ToDtos(IEnumerable<MonthlyDebt> debts, Dictionary<long, (string Customer, string Salesman)>? names = null)
        => debts.Select(d => new MonthlyDebtDto(
            d.Id, d.CustomerId, names?.GetValueOrDefault(d.Id).Customer ?? "",
            d.SalesmanId, names?.GetValueOrDefault(d.Id).Salesman ?? "",
            d.Month.Value, d.DebtAmount.Value, d.CollectedAmount.Value, d.Balance.Value,
            d.Payments.OrderBy(p => p.Type == PaymentType.Debt ? 1 : 0).ThenBy(p => p.TradeDate).ThenBy(p => p.Id)
                .Select(p => new PaymentDto(
                    p.Id, d.Id, p.TradeDate,
                    p.Type == PaymentType.Debt ? "欠款" : "收款",
                    p.Quantity, p.UnitPrice, p.Amount.Value, p.Remark,
                    p.DocumentNo, p.ProductName, p.SpecDetails, p.CustomerMaterial,
                    p.ReconciliationRemark, p.ReceivableRemark, p.DebtType))
                .ToList()
        )).ToList();
}
