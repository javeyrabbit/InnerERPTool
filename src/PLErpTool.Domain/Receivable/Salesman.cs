namespace PLErpTool.Domain.Receivable;

public class Salesman
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public string Name { get; init; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Salesman(long customerId, string name)
    {
        CustomerId = customerId;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("业务员姓名不能为空", nameof(name))
            : name;
    }
}