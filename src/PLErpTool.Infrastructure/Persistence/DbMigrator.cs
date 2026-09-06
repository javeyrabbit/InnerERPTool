using Microsoft.EntityFrameworkCore;
using PLErpTool.Infrastructure.Persistence;

namespace PLErpTool.Infrastructure.Persistence;

public static class DbMigrator
{
    public static void EnsureMigrated(DbContextOptions<PlErpDbContext> options)
    {
        using var db = new PlErpDbContext(options);
        db.Database.EnsureCreated();
        EnsurePaymentColumns(db);
        EnsurePaymentChangeLogsTable(db);
        CorrectLegacyReceivableData(db);
    }

    // EnsureCreated 不会给已存在的表补列，这里手动补齐 Payment 明细列（旧库升级用）
    private static void EnsurePaymentColumns(PlErpDbContext db)
    {
        var columns = new Dictionary<string, string>
        {
            ["DocumentNo"] = "TEXT NOT NULL DEFAULT ''",
            ["ProductName"] = "TEXT NOT NULL DEFAULT ''",
            ["SpecDetails"] = "TEXT NOT NULL DEFAULT ''",
            ["Quantity"] = "REAL NOT NULL DEFAULT 0",
            ["UnitPrice"] = "REAL NOT NULL DEFAULT 0",
            ["CustomerMaterial"] = "TEXT NOT NULL DEFAULT ''",
            ["DebtType"] = "TEXT NOT NULL DEFAULT ''",
            ["ReconciliationRemark"] = "TEXT NOT NULL DEFAULT ''",
            ["ReceivableRemark"] = "TEXT NOT NULL DEFAULT ''",
            ["IsDeleted"] = "INTEGER NOT NULL DEFAULT 0"
        };

        var existingColumns = GetTableColumns(db, "Payments");
        foreach (var (column, definition) in columns)
        {
            if (!existingColumns.Contains(column))
#pragma warning disable EF1002 // column names and definitions are from the fixed map above, never user input
                db.Database.ExecuteSqlRaw($"ALTER TABLE Payments ADD COLUMN \"{column}\" {definition}");
#pragma warning restore EF1002
        }
    }

    private static HashSet<string> GetTableColumns(PlErpDbContext db, string tableName)
    {
        db.Database.OpenConnection();
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            using var reader = command.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
                columns.Add(reader.GetString(1));
            return columns;
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    // EnsureCreated 不会给旧库建新表，手动建 PaymentChangeLogs 表
    private static void EnsurePaymentChangeLogsTable(PlErpDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""PaymentChangeLogs"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PaymentChangeLogs"" PRIMARY KEY AUTOINCREMENT,
    ""PaymentId"" INTEGER NOT NULL,
    ""MonthlyDebtId"" INTEGER NOT NULL,
    ""CustomerName"" TEXT NOT NULL DEFAULT '',
    ""Month"" TEXT NOT NULL DEFAULT '',
    ""DocumentNo"" TEXT NOT NULL DEFAULT '',
    ""PaymentType"" TEXT NOT NULL DEFAULT '',
    ""Action"" TEXT NOT NULL DEFAULT '',
    ""Field"" TEXT NOT NULL DEFAULT '',
    ""OldValue"" TEXT NOT NULL DEFAULT '',
    ""NewValue"" TEXT NOT NULL DEFAULT '',
    ""OldAmount"" REAL NOT NULL DEFAULT 0,
    ""NewAmount"" REAL NOT NULL DEFAULT 0,
    ""Operator"" TEXT NULL,
    ""ChangedAt"" TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ""IX_PaymentChangeLogs_Payment"" ON ""PaymentChangeLogs"" (""PaymentId"");
CREATE INDEX IF NOT EXISTS ""IX_PaymentChangeLogs_MonthlyDebt"" ON ""PaymentChangeLogs"" (""MonthlyDebtId"");
");
    }

    // 修正早期导入数据：缺业务员的月份欠款统一归属到该客户的“默认业务员”，
    // 欠款明细未填写类型时按“其他欠款”处理。此方法可重复执行。
    private static void CorrectLegacyReceivableData(PlErpDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
INSERT INTO ""Salesmen"" (""CustomerId"", ""Name"", ""CreatedAt"", ""UpdatedAt"")
SELECT DISTINCT debt.""CustomerId"", '默认业务员', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM ""MonthlyDebts"" AS debt
WHERE debt.""SalesmanId"" IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM ""Salesmen"" AS salesman
      WHERE salesman.""CustomerId"" = debt.""CustomerId""
        AND salesman.""Name"" = '默认业务员'
  );

-- 若同客户、同月份已存在“默认业务员”记录，先把明细转入该记录。
UPDATE ""Payments""
SET ""MonthlyDebtId"" = (
    SELECT target.""Id""
    FROM ""MonthlyDebts"" AS source
    INNER JOIN ""Salesmen"" AS defaultSalesman
        ON defaultSalesman.""CustomerId"" = source.""CustomerId""
       AND defaultSalesman.""Name"" = '默认业务员'
    INNER JOIN ""MonthlyDebts"" AS target
        ON target.""CustomerId"" = source.""CustomerId""
       AND target.""SalesmanId"" = defaultSalesman.""Id""
       AND target.""Month"" = source.""Month""
    WHERE source.""Id"" = ""Payments"".""MonthlyDebtId""
)
WHERE ""MonthlyDebtId"" IN (
    SELECT source.""Id""
    FROM ""MonthlyDebts"" AS source
    INNER JOIN ""Salesmen"" AS defaultSalesman
        ON defaultSalesman.""CustomerId"" = source.""CustomerId""
       AND defaultSalesman.""Name"" = '默认业务员'
    INNER JOIN ""MonthlyDebts"" AS target
        ON target.""CustomerId"" = source.""CustomerId""
       AND target.""SalesmanId"" = defaultSalesman.""Id""
       AND target.""Month"" = source.""Month""
    WHERE source.""SalesmanId"" IS NULL
);

-- 合并后重新计算目标月份汇总，随后删除已合并的空业务员记录。
UPDATE ""MonthlyDebts""
SET ""DebtAmount"" = COALESCE((
        SELECT SUM(payment.""Amount"")
        FROM ""Payments"" AS payment
        WHERE payment.""MonthlyDebtId"" = ""MonthlyDebts"".""Id""
          AND payment.""Type"" = 'Debt'
          AND payment.""IsDeleted"" = 0
    ), 0),
    ""CollectedAmount"" = COALESCE((
        SELECT SUM(payment.""Amount"")
        FROM ""Payments"" AS payment
        WHERE payment.""MonthlyDebtId"" = ""MonthlyDebts"".""Id""
          AND payment.""Type"" = 'Collection'
          AND payment.""IsDeleted"" = 0
    ), 0),
    ""UpdatedAt"" = CURRENT_TIMESTAMP
WHERE ""Id"" IN (
    SELECT target.""Id""
    FROM ""MonthlyDebts"" AS source
    INNER JOIN ""Salesmen"" AS defaultSalesman
        ON defaultSalesman.""CustomerId"" = source.""CustomerId""
       AND defaultSalesman.""Name"" = '默认业务员'
    INNER JOIN ""MonthlyDebts"" AS target
        ON target.""CustomerId"" = source.""CustomerId""
       AND target.""SalesmanId"" = defaultSalesman.""Id""
       AND target.""Month"" = source.""Month""
    WHERE source.""SalesmanId"" IS NULL
);

DELETE FROM ""MonthlyDebts""
WHERE ""SalesmanId"" IS NULL
  AND EXISTS (
      SELECT 1
      FROM ""Salesmen"" AS defaultSalesman
      INNER JOIN ""MonthlyDebts"" AS target
          ON target.""CustomerId"" = ""MonthlyDebts"".""CustomerId""
         AND target.""SalesmanId"" = defaultSalesman.""Id""
         AND target.""Month"" = ""MonthlyDebts"".""Month""
      WHERE defaultSalesman.""CustomerId"" = ""MonthlyDebts"".""CustomerId""
        AND defaultSalesman.""Name"" = '默认业务员'
  );

UPDATE ""MonthlyDebts""
SET ""SalesmanId"" = (
        SELECT salesman.""Id""
        FROM ""Salesmen"" AS salesman
        WHERE salesman.""CustomerId"" = ""MonthlyDebts"".""CustomerId""
          AND salesman.""Name"" = '默认业务员'
        LIMIT 1
    ),
    ""UpdatedAt"" = CURRENT_TIMESTAMP
WHERE ""SalesmanId"" IS NULL;

UPDATE ""Payments""
SET ""DebtType"" = '其他欠款',
    ""UpdatedAt"" = CURRENT_TIMESTAMP
WHERE ""Type"" = 'Debt'
  AND (""DebtType"" IS NULL OR TRIM(""DebtType"") = '');
");
    }
}
