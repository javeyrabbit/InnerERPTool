using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Receivable.Events;

public interface DomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}

public sealed record PaymentRegisteredDomainEvent(
    long MonthlyDebtId,
    MonthKey Month,
    Money Amount) : DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.Now;
}

public sealed record DebtRecordedDomainEvent(
    long MonthlyDebtId,
    MonthKey Month,
    Money Amount) : DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.Now;
}

public sealed record DebtUpdatedDomainEvent(
    long MonthlyDebtId,
    Money OldAmount,
    Money NewAmount) : DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.Now;
}