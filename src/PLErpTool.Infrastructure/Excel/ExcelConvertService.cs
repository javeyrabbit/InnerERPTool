using System.IO;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using PLErpTool.Domain.Account;
using PLErpTool.Domain.Shared;
using PLErpTool.Application.Account;

namespace PLErpTool.Infrastructure.Excel;

public sealed class ExcelConvertService : IExcelConvertService
{
    private const string TemplateSheetName = "模板";

    // 模板中关键样板行的行号
    private const int TplHeaderRow = 3;     // 月份表头：日期|送货单号|...
    private const int TplDataRow = 4;       // 数据行样板
    private const int TplSubtotalRow = 13;  // 小计样板
    private const int TplTotalRow = 35;     // 合计样板
    private const int TplReceivableRow = 36;// 应付账款样板

    private static readonly CellRangeAddress[] HeaderMergedRegions =
    {
        new(0, 0, 0, 7),
        new(2, 2, 4, 5),
        new(2, 2, 6, 7),
    };

    // 缓存模板各样板行每个单元格的样式，避免重复访问
    private sealed class TemplateStyleCache
    {
        private readonly ICellStyle?[][] _rows = new ICellStyle?[5][];
        public TemplateStyleCache(ISheet template, int maxCol)
        {
            int[] sampleRows = { TplHeaderRow, TplDataRow, TplSubtotalRow, TplTotalRow, TplReceivableRow };
            for (int i = 0; i < sampleRows.Length; i++)
            {
                var tRow = template.GetRow(sampleRows[i]);
                _rows[i] = new ICellStyle?[maxCol];
                if (tRow is null) continue;
                for (int c = 0; c < maxCol; c++)
                {
                    var cell = tRow.GetCell(c);
                    _rows[i][c] = cell?.CellStyle;
                }
            }
        }

        public ICellStyle? Header(int c) => _rows[0][c];
        public ICellStyle? Data(int c) => _rows[1][c];
        public ICellStyle? Subtotal(int c) => _rows[2][c];
        public ICellStyle? Total(int c) => _rows[3][c];
        public ICellStyle? Receivable(int c) => _rows[4][c];
    }

    public async Task<ConvertResult> ConvertAsync(
        string masterPath,
        IReadOnlyList<ImportRecord> records,
        Dictionary<(string, string), (long, long?)> customerSalesmanMap,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var readStream = File.Open(masterPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            IWorkbook workbook = WorkbookFactory.Create(readStream);
            var templateSheet = workbook.GetSheet(TemplateSheetName)
                ?? throw new InvalidOperationException($"未找到模板 Sheet：{TemplateSheetName}");

            // 预先缓存模板各样板行(表头/数据/小计/合计/应付账款)的单元格样式
            int maxCol = 16;
            var styleCache = new TemplateStyleCache(templateSheet, maxCol);

            var messages = new List<string>();
            int totalWritten = 0, totalSkipped = 0;

            foreach (var group in records.GroupBy(r => (r.CustomerShortName, r.Salesman)))
            {
                var firstRecord = group.First();
                var sheet = FindSheetByCustomerAndSalesman(workbook, firstRecord.CustomerShortName, firstRecord.Salesman);
                if (sheet is null)
                {
                    var sheetName = GenerateSheetName(workbook, firstRecord.CustomerShortName, firstRecord.Salesman);
                    sheet = workbook.CreateSheet(sheetName);
                    CopyTemplateHeader(templateSheet, sheet, firstRecord.CustomerShortName, firstRecord.Salesman);
                    messages.Add($"[新建] 已按模板创建 Sheet：{sheet.SheetName}");
                }

                var existingKeys = LoadExistingSheetKeys(sheet);
                var monthGroups = group.OrderBy(x => x.TradeDateValue).GroupBy(x => x.MonthKey);

                int sheetWritten = 0, sheetSkipped = 0;
                foreach (var monthGroup in monthGroups)
                {
                    var recordsToInsert = new List<ImportRecord>();
                    foreach (var rec in monthGroup)
                    {
                        if (!existingKeys.Add(rec.LedgerDuplicateKey))
                        {
                            sheetSkipped++; totalSkipped++;
                            continue;
                        }
                        recordsToInsert.Add(rec);
                    }

                    if (recordsToInsert.Count == 0) continue;

                    var section = FindMonthSection(sheet, monthGroup.Key);
                    if (section is null)
                        section = CreateMonthSection(sheet, styleCache, monthGroup.Key);

                    InsertRecords(sheet, section.Value, recordsToInsert, styleCache);
                    sheetWritten += recordsToInsert.Count;
                    totalWritten += recordsToInsert.Count;
                }

                if (sheetWritten > 0)
                {
                    EnsureSummaryRows(sheet, styleCache);
                    UpdateSheetSummary(sheet);
                }

                messages.Add($"[主账] {sheet.SheetName} 新增 {sheetWritten} 条，跳过重复 {sheetSkipped} 条。");
            }

            workbook.GetCreationHelper().CreateFormulaEvaluator().EvaluateAll();
            for (int i = 0; i < workbook.NumberOfSheets; i++)
                workbook.GetSheetAt(i).ForceFormulaRecalculation = true;

            var backupPath = BackupFile(masterPath);
            using var writeStream = new FileStream(masterPath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(writeStream, leaveOpen: false);

            messages.Add($"[转换完成] 共写入 {totalWritten} 条，跳过重复 {totalSkipped} 条。");
            messages.Add($"[备份文件] {backupPath}");
            messages.Add($"[已更新原文件] {masterPath}");

            return new ConvertResult(totalWritten, totalSkipped, backupPath, messages);
        }, ct);
    }

    private static string GenerateSheetName(IWorkbook wb, string customer, string salesman)
    {
        string custPart = Sanitize(customer), spPart = Sanitize(salesman);
        for (int i = 1; i <= 999; i++)
        {
            string name = $"{i}#{custPart}#{spPart}";
            if (name.Length > 31) name = name[..31];
            if (wb.GetSheet(name) is null) return name;
        }
        throw new InvalidOperationException("无法生成新的 Sheet 名称。");
    }

    private static string Sanitize(string s)
    {
        foreach (char c in ":\\/?*[]") s = s.Replace(c.ToString(), "");
        return s.Trim();
    }

    private static ISheet? FindSheetByCustomerAndSalesman(IWorkbook wb, string customer, string salesman)
    {
        string cust = customer.Replace(" ", "").Trim();
        string sp = salesman.Replace(" ", "").Trim();
        for (int i = 0; i < wb.NumberOfSheets; i++)
        {
            var sheet = wb.GetSheetAt(i);
            var parts = sheet.SheetName.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool custMatch = parts.Any(p => p == cust) || sheet.SheetName.Replace(" ", "").Contains(cust);
            bool spMatch = string.IsNullOrWhiteSpace(sp) || parts.Any(p => p == sp) || sheet.SheetName.Replace(" ", "").Contains(sp);
            if (custMatch && spMatch) return sheet;
        }
        return null;
    }

    private static void CopyTemplateHeader(ISheet template, ISheet target, string customer, string salesman)
    {
        // 只复制标题区 R0~R2（公司名 / 空行 / To/业务员/时间），不复制 R3 月份表头，
        // 月份表头由 CreateMonthSection 按需创建，避免出现两排表头。
        for (int r = 0; r <= 2; r++)
        {
            var tRow = template.GetRow(r);
            if (tRow is null) continue;
            var newRow = target.CreateRow(r);
            for (int c = 0; c < tRow.LastCellNum; c++)
            {
                var tc = tRow.GetCell(c);
                if (tc is null) continue;
                var nc = newRow.CreateCell(c);
                nc.CellStyle = tc.CellStyle;
                if (tc.CellType == CellType.String) nc.SetCellValue(tc.StringCellValue);
            }
            for (int c = 0; c < tRow.LastCellNum; c++)
                target.SetColumnWidth(c, template.GetColumnWidth(c));
        }
        target.GetRow(2).GetCell(0)?.SetCellValue($"To:  {customer}");
        target.GetRow(2).GetCell(2)?.SetCellValue($"业务员:  {salesman}");
        target.GetRow(2).GetCell(4)?.SetCellValue($"时间:  {DateTime.Today:yyyy.M.d}");

        foreach (var region in HeaderMergedRegions)
        {
            if (!MergedRegionContains(target, region))
                target.AddMergedRegion(region);
        }
    }

    private static bool MergedRegionContains(ISheet sheet, CellRangeAddress region)
    {
        for (int i = 0; i < sheet.NumMergedRegions; i++)
        {
            var r = sheet.GetMergedRegion(i);
            if (r.FirstRow == region.FirstRow && r.LastRow == region.LastRow
                && r.FirstColumn == region.FirstColumn && r.LastColumn == region.LastColumn)
                return true;
        }
        return false;
    }

    private static HashSet<string> LoadExistingSheetKeys(ISheet sheet)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            string dateText = GetText(row.GetCell(0)).Trim();
            string docNo = GetText(row.GetCell(1)).Trim().Replace(" ", "");
            if (string.IsNullOrWhiteSpace(dateText) || string.IsNullOrWhiteSpace(docNo)) continue;
            if (dateText == "日期" || docNo == "送货单号") continue;
            if (dateText.Contains("小计") || dateText.Contains("合计") || dateText.Contains("应付")) continue;
            string qtyText = GetText(row.GetCell(4)).Trim();
            string priceText = GetText(row.GetCell(5)).Trim();
            string amountText = GetText(row.GetCell(6)).Trim();
            string productText = GetText(row.GetCell(2)).Trim();
            string specText = GetText(row.GetCell(3)).Trim();
            keys.Add($"{dateText}|{docNo}|{productText}|{specText}|{qtyText}|{priceText}|{amountText}");
        }
        return keys;
    }

    private static (int Header, int FirstData, int Subtotal)? FindMonthSection(ISheet sheet, MonthKey month)
    {
        int r = sheet.FirstRowNum;
        while (r <= sheet.LastRowNum)
        {
            var row = sheet.GetRow(r);
            if (row is null || !IsMonthHeader(row)) { r++; continue; }
            int header = r, firstData = r + 1, subtotal = -1;
            for (int d = firstData; d <= sheet.LastRowNum; d++)
            {
                var dRow = sheet.GetRow(d);
                if (dRow is null) continue;
                if (GetText(dRow.GetCell(0)).Trim().Contains("小计")) { subtotal = d; break; }
            }
            if (subtotal <= 0) { r++; continue; }
            for (int d = firstData; d < subtotal; d++)
            {
                var dRow = sheet.GetRow(d);
                if (dRow is null) continue;
                if (MonthKey.TryParse(GetText(dRow.GetCell(0)).Trim(), out var key) && key == month)
                    return (header, firstData, subtotal);
            }
            r = subtotal + 1;
        }
        return null;
    }

    private static bool IsMonthHeader(IRow row)
        => GetText(row.GetCell(0)).Trim() == "日期" && GetText(row.GetCell(1)).Trim() == "送货单号";

    private static int FindSummaryStartRow(ISheet sheet)
    {
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            string first = GetText(row.GetCell(0)).Trim();
            if (first.Contains("合计") || first.Contains("应付账款")) return r;
        }
        return -1;
    }

    private static (int Header, int FirstData, int Subtotal) CreateMonthSection(ISheet sheet, TemplateStyleCache styleCache, MonthKey month)
    {
        int summaryStart = FindSummaryStartRow(sheet);
        int start = summaryStart >= 0 ? summaryStart : sheet.LastRowNum + 1;

        if (sheet.LastRowNum >= start)
        {
            var movedRegions = new List<CellRangeAddress>();
            for (int i = sheet.NumMergedRegions - 1; i >= 0; i--)
            {
                var region = sheet.GetMergedRegion(i);
                if (region.FirstRow >= start)
                {
                    movedRegions.Add(region);
                    sheet.RemoveMergedRegion(i);
                }
            }
            sheet.ShiftRows(start, sheet.LastRowNum, 2, true, false);
            foreach (var region in movedRegions)
                sheet.AddMergedRegion(new CellRangeAddress(region.FirstRow + 2, region.LastRow + 2,
                    region.FirstColumn, region.LastColumn));
        }

        // 月份表头行：套用模板表头样式。需创建完整的 C0~C15 全部单元格，
        // 否则 C8~C15 合并区域内的 C9~C15 单元格不存在，会导致合并区域右边界
        // 及中间段上下边框缺失（只显示一部分边框）。
        var headerRow = sheet.CreateRow(start);
        for (int c = 0; c < 16; c++)
        {
            var cell = headerRow.CreateCell(c);
            cell.CellStyle = styleCache.Header(c) ?? cell.CellStyle;
        }
        string[] headers = { "日期", "送货单号", "名称及规格", "头+牙+锁+管长+大管+长心", "数量", "单价", "金额", "收款", "客户物料" };
        for (int c = 0; c < headers.Length; c++)
            headerRow.GetCell(c).SetCellValue(headers[c]);
        // 客户物料列合并 C8-C15
        if (!RowHasMerge(sheet, start, 8, 15))
            sheet.AddMergedRegion(new CellRangeAddress(start, start, 8, 15));

        // 小计行：套用模板小计样式
        int subtotalIdx = start + 1;
        var subtotalRow = sheet.CreateRow(subtotalIdx);
        for (int c = 0; c < 16; c++)
        {
            var cell = subtotalRow.CreateCell(c);
            cell.CellStyle = styleCache.Subtotal(c) ?? cell.CellStyle;
        }
        subtotalRow.GetCell(0).SetCellValue("小计");
        if (!RowHasMerge(sheet, subtotalIdx, 8, 15))
            sheet.AddMergedRegion(new CellRangeAddress(subtotalIdx, subtotalIdx, 8, 15));

        return (start, start + 1, subtotalIdx);
    }

    private static bool RowHasMerge(ISheet sheet, int row, int firstCol, int lastCol)
    {
        for (int i = 0; i < sheet.NumMergedRegions; i++)
        {
            var r = sheet.GetMergedRegion(i);
            if (r.FirstRow == row && r.LastRow == row && r.FirstColumn == firstCol && r.LastColumn == lastCol)
                return true;
        }
        return false;
    }

    private static void InsertRecords(ISheet sheet, (int Header, int FirstData, int Subtotal) section, List<ImportRecord> records, TemplateStyleCache styleCache)
    {
        int insert = section.Subtotal;
        if (records.Count > 0)
            sheet.ShiftRows(insert, sheet.LastRowNum, records.Count, true, false);

        for (int i = 0; i < records.Count; i++)
        {
            var row = sheet.CreateRow(insert + i);
            var rec = records[i];
            for (int c = 0; c < 16; c++)
            {
                var cell = row.CreateCell(c);
                cell.CellStyle = styleCache.Data(c) ?? cell.CellStyle;
            }
            row.GetCell(0).SetCellValue(rec.TradeDateValue.ToString("yyyy/M/d"));
            row.GetCell(1).SetCellValue(rec.DocumentNumber);
            row.GetCell(2).SetCellValue(rec.ProductName);
            row.GetCell(3).SetCellValue(rec.Spec);
            row.GetCell(4).SetCellValue((double)rec.Quantity);
            row.GetCell(5).SetCellValue((double)rec.UnitPrice);
            row.GetCell(6).SetCellValue((double)(rec.ReceivableAmount == 0m ? rec.Quantity * rec.UnitPrice : rec.ReceivableAmount));
            row.GetCell(7).SetCellValue(0.0);
            row.GetCell(8).SetCellValue(rec.CustomerMaterial);
            if (!RowHasMerge(sheet, insert + i, 8, 15))
                sheet.AddMergedRegion(new CellRangeAddress(insert + i, insert + i, 8, 15));
        }

        int subtotalRowIdx = insert + records.Count;
        var subRow = sheet.GetRow(subtotalRowIdx) ?? sheet.CreateRow(subtotalRowIdx);
        // 小计行套用模板小计样式
        for (int c = 0; c < 16; c++)
        {
            var cell = subRow.GetCell(c) ?? subRow.CreateCell(c);
            cell.CellStyle = styleCache.Subtotal(c) ?? cell.CellStyle;
        }
        subRow.GetCell(0).SetCellValue("小计");
        int firstData = section.FirstData;
        int lastData = subtotalRowIdx - 1;
        if (lastData >= firstData)
        {
            subRow.GetCell(4).SetCellFormula($"SUM(E{firstData + 1}:E{lastData + 1})");
            subRow.GetCell(6).SetCellFormula($"SUM(G{firstData + 1}:G{lastData + 1})");
            subRow.GetCell(7).SetCellFormula($"SUM(H{firstData + 1}:H{lastData + 1})");
        }
        if (!RowHasMerge(sheet, subtotalRowIdx, 8, 15))
            sheet.AddMergedRegion(new CellRangeAddress(subtotalRowIdx, subtotalRowIdx, 8, 15));
    }

    private static void EnsureSummaryRows(ISheet sheet, TemplateStyleCache styleCache)
    {
        int totalRow = FindLabelRow(sheet, "合计");
        if (totalRow >= 0)
        {
            // 合计行已存在，补齐样式
            ApplyRowStyle(sheet, totalRow, styleCache.Total, "合计");
            return;
        }

        int start = sheet.LastRowNum + 1;
        // 合计行
        sheet.CreateRow(start);
        ApplyRowStyle(sheet, start, styleCache.Total, "合计");

        // 应付账款行
        sheet.CreateRow(start + 1);
        ApplyRowStyle(sheet, start + 1, styleCache.Receivable, "应付账款");
    }

    private static void ApplyRowStyle(ISheet sheet, int rowIdx, Func<int, ICellStyle?> styleGetter, string label)
    {
        var row = sheet.GetRow(rowIdx);
        if (row is null) return;
        for (int c = 0; c < 16; c++)
        {
            var cell = row.GetCell(c) ?? row.CreateCell(c);
            cell.CellStyle = styleGetter(c) ?? cell.CellStyle;
        }
        if (!string.IsNullOrEmpty(label))
            row.GetCell(0).SetCellValue(label);
        if (!RowHasMerge(sheet, rowIdx, 8, 15))
            sheet.AddMergedRegion(new CellRangeAddress(rowIdx, rowIdx, 8, 15));
    }

    private static int FindLabelRow(ISheet sheet, string label)
    {
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            if (GetText(row.GetCell(0)).Trim().Contains(label)) return r;
        }
        return -1;
    }

    private static void UpdateSheetSummary(ISheet sheet)
    {
        int totalRowIdx = -1, receivableRowIdx = -1;
        var subtotalIdxs = new List<int>();
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            string first = GetText(row.GetCell(0)).Trim();
            if (first.Contains("小计")) subtotalIdxs.Add(r);
            else if (first.Contains("合计")) totalRowIdx = r;
            else if (first.Contains("应付账款") || first.Contains("应收账款")) receivableRowIdx = r;
        }

        if (totalRowIdx >= 0)
        {
            var tRow = sheet.GetRow(totalRowIdx) ?? sheet.CreateRow(totalRowIdx);
            if (subtotalIdxs.Count > 0)
            {
                string eCells = string.Join(",", subtotalIdxs.Select(i => $"E{i + 1}"));
                string gCells = string.Join(",", subtotalIdxs.Select(i => $"G{i + 1}"));
                string hCells = string.Join(",", subtotalIdxs.Select(i => $"H{i + 1}"));
                (tRow.GetCell(4) ?? tRow.CreateCell(4)).SetCellFormula($"SUM({eCells})");
                (tRow.GetCell(6) ?? tRow.CreateCell(6)).SetCellFormula($"SUM({gCells})");
                (tRow.GetCell(7) ?? tRow.CreateCell(7)).SetCellFormula($"SUM({hCells})");
            }
        }
        if (receivableRowIdx >= 0 && totalRowIdx >= 0)
        {
            var rRow = sheet.GetRow(receivableRowIdx) ?? sheet.CreateRow(receivableRowIdx);
            (rRow.GetCell(7) ?? rRow.CreateCell(7)).SetCellFormula($"G{totalRowIdx + 1}-H{totalRowIdx + 1}");
        }
    }

    private static string BackupFile(string masterPath)
    {
        string dir = Path.GetDirectoryName(masterPath) ?? Environment.CurrentDirectory;
        string name = Path.GetFileNameWithoutExtension(masterPath);
        string ext = Path.GetExtension(masterPath);
        string backupDir = Path.Combine(dir, "Backup");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir, $"{name}_backup_{DateTime.Now:yyyyMMddHHmmss}{ext}");
        File.Copy(masterPath, backupPath, overwrite: true);
        return backupPath;
    }

    private static string GetText(ICell? cell)
    {
        if (cell is null) return "";
        return cell.CellType switch
        {
            CellType.Numeric when DateUtil.IsCellDateFormatted(cell) => cell.DateCellValue?.ToString("yyyy/M/d") ?? "",
            CellType.Numeric => cell.NumericCellValue.ToString("0.################"),
            CellType.String => cell.StringCellValue,
            CellType.Formula => cell.CachedFormulaResultType == CellType.Numeric
                ? cell.NumericCellValue.ToString("0.################") : cell.ToString() ?? "",
            _ => cell.ToString() ?? ""
        };
    }
}