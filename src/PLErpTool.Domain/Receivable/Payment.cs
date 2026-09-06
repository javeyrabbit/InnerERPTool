using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Receivable;

public class Payment
{
    public long Id { get; init; }
    public long MonthlyDebtId { get; init; }
    public DateTime TradeDate { get; init; }
    public PaymentType Type { get; init; }
    public Money Amount { get; private set; }
    public string Remark { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; private set; }

    // 明细字段（欠款明细承载，收款记录留空）
    public string DocumentNo { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string SpecDetails { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string CustomerMaterial { get; private set; } = string.Empty;
    public string DebtType { get; private set; } = string.Empty;
    public string ReconciliationRemark { get; private set; } = string.Empty;
    public string ReceivableRemark { get; private set; } = string.Empty;

    private Payment() { }

    // 欠款明细：金额 = 数量 * 单价
    public Payment(long monthlyDebtId, DateTime tradeDate, PaymentType type,
        decimal quantity, decimal unitPrice, string? remark = null,
        string documentNo = "", string productName = "", string specDetails = "",
        string customerMaterial = "", string reconciliationRemark = "", string receivableRemark = "",
        string? debtType = null)
    {
        MonthlyDebtId = monthlyDebtId;
        TradeDate = tradeDate;
        Type = type;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DocumentNo = documentNo ?? string.Empty;
        ProductName = productName ?? string.Empty;
        SpecDetails = specDetails ?? string.Empty;
        CustomerMaterial = customerMaterial ?? string.Empty;
        DebtType = debtType ?? string.Empty;
        ReconciliationRemark = reconciliationRemark ?? string.Empty;
        ReceivableRemark = receivableRemark ?? string.Empty;
        Remark = remark ?? string.Empty;
        Amount = type == PaymentType.Debt
            ? new Money(quantity * unitPrice)
            : new Money(quantity > 0m ? quantity : 0m);
        if (Amount.Value <= 0m)
            throw new ArgumentException("金额必须大于 0", nameof(quantity));
    }

    // 收款记录：只有日期/类型/金额/备注
    public Payment(long monthlyDebtId, DateTime tradeDate, PaymentType type, Money amount, string? remark = null,
        string? debtType = null, string? reconciliationRemark = null)
    {
        if (amount.Value <= 0m)
            throw new ArgumentException("金额必须大于 0", nameof(amount));
        MonthlyDebtId = monthlyDebtId;
        TradeDate = tradeDate;
        Type = type;
        Amount = amount;
        Remark = remark ?? string.Empty;
        DebtType = debtType ?? string.Empty;
        ReconciliationRemark = reconciliationRemark ?? string.Empty;
    }

    // 编辑明细：数量/单价变更后重算金额
    public void EditLineItem(decimal quantity, decimal unitPrice)
    {
        if (Type != PaymentType.Debt) return;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Amount = new Money(quantity * unitPrice);
        UpdatedAt = DateTime.Now;
    }

    public void EditRemarks(string reconciliationRemark, string receivableRemark)
    {
        ReconciliationRemark = reconciliationRemark ?? string.Empty;
        ReceivableRemark = receivableRemark ?? string.Empty;
        UpdatedAt = DateTime.Now;
    }

    // 修改收款金额
    public void EditAmount(decimal newAmount)
    {
        if (newAmount <= 0m)
            throw new ArgumentException("金额必须大于 0", nameof(newAmount));
        Amount = new Money(newAmount);
        UpdatedAt = DateTime.Now;
    }

    // 软删除：仅置标记，不从集合移除
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.Now;
    }
}
