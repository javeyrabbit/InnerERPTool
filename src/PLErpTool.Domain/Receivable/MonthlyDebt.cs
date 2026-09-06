using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Receivable;

public class MonthlyDebt
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public long? SalesmanId { get; init; }
    public MonthKey Month { get; init; }
    public Money DebtAmount { get; private set; }
    public Money CollectedAmount { get; private set; }
    public List<Payment> Payments { get; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Money Balance => CollectedAmount.Subtract(DebtAmount);
    public bool IsNegativeBalance => Balance.IsNegative;

    private MonthlyDebt() { }

    public MonthlyDebt(long customerId, long? salesmanId, MonthKey month, Money debtAmount, string? remark = null,
        string? debtType = null, string? reconciliationRemark = null)
    {
        CustomerId = customerId;
        SalesmanId = salesmanId;
        Month = month;
        DebtAmount = debtAmount;
        CollectedAmount = Money.Zero;
        if (debtAmount.Value > 0m)
            Payments.Add(new Payment(0, DateTime.Now, PaymentType.Debt, debtAmount, remark, debtType, reconciliationRemark));
    }

    public void UpdateDebt(Money amount)
    {
        if (amount.Value < 0m)
            throw new ArgumentException("欠款金额不能为负", nameof(amount));
        DebtAmount = amount;
        UpdatedAt = DateTime.Now;
    }

    public void AddDebt(Money amount, string? remark = null, string? debtType = null, string? reconciliationRemark = null)
    {
        if (amount.Value < 0m)
            throw new ArgumentException("累加金额不能为负", nameof(amount));
        if (amount.Value > 0m)
            Payments.Add(new Payment(Id, DateTime.Now, PaymentType.Debt, amount, remark, debtType, reconciliationRemark));
        DebtAmount = DebtAmount.Add(amount);
        UpdatedAt = DateTime.Now;
    }

    // 新增一条欠款明细（导入源文件用）：金额由数量*单价计算，并累加到 DebtAmount
    public Payment AddDebtLine(DateTime tradeDate, decimal quantity, decimal unitPrice,
        string documentNo, string productName, string specDetails, string customerMaterial,
        string reconciliationRemark = "", string receivableRemark = "")
    {
        var payment = new Payment(Id, tradeDate, PaymentType.Debt, quantity, unitPrice,
            remark: null, documentNo, productName, specDetails, customerMaterial,
            reconciliationRemark, receivableRemark);
        Payments.Add(payment);
        DebtAmount = DebtAmount.Add(payment.Amount);
        UpdatedAt = DateTime.Now;
        return payment;
    }

    // 编辑明细后重算 DebtAmount（按未删除的 Debt 明细金额求和）
    public void RecomputeDebtAmount()
    {
        decimal sum = 0m;
        foreach (var p in Payments)
            if (!p.IsDeleted && p.Type == PaymentType.Debt)
                sum += p.Amount.Value;
        DebtAmount = new Money(sum);
        UpdatedAt = DateTime.Now;
    }

    // 编辑收款明细后重算 CollectedAmount（按未删除的 Collection 明细金额求和）
    public void RecomputeCollectedAmount()
    {
        decimal sum = 0m;
        foreach (var p in Payments)
            if (!p.IsDeleted && p.Type == PaymentType.Collection)
                sum += p.Amount.Value;
        CollectedAmount = new Money(sum);
        UpdatedAt = DateTime.Now;
    }

    // 软删除一条明细：仅置标记，并重算父行汇总（排除已删除项）
    public bool RemovePayment(long paymentId)
    {
        var payment = Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null || payment.IsDeleted) return false;
        payment.SoftDelete();
        if (payment.Type == PaymentType.Debt)
            RecomputeDebtAmount();
        else
            RecomputeCollectedAmount();
        UpdatedAt = DateTime.Now;
        return true;
    }

    public Payment RecordPayment(Money amount, string? remark, DateTime? tradeDate = null)
    {
        var payment = new Payment(Id, tradeDate ?? DateTime.Now, PaymentType.Collection, amount, remark);
        Payments.Add(payment);
        CollectedAmount = CollectedAmount.Add(amount);
        UpdatedAt = DateTime.Now;
        return payment;
    }
}
