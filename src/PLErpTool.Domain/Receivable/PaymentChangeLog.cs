namespace PLErpTool.Domain.Receivable;

public class PaymentChangeLog
{
    public long Id { get; init; }
    public long PaymentId { get; init; }
    public long MonthlyDebtId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Month { get; init; } = string.Empty;
    public string DocumentNo { get; init; } = string.Empty;
    public string PaymentType { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string OldValue { get; init; } = string.Empty;
    public string NewValue { get; init; } = string.Empty;
    public decimal OldAmount { get; init; }
    public decimal NewAmount { get; init; }
    public string? Operator { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.Now;

    private PaymentChangeLog() { }

    public PaymentChangeLog(
        long paymentId, long monthlyDebtId,
        string customerName, string month, string documentNo, string paymentType,
        string action, string field, string oldValue, string newValue,
        decimal oldAmount, decimal newAmount, string? op = null)
    {
        PaymentId = paymentId;
        MonthlyDebtId = monthlyDebtId;
        CustomerName = customerName;
        Month = month;
        DocumentNo = documentNo;
        PaymentType = paymentType;
        Action = action;
        Field = field;
        OldValue = oldValue;
        NewValue = newValue;
        OldAmount = oldAmount;
        NewAmount = newAmount;
        Operator = op;
        ChangedAt = DateTime.Now;
    }
}