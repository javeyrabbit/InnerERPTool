using System.IO;
using System.Text.Json;

namespace PLErpTool.Services;

public sealed class SearchHistoryStore
{
    private const int MaxEntriesPerCategory = 20;
    private readonly object _sync = new();
    private readonly string _filePath;
    private Dictionary<string, List<string>> _histories;

    public SearchHistoryStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PLErpTool");
        _filePath = Path.Combine(directory, "search-history.json");
        _histories = Load();
    }

    public IReadOnlyList<string> Get(string category)
    {
        lock (_sync)
            return _histories.TryGetValue(category, out var values)
                ? values.ToList()
                : Array.Empty<string>();
    }

    public void Remember(string category, string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return;

        lock (_sync)
        {
            if (!_histories.TryGetValue(category, out var values))
            {
                values = new List<string>();
                _histories[category] = values;
            }

            values.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
            values.Insert(0, normalized);
            if (values.Count > MaxEntriesPerCategory)
                values.RemoveRange(MaxEntriesPerCategory, values.Count - MaxEntriesPerCategory);
            Save();
        }
    }

    private Dictionary<string, List<string>> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(_filePath))
                ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_histories));
        }
        catch
        {
            // 搜索历史写入失败不影响正常查询。
        }
    }
}
