namespace PLErpTool.Domain.Shared;

public readonly struct Money : IEquatable<Money>
{
    public decimal Value { get; }
    public string Currency { get; }

    public Money(decimal value, string currency = "CNY")
    {
        Value = value;
        Currency = currency;
    }

    public static Money Zero => new(0m);

    public bool IsNegative => Value < 0m;
    public bool IsZero => Value == 0m;

    public Money Add(Money other) => new(Value + other.Value, Currency);
    public Money Subtract(Money other) => new(Value - other.Value, Currency);
    public Money Multiply(decimal factor) => new(Value * factor, Currency);
    public Money Negate() => new(-Value, Currency);

    public bool Equals(Money other) => Value == other.Value && Currency == other.Currency;
    public override bool Equals(object? obj) => obj is Money m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(Value, Currency);
    public override string ToString() => Value.ToString("N2");

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    public static bool operator <(Money left, Money right) => left.Value < right.Value;
    public static bool operator >(Money left, Money right) => left.Value > right.Value;
    public static bool operator <=(Money left, Money right) => left.Value <= right.Value;
    public static bool operator >=(Money left, Money right) => left.Value >= right.Value;
}