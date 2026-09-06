using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PLErpTool.Application.Account;
using PLErpTool.Application.Account.Dtos;

namespace PLErpTool.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    private const int PageSize = 50;

    private readonly AccountAppService _appService;
    private List<ImportRecordPreviewDto> _allPreview = new();

    [ObservableProperty] private ObservableCollection<ImportRecordPreviewDto> _previewRecords = new();
    [ObservableProperty] private ObservableCollection<PageNumberItem> _pageNumbers = new();
    [ObservableProperty] private string _terminalLog = "系统初始化完成。等待用户操作...\n";
    [ObservableProperty] private string _masterPath = "";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _searchCustomer = "";
    [ObservableProperty] private DateTime? _searchDate;
    [ObservableProperty] private string _searchDocumentNo = "";
    [ObservableProperty] private string _searchProduct = "";
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasPrevPage;
    [ObservableProperty] private bool _hasNextPage;

    public AccountViewModel(AccountAppService appService)
    {
        _appService = appService;
    }

    public void OnLoaded() => RefreshPreview();

    partial void OnTotalPagesChanged(int value)
    {
        HasPrevPage = value > 1;
        HasNextPage = CurrentPage < value;
    }

    partial void OnCurrentPageChanged(int value)
    {
        HasPrevPage = value > 1;
        HasNextPage = value < TotalPages;
    }

    private void Log(string msg)
    {
        TerminalLog += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
    }

    [RelayCommand]
    private async Task ImportSourceAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择要导入的 Excel 文件",
            Filter = "Excel 文件|*.xlsx;*.xls",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true)
        {
            Log("已取消导入。");
            return;
        }

        Log("正在连接文件流...准备导入源文件");
        var result = await _appService.ImportSourceFilesAsync(dlg.FileNames);
        foreach (var msg in result.Messages)
            Log(msg);
        RefreshPreview();
        Log($"当前预览数据共 {TotalCount} 条。");
    }

    [RelayCommand]
    private async Task ImportCustomerSalesmanAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择要导入的 Excel 文件（导入客户业务员关系）",
            Filter = "Excel 文件|*.xlsx;*.xls",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true)
        {
            Log("已取消导入客户业务员关系。");
            return;
        }

        Log("正在解析源文件并同步客户/业务员关系...");
        var result = await _appService.ImportCustomerSalesmanAsync(dlg.FileNames);
        foreach (var msg in result.Messages)
            Log(msg);
    }

    [RelayCommand]
    private void ImportMaster()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择主账 Excel 文件",
            Filter = "Excel 文件|*.xlsx;*.xls",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true)
        {
            Log("已取消导入主账。");
            return;
        }
        MasterPath = dlg.FileName;
        Log($"[主账] 已选择 {System.IO.Path.GetFileName(MasterPath)}");
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrWhiteSpace(MasterPath))
        {
            Log("[转换] 请先导入主账文件。");
            return;
        }

        Log("[转换] 开始执行（含客户/业务员主数据同步：无则新增，有则不新增）。");
        try
        {
            var result = await _appService.ConvertToMasterAsync(MasterPath);
            foreach (var msg in result.Messages)
                Log(msg);
        }
        catch (Exception ex)
        {
            Log($"[转换失败] {ex.Message}");
            var loserPath = await _appService.ExportFailedRecordsAsync();
            Log($"[失败数据] {loserPath}");
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _appService.ResetImportData();
        RefreshPreview();
        TotalCount = 0;
        Log("[重置] 已清空导入数据。");
    }

    [RelayCommand]
    private void SearchPreview()
    {
        CurrentPage = 1;
        ApplyPreviewFilter(1);
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages || page == CurrentPage) return;
        CurrentPage = page;
        ApplyPreviewFilter(page);
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (CurrentPage > 1)
            GoToPage(CurrentPage - 1);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
            GoToPage(CurrentPage + 1);
    }

    private void RefreshPreview()
    {
        _allPreview = _appService.GetPreviewRecords();
        ApplyPreviewFilter(1);
    }

    private void ApplyPreviewFilter(int page)
    {
        IEnumerable<ImportRecordPreviewDto> query = _allPreview;

        if (!string.IsNullOrWhiteSpace(SearchCustomer))
            query = query.Where(r => r.CustomerShortName.Contains(SearchCustomer.Trim()));
        if (SearchDate is { } date)
            query = query.Where(r => DateTime.TryParse(r.TradeDate, out var d) && d.Date == date.Date);
        if (!string.IsNullOrWhiteSpace(SearchDocumentNo))
            query = query.Where(r => r.DocumentNumber.Contains(SearchDocumentNo.Trim()));
        if (!string.IsNullOrWhiteSpace(SearchProduct))
            query = query.Where(r => r.ProductName.Contains(SearchProduct.Trim()));

        var filtered = query.ToList();
        TotalCount = filtered.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        CurrentPage = Math.Clamp(page, 1, TotalPages);

        PreviewRecords = new ObservableCollection<ImportRecordPreviewDto>(
            filtered.Skip((CurrentPage - 1) * PageSize).Take(PageSize));
        BuildPageNumbers();
    }

    private void BuildPageNumbers()
    {
        var pages = new List<PageNumberItem>();
        int start = Math.Max(1, CurrentPage - 2);
        int end = Math.Min(TotalPages, CurrentPage + 2);

        if (start > 1)
            pages.Add(new PageNumberItem("…", false, GoToPageCommand));
        for (int i = start; i <= end; i++)
            pages.Add(new PageNumberItem(i.ToString(), i == CurrentPage, GoToPageCommand));
        if (end < TotalPages)
            pages.Add(new PageNumberItem("…", false, GoToPageCommand));

        PageNumbers = new ObservableCollection<PageNumberItem>(pages);
    }
}
