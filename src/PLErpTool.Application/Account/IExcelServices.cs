using PLErpTool.Domain.Account;

namespace PLErpTool.Application.Account;

public interface IExcelImportService
{
    Task<ImportFileResult> ImportExcelFileAsync(string filePath, IImportRecordStore store, CancellationToken ct = default);
}

public sealed record ConvertResult(
    int TotalWritten,
    int TotalSkipped,
    string BackupPath,
    List<string> Messages);

public interface IExcelConvertService
{
    Task<ConvertResult> ConvertAsync(
        string masterPath,
        IReadOnlyList<ImportRecord> records,
        Dictionary<(string CustomerShortName, string Salesman), (long CustomerId, long? SalesmanId)> customerSalesmanMap,
        CancellationToken ct = default);
}

public interface IExcelExportService
{
    Task<string> ExportFailedRecordsAsync(IReadOnlyList<ImportRecord> records, CancellationToken ct = default);
}