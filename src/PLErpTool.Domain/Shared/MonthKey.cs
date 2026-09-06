namespace PLErpTool.Domain.Shared;

public readonly struct MonthKey : IEquatable<MonthKey>, IComparable<MonthKey>
{
    public int Year { get; }
    public int Month { get; }
    public string Value => $"{Year:D4}-{Month:D2}";

    public MonthKey(int year, int month)
    {
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month));
        Year = year;
        Month = month;
    }

    public static MonthKey FromDateTime(DateTime date) => new(date.Year, date.Month);

    public static MonthKey Parse(string text)
    {
        if (TryParse(text, out var key)) return key;
        throw new FormatException($"Invalid MonthKey: {text}");
    }

    public static bool TryParse(string? text, out MonthKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Trim().Split('-', '/');
        if (parts.Length >= 2 && int.TryParse(parts[0], out int y) && int.TryParse(parts[1], out int m))
        {
            key = new MonthKey(y, m);
            return true;
        }
        return false;
    }

    public MonthKey Previous() => Month == 1 ? new MonthKey(Year - 1, 12) : new MonthKey(Year, Month - 1);
    public MonthKey Next() => Month == 12 ? new MonthKey(Year + 1, 1) : new MonthKey(Year, Month + 1);
    public DateTime FirstDay => new(Year, Month, 1);

    public bool Equals(MonthKey other) => Year == other.Year && Month == other.Month;
    public int CompareTo(MonthKey other) => (Year, Month).CompareTo((other.Year, other.Month));
    public override bool Equals(object? obj) => obj is MonthKey k && Equals(k);
    public override int GetHashCode() => HashCode.Combine(Year, Month);
    public override string ToString() => Value;
    public static bool operator ==(MonthKey l, MonthKey r) => l.Equals(r);
    public static bool operator !=(MonthKey l, MonthKey r) => !l.Equals(r);
    public static bool operator <(MonthKey l, MonthKey r) => l.CompareTo(r) < 0;
    public static bool operator >(MonthKey l, MonthKey r) => l.CompareTo(r) > 0;
    public static bool operator <=(MonthKey l, MonthKey r) => l.CompareTo(r) <= 0;
    public static bool operator >=(MonthKey l, MonthKey r) => l.CompareTo(r) >= 0;
}