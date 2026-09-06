using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Receivable;

public sealed record QueryCriteria(
    string? CustomerName,
    string? SalesmanName,
    DateRange? MonthRange,
    bool NegativeBalanceOnly,
    long? CustomerId = null,
    long? SalesmanId = null);