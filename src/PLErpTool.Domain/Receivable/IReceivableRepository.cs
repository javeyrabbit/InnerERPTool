namespace PLErpTool.Domain.Receivable;

public interface IReceivableRepository
{
    Task<List<Customer>> GetCustomerTreeAsync(CancellationToken ct = default);
    Task<List<MonthlyDebt>> GetAllDebtsAsync(CancellationToken ct = default);
    Task<List<MonthlyDebt>> GetDebtsByCustomerAsync(long customerId, CancellationToken ct = default);
    Task<List<MonthlyDebt>> GetDebtsByCustomerAndSalesmanAsync(long customerId, long? salesmanId, CancellationToken ct = default);
    Task<MonthlyDebt?> GetByIdAsync(long id, CancellationToken ct = default);
    Task SaveAsync(MonthlyDebt debt, CancellationToken ct = default);
    Task<PagedResult<MonthlyDebt>> QueryAsync(QueryCriteria criteria, int page, int pageSize, CancellationToken ct = default);
    Task<decimal> GetTotalDebtAsync(QueryCriteria criteria, CancellationToken ct = default);

    Task<Customer?> FindCustomerByShortNameAsync(string shortName, CancellationToken ct = default);
    Task<Salesman?> FindSalesmanByCustomerIdAndNameAsync(long customerId, string name, CancellationToken ct = default);
    Task AddCustomerAsync(Customer customer, CancellationToken ct = default);
    Task AddSalesmanAsync(Salesman salesman, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task AddChangeLogsAsync(IEnumerable<PaymentChangeLog> logs, CancellationToken ct = default);
}
