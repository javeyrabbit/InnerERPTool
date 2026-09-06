namespace PLErpTool.Domain.Shared;

public readonly struct DateRange
{
    public MonthKey Start { get; }
    public MonthKey End { get; }

    public DateRange(MonthKey start, MonthKey end)
    {
        Start = start;
        End = end;
    }

    public bool IsValid => Start <= End;
    public bool Contains(MonthKey month) => month >= Start && month <= End;
}