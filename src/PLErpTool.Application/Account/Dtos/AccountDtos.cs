namespace PLErpTool.Application.Account.Dtos;

public sealed record ImportResultDto(
    int AddedFiles,
    int AddedRecords,
    int ExtractedRecords,
    int DuplicateRecords,
    int FilteredZeroQuantity,
    int TotalFiles,
    int TotalRecords,
    List<string> Messages);

public sealed record ImportRecordPreviewDto(
    string CustomerShortName,
    string TradeDate,
    string DocumentNumber,
    string ProductName,
    string Spec,
    decimal Quantity,
    decimal UnitPrice,
    string CustomerMaterial,
    string Salesman);

public sealed record ConvertResultDto(
    int TotalWritten,
    int TotalSkipped,
    int SyncedCustomers,
    int SyncedSalesmen,
    string BackupPath,
    string MasterPath,
    List<string> Messages);