using Microsoft.EntityFrameworkCore;
using PLErpTool.Domain.Receivable;
using PLErpTool.Domain.Shared;

namespace PLErpTool.Infrastructure.Persistence.Repositories;

public sealed class ReceivableRepository : IReceivableRepository
{
    private static readonly System.Threading.SemaphoreSlim Gate = new(1, 1);

    private readonly PlErpDbContext _db;

    public ReceivableRepository(PlErpDbContext db) => _db = db;

    private async Task<T> SerializedAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await action(ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task SerializedAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            await action(ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    public Task<List<Customer>> GetCustomerTreeAsync(CancellationToken ct = default)
        => SerializedAsync(ct => _db.Customers.Include(c => c.Salesmen).AsNoTracking().ToListAsync(ct), ct);

    public Task<List<MonthlyDebt>> GetAllDebtsAsync(CancellationToken ct = default)
        => SerializedAsync(ct => _db.MonthlyDebts.Include(d => d.Payments).AsNoTracking().ToListAsync(ct), ct);

    public Task<List<MonthlyDebt>> GetDebtsByCustomerAsync(long customerId, CancellationToken ct = default)
        => SerializedAsync(ct => _db.MonthlyDebts.Include(d => d.Payments)
            .Where(d => d.CustomerId == customerId).AsNoTracking().ToListAsync(ct), ct);

    public Task<List<MonthlyDebt>> GetDebtsByCustomerAndSalesmanAsync(long customerId, long? salesmanId, CancellationToken ct = default)
        => SerializedAsync(ct => _db.MonthlyDebts.Include(d => d.Payments)
            .Where(d => d.CustomerId == customerId && d.SalesmanId == salesmanId)
            .AsNoTracking().ToListAsync(ct), ct);

    public Task<MonthlyDebt?> GetByIdAsync(long id, CancellationToken ct = default)
        => SerializedAsync(ct => _db.MonthlyDebts.Include(d => d.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct), ct);

    public Task SaveAsync(MonthlyDebt debt, CancellationToken ct = default)
        => SerializedAsync(async ct2 =>
        {
            // 此仓储在桌面应用的长期 Scope 内复用。每次保存均以传入聚合为唯一事实来源，
            // 清理上次操作的跟踪状态，避免相同主键或唯一索引的实体跨操作互相冲突。
            _db.ChangeTracker.Clear();

            if (debt.Id == 0)
            {
                _db.MonthlyDebts.Add(debt);
            }
            else
            {
                _db.MonthlyDebts.Attach(debt);
                _db.Entry(debt).State = EntityState.Modified;
                foreach (var payment in debt.Payments)
                    _db.Entry(payment).State = payment.Id == 0 ? EntityState.Added : EntityState.Modified;
            }
            await _db.SaveChangesAsync(ct2);
        }, ct);

    public Task<PagedResult<MonthlyDebt>> QueryAsync(QueryCriteria criteria, int page, int pageSize, CancellationToken ct = default)
        => SerializedAsync(async ct2 =>
        {
            var query = _db.MonthlyDebts.Include(d => d.Payments).AsNoTracking();

            if (criteria.CustomerId is { } customerId)
                query = query.Where(d => d.CustomerId == customerId);
            else if (!string.IsNullOrWhiteSpace(criteria.CustomerName))
            {
                var custIds = await _db.Customers.Where(c => c.ShortName.Contains(criteria.CustomerName))
                    .Select(c => c.Id).ToListAsync(ct2);
                query = query.Where(d => custIds.Contains(d.CustomerId));
            }
            if (criteria.SalesmanId is { } salesmanId)
                query = query.Where(d => d.SalesmanId == salesmanId);
            else if (!string.IsNullOrWhiteSpace(criteria.SalesmanName))
            {
                var spIds = await _db.Salesmen.Where(s => s.Name.Contains(criteria.SalesmanName))
                    .Select(s => s.Id).ToListAsync(ct2);
                query = query.Where(d => d.SalesmanId.HasValue && spIds.Contains(d.SalesmanId.Value));
            }
            if (criteria.MonthRange is { } range)
            {
                var startMonth = range.Start;
                var endMonth = range.End;
                query = query.Where(d => d.Month >= startMonth && d.Month <= endMonth);
            }
            if (criteria.NegativeBalanceOnly)
                query = query.Where(d => d.CollectedAmount < d.DebtAmount);

            var total = await query.CountAsync(ct2);
            var items = await query.OrderBy(d => d.Month).ThenBy(d => d.CustomerId)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct2);
            return new PagedResult<MonthlyDebt>(items, total, page, pageSize);
        }, ct);

    public Task<decimal> GetTotalDebtAsync(QueryCriteria criteria, CancellationToken ct = default)
        => SerializedAsync(async ct2 =>
        {
            var query = _db.MonthlyDebts.AsNoTracking();

            if (criteria.CustomerId is { } customerId)
                query = query.Where(d => d.CustomerId == customerId);
            else if (!string.IsNullOrWhiteSpace(criteria.CustomerName))
            {
                var custIds = await _db.Customers.Where(c => c.ShortName.Contains(criteria.CustomerName))
                    .Select(c => c.Id).ToListAsync(ct2);
                query = query.Where(d => custIds.Contains(d.CustomerId));
            }
            if (criteria.SalesmanId is { } salesmanId)
                query = query.Where(d => d.SalesmanId == salesmanId);
            else if (!string.IsNullOrWhiteSpace(criteria.SalesmanName))
            {
                var spIds = await _db.Salesmen.Where(s => s.Name.Contains(criteria.SalesmanName))
                    .Select(s => s.Id).ToListAsync(ct2);
                query = query.Where(d => d.SalesmanId.HasValue && spIds.Contains(d.SalesmanId.Value));
            }
            if (criteria.MonthRange is { } range)
                query = query.Where(d => d.Month >= range.Start && d.Month <= range.End);
            if (criteria.NegativeBalanceOnly)
                query = query.Where(d => d.CollectedAmount < d.DebtAmount);

            var amounts = await query.Select(d => d.DebtAmount).ToListAsync(ct2);
            return amounts.Sum(amount => amount.Value);
        }, ct);

    public Task<Customer?> FindCustomerByShortNameAsync(string shortName, CancellationToken ct = default)
        => SerializedAsync(ct2 => _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ShortName == shortName, ct2), ct);

    public Task<Salesman?> FindSalesmanByCustomerIdAndNameAsync(long customerId, string name, CancellationToken ct = default)
        => SerializedAsync(ct2 => _db.Salesmen.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.Name == name, ct2), ct);

    public Task AddCustomerAsync(Customer customer, CancellationToken ct = default)
        => SerializedAsync(ct2 => _db.Customers.AddAsync(customer, ct2).AsTask(), ct);

    public Task AddSalesmanAsync(Salesman salesman, CancellationToken ct = default)
        => SerializedAsync(ct2 => _db.Salesmen.AddAsync(salesman, ct2).AsTask(), ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => SerializedAsync(ct2 => _db.SaveChangesAsync(ct2), ct);

    public Task AddChangeLogsAsync(IEnumerable<PaymentChangeLog> logs, CancellationToken ct = default)
        => SerializedAsync(async ct2 =>
        {
            await _db.PaymentChangeLogs.AddRangeAsync(logs, ct2);
            await _db.SaveChangesAsync(ct2);
        }, ct);
}
