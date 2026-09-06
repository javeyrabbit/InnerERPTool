using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Account;

public class ImportRecord
{
    public long ImportRecordId { get; init; }
    public string CustomerShortName { get; init; }
    public DateTime TradeDateValue { get; init; }
    public string DocumentNumber { get; init; }
    public string ProductName { get; init; }
    public string Spec { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string CustomerMaterial { get; init; }
    public string Salesman { get; init; }
    public decimal ReceivableAmount { get; init; }
    public string PaymentDate { get; init; }
    public decimal PaymentAmount { get; init; }

    public MonthKey MonthKey => MonthKey.FromDateTime(TradeDateValue);

    public string UniqueKey => string.Join("|",
        CustomerShortName.Replace(" ", ""),
        TradeDateValue.ToString("yyyy/M/d"),
        DocumentNumber.Replace(" ", ""),
        ProductName.Replace(" ", ""),
        Spec.Replace(" ", ""),
        UnitPrice.ToString("0.################"),
        CustomerMaterial.Replace(" ", ""));

    public string LedgerDuplicateKey
    {
        get
        {
            decimal amount = ReceivableAmount == 0m ? Quantity * UnitPrice : ReceivableAmount;
            return string.Join("|",
                TradeDateValue.ToString("yyyy/M/d"),
                DocumentNumber.Replace(" ", ""),
                $"{ProductName}|{Spec}".Trim(),
                Quantity.ToString("0.################"),
                UnitPrice.ToString("0.################"),
                amount.ToString("0.################"));
        }
    }

    public Money Amount => ReceivableAmount == 0m
        ? new Money(Quantity * UnitPrice)
        : new Money(ReceivableAmount);

    public ImportRecord(
        string customerShortName,
        DateTime tradeDateValue,
        string documentNumber,
        string productName,
        string spec,
        decimal quantity,
        decimal unitPrice,
        string customerMaterial,
        string salesman,
        decimal receivableAmount = 0m,
        string paymentDate = "",
        decimal paymentAmount = 0m)
    {
        CustomerShortName = customerShortName ?? string.Empty;
        TradeDateValue = tradeDateValue;
        DocumentNumber = documentNumber ?? string.Empty;
        ProductName = productName ?? string.Empty;
        Spec = spec ?? string.Empty;
        Quantity = quantity;
        UnitPrice = unitPrice;
        CustomerMaterial = customerMaterial ?? string.Empty;
        Salesman = salesman ?? string.Empty;
        ReceivableAmount = receivableAmount;
        PaymentDate = paymentDate ?? string.Empty;
        PaymentAmount = paymentAmount;
    }
}