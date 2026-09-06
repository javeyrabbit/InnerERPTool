using Microsoft.Data.Sqlite;

string dbPath = @"D:\LearningSoftWare\C#\PLErpTool\src\PLErpTool\bin\Debug\net8.0-windows\plerp.db";
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

Console.WriteLine("===== 所有 MonthlyDebts (含 CustomerId/SalesmanId/Month) =====");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT d.Id, d.CustomerId, d.SalesmanId, d.Month, d.DebtAmount, d.CollectedAmount FROM MonthlyDebts d ORDER BY d.Month";
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        long custId = r.GetInt64(1);
        long? spId = r.IsDBNull(2) ? (long?)null : r.GetInt64(2);
        string month = r.GetString(3);
        decimal debt = r.GetDecimal(4);
        decimal col = r.GetDecimal(5);
        Console.WriteLine($"  Debt.Id={r.GetInt64(0)} CustId={custId} SalesmanId={spId} Month={month} Debt={debt} Collected={col}");
    }
}

Console.WriteLine("\n===== Customers =====");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT Id, ShortName FROM Customers ORDER BY Id";
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"  Cust Id={r.GetInt64(0)} Short='{r.GetString(1)}'");
}

Console.WriteLine("\n===== Salesmen =====");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT Id, CustomerId, Name FROM Salesmen ORDER BY CustomerId, Id";
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"  Sp Id={r.GetInt64(0)} CustId={r.GetInt64(1)} Name='{r.GetString(2)}'");
}

Console.WriteLine("\n===== 含2月份的欠款 (Month LIKE '2026-02%' 或 Month LIKE '%02%') =====");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT d.Id, d.CustomerId, c.ShortName, d.Month, d.DebtAmount FROM MonthlyDebts d LEFT JOIN Customers c ON d.CustomerId=c.Id WHERE d.Month LIKE '%-02%' OR d.Month LIKE '2026-02%'";
    using var r = cmd.ExecuteReader();
    bool any = false;
    while (r.Read()) { any = true; Console.WriteLine($"  Debt.Id={r.GetInt64(0)} CustId={r.GetInt64(1)} Cust='{r.GetString(2)}' Month={r.GetString(3)} Debt={r.GetDecimal(4)}"); }
    if (!any) Console.WriteLine("  无2月份数据");
}