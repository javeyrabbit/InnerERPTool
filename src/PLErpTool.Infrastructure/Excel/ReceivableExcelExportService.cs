using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PLErpTool.Application.Receivable.Dtos;

namespace PLErpTool.Infrastructure.Excel;

public sealed class ReceivableExcelExportService
{
    public Task<string> ExportAsync(IReadOnlyList<MonthlyDebtDto> items, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            string path = Path.Combine(AppContext.BaseDirectory, $"应收明细_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("应收明细");

            string[] headers = { "公司", "业务员", "月份", "欠款金额 (元)", "已收金额 (元)", "差额 (元)" };
            // 中文表头与内容宽度预估（单位 1/256 字符宽）
            int[] colWidths = { 18 * 256, 12 * 256, 12 * 256, 16 * 256, 16 * 256, 16 * 256 };
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
                sheet.SetColumnWidth(i, colWidths[i]);
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0).SetCellValue(item.CustomerName);
                row.CreateCell(1).SetCellValue(item.SalesmanName);
                row.CreateCell(2).SetCellValue(item.Month);
                row.CreateCell(3).SetCellValue((double)item.DebtAmount);
                row.CreateCell(4).SetCellValue((double)item.CollectedAmount);
                row.CreateCell(5).SetCellValue((double)item.Balance);
            }

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(stream, leaveOpen: false);
            return path;
        }, ct);
    }
}
