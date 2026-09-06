using PLErpTool.Domain.Account;

namespace PLErpTool.Domain.Account;

public interface IImportRecordStore
{
    IReadOnlyList<ImportRecord> GetImportRecords();
    void AddImportRecords(IEnumerable<ImportRecord> records);
    void ClearImportRecords();
    bool HasDuplicate(ImportRecord record);
    bool HasDuplicateFileName(string fileName);
    void AddFileName(string fileName);
    int Count { get; }
    IReadOnlyList<string> FileNames { get; }
}