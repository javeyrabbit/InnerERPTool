using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PLErpTool.Domain.Account;
using PLErpTool.Application.Account;

namespace PLErpTool.Infrastructure.Excel;

public sealed class ExcelExportService : IExcelExportService
{
    public async Task<string> ExportFailedRecordsAsync(IReadOnlyList<ImportRecord> records, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            string path = Path.Combine(AppContext.BaseDirectory, $"loser_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("失败数据");

            string[] headers = { "客户简称", "交易日", "单据号码", "品名", "头+牙+锁+管长+大管+长心",
                "数量", "单价", "应收金额", "收款日期", "收款金额", "客户物料", "业务员" };
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
                headerRow.CreateCell(i).SetCellValue(headers[i]);

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0).SetCellValue(rec.CustomerShortName);
                row.CreateCell(1).SetCellValue(rec.TradeDateValue.ToString("yyyy/M/d"));
                row.CreateCell(2).SetCellValue(rec.DocumentNumber);
                row.CreateCell(3).SetCellValue(rec.ProductName);
                row.CreateCell(4).SetCellValue(rec.Spec);
                row.CreateCell(5).SetCellValue((double)rec.Quantity);
                row.CreateCell(6).SetCellValue((double)rec.UnitPrice);
                row.CreateCell(7).SetCellValue((double)rec.ReceivableAmount);
                row.CreateCell(8).SetCellValue(rec.PaymentDate);
                row.CreateCell(9).SetCellValue((double)rec.PaymentAmount);
                row.CreateCell(10).SetCellValue(rec.CustomerMaterial);
                row.CreateCell(11).SetCellValue(rec.Salesman);
            }

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(stream, leaveOpen: false);
            return path;
        }, ct);
    }
}