using PLErpTool.Domain.Shared;

namespace PLErpTool.Application.Receivable.Commands;

public sealed record RegisterPaymentCommand(
    long MonthlyDebtId,
    decimal Amount,
    string? Remark);