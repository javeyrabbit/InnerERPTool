using System.IO;
using NPOI.SS.UserModel;
using PLErpTool.Domain.Account;
using PLErpTool.Application.Account;

namespace PLErpTool.Infrastructure.Excel;

public sealed class ExcelImportService : IExcelImportService
{
    public async Task<ImportFileResult> ImportExcelFileAsync(string filePath, IImportRecordStore store, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            IWorkbook workbook = WorkbookFactory.Create(stream);
            ISheet sheet = workbook.GetSheetAt(0);

            int headerRowIndex = FindHeaderRowIndex(sheet);
            var headerMap = BuildHeaderMap(sheet.GetRow(headerRowIndex));

            int extracted = 0, added = 0, duplicate = 0, zeroQty = 0;

            for (int r = headerRowIndex + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null || IsEmptyRow(row)) continue;

                if (IsSummaryRow(row, headerMap)) continue;

                if (TryCreateRecord(row, headerMap, out var record, out bool isZero) && record is not null)
                {
                    if (isZero) { zeroQty++; continue; }
                    extracted++;
                    if (!store.HasDuplicate(record))
                    {
                        store.AddImportRecords(new[] { record });
                        added++;
                    }
                    else duplicate++;
                }
            }

            return new ImportFileResult(extracted, added, duplicate, zeroQty);
        }, ct);
    }

    private static string[] RequiredHeaders =
        { "客户简称", "交易日", "单据号码", "品名", "头+牙+锁+管长+大管+长心", "数量", "单价", "客户物料", "业务员" };

    private static int FindHeaderRowIndex(ISheet sheet)
    {
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            var map = BuildHeaderMap(row);
            if (RequiredHeaders.All(h => map.ContainsKey(h))) return r;
        }
        throw new InvalidOperationException("未匹配到必需表头，请检查 Excel 模板。");
    }

    private static Dictionary<string, int> BuildHeaderMap(IRow row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = row.FirstCellNum; c < row.LastCellNum; c++)
        {
            if (c < 0) continue;
            var text = NormalizeHeader(GetCellText(row.GetCell(c)));
            text = NormalizeHeaderAlias(text);
            if (!string.IsNullOrWhiteSpace(text) && !map.ContainsKey(text))
                map[text] = c;
        }
        return map;
    }

    private static string NormalizeHeader(string h) => h.Replace(" ", "").Replace("\r", "").Replace("\n", "").Trim();

    private static string NormalizeHeaderAlias(string h) => h switch
    {
        "销售日期" => "交易日",
        "客户名称" => "客户简称",
        "单号" => "单据号码",
        _ => h
    };

    private static bool TryCreateRecord(IRow row, IReadOnlyDictionary<string, int> map, out ImportRecord? record, out bool isZero)
    {
        record = null;
        isZero = false;
        string cust = GetCellText(row.GetCell(map["客户简称"])).Trim();
        string dateText = GetCellText(row.GetCell(map["交易日"])).Trim();
        string docNo = GetCellText(row.GetCell(map["单据号码"])).Trim();
        string productName = GetCellText(row.GetCell(map["品名"])).Trim();
        string spec = GetCellText(row.GetCell(map["头+牙+锁+管长+大管+长心"])).Trim();
        string qtyText = GetCellText(row.GetCell(map["数量"])).Trim();
        string priceText = GetCellText(row.GetCell(map["单价"])).Trim();
        string material = GetCellText(row.GetCell(map["客户物料"])).Trim();
        string salesman = GetCellText(row.GetCell(map["业务员"])).Trim();

        if (string.IsNullOrWhiteSpace(cust) && string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(docNo))
            return false;

        if (!TryParseDate(dateText, out var date)) return false;

        decimal qty = ParseDecimal(qtyText);
        if (qty == 0m) { isZero = true; return false; }

        decimal price = ParseDecimal(priceText);
        decimal receivable = map.ContainsKey("应收金额") ? ParseDecimal(GetCellText(row.GetCell(map["应收金额"]))) : 0m;
        string payDate = map.ContainsKey("收款日期") ? GetCellText(row.GetCell(map["收款日期"])).Trim() : "";
        decimal payAmount = map.ContainsKey("收款金额") ? ParseDecimal(GetCellText(row.GetCell(map["收款金额"]))) : 0m;

        record = new ImportRecord(cust, date, docNo, productName, spec, qty, price, material, salesman, receivable, payDate, payAmount);
        return true;
    }

    private static bool IsSummaryRow(IRow row, IReadOnlyDictionary<string, int> map)
    {
        string cust = GetCellText(row.GetCell(map["客户简称"])).Trim();
        string docNo = GetCellText(row.GetCell(map["单据号码"])).Trim();
        string product = GetCellText(row.GetCell(map["品名"])).Trim();
        return cust.Contains("合计") || docNo.Contains("合计") || product.Contains("合计")
            || cust.Contains("月合计") || docNo.Contains("月合计");
    }

    private static bool IsEmptyRow(IRow row)
    {
        for (int c = row.FirstCellNum; c < row.LastCellNum; c++)
            if (c >= 0 && !string.IsNullOrWhiteSpace(GetCellText(row.GetCell(c)))) return false;
        return true;
    }

    private static bool TryParseDate(string text, out DateTime date)
    {
        string[] formats = { "yyyy/M/d", "yyyy/M/dd", "yyyy/MM/d", "yyyy/MM/dd", "yyyy-M-d", "yyyy-MM-dd" };
        return DateTime.TryParseExact(text.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date)
            || DateTime.TryParse(text, out date);
    }

    private static decimal ParseDecimal(string text)
    {
        string cleaned = text.Replace(",", "").Replace("￥", "").Replace("$", "").Trim();
        return decimal.TryParse(cleaned, out decimal v) ? v : 0m;
    }

    private static string GetCellText(ICell? cell)
    {
        if (cell is null) return "";
        return cell.CellType switch
        {
            CellType.Numeric when DateUtil.IsCellDateFormatted(cell) =>
                DateUtil.GetJavaDate(cell.NumericCellValue).ToString("yyyy/M/d"),
            CellType.Numeric => cell.NumericCellValue.ToString("0.################"),
            CellType.Boolean => cell.BooleanCellValue ? "True" : "False",
            CellType.Formula => cell.CachedFormulaResultType switch
            {
                CellType.Numeric => cell.NumericCellValue.ToString("0.################"),
                CellType.String => cell.StringCellValue,
                _ => cell.ToString() ?? ""
            },
            _ => cell.ToString()?.Trim() ?? ""
        };
    }
}
