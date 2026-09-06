using PLErpTool.Domain.Shared;

namespace PLErpTool.Application.Receivable.Commands;

public sealed record RecordDebtCommand(
    long CustomerId,
    long? SalesmanId,
    MonthKey Month,
    decimal Amount,
    string? Remark = null,
    string DebtType = "");
