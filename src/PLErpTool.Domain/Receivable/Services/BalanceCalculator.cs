using PLErpTool.Domain.Receivable;
using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Receivable.Services;

public static class BalanceCalculator
{
    public static Money CalculateBalance(Money debt, Money collected) => collected.Subtract(debt);
}