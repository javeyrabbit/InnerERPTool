using PLErpTool.Domain.Receivable;
using PLErpTool.Domain.Shared;

namespace PLErpTool.Application.Receivable.Queries;

public sealed record QueryReceivablesQuery(
    string? CustomerName,
    string? SalesmanName,
    MonthKey? StartMonth,
    MonthKey? EndMonth,
    bool NegativeBalanceOnly,
    int Page = 1,
    int PageSize = 50,
    long? CustomerId = null,
    long? SalesmanId = null);