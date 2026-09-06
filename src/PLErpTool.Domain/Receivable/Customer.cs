namespace PLErpTool.Domain.Receivable;

public class Customer
{
    public long Id { get; init; }
    public string ShortName { get; init; }
    public string FullName { get; set; }
    public List<Salesman> Salesmen { get; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Customer(string shortName, string? fullName = null)
    {
        ShortName = string.IsNullOrWhiteSpace(shortName)
            ? throw new ArgumentException("客户简称不能为空", nameof(shortName))
            : shortName;
        FullName = fullName ?? shortName;
    }

    public Salesman AddSalesman(string name)
    {
        if (Salesmen.Any(s => s.Name == name))
            return Salesmen.First(s => s.Name == name);
        var salesman = new Salesman(Id, name);
        Salesmen.Add(salesman);
        return salesman;
    }
}