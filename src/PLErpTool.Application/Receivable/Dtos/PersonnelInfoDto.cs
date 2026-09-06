namespace PLErpTool.Application.Receivable.Dtos;

public sealed record PersonnelInfoDto(
    long CustomerId,
    string CustomerShortName,
    string CustomerFullName,
    DateTime CustomerCreatedAt,
    long? SalesmanId,
    string? SalesmanName,
    DateTime? SalesmanCreatedAt);
