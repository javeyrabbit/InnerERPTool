using PLErpTool.Domain.Account;

namespace PLErpTool.Infrastructure.Import;

public sealed class InMemoryImportRecordStore : IImportRecordStore
{
    private readonly object _sync = new();
    private readonly List<ImportRecord> _records = new();
    private readonly HashSet<string> _uniqueKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _fileNames = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ImportRecord> GetImportRecords()
    {
        lock (_sync)
            return _records.ToList();
    }

    public IReadOnlyList<string> FileNames
    {
        get
        {
            lock (_sync)
                return _fileNames.ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _records.Count;
        }
    }

    public void AddImportRecords(IEnumerable<ImportRecord> records)
    {
        lock (_sync)
        {
            foreach (var record in records)
            {
                if (record is null) continue;
                if (_uniqueKeys.Add(record.UniqueKey))
                    _records.Add(record);
            }
        }
    }

    public void ClearImportRecords()
    {
        lock (_sync)
        {
            _records.Clear();
            _uniqueKeys.Clear();
            _fileNames.Clear();
        }
    }

    public bool HasDuplicate(ImportRecord record)
    {
        lock (_sync)
            return record is not null && _uniqueKeys.Contains(record.UniqueKey);
    }

    public bool HasDuplicateFileName(string fileName)
    {
        lock (_sync)
            return _fileNames.Contains(fileName);
    }

    public void AddFileName(string fileName)
    {
        lock (_sync)
            _fileNames.Add(fileName);
    }
}
