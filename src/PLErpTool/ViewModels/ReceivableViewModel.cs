using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLErpTool.Application.Receivable;
using PLErpTool.Application.Receivable.Commands;
using PLErpTool.Application.Receivable.Dtos;
using PLErpTool.Application.Receivable.Queries;
using PLErpTool.Application.Account;
using PLErpTool.Domain.Shared;
using PLErpTool.Infrastructure.Excel;
using PLErpTool.Services;
using PLErpTool.Views;

namespace PLErpTool.ViewModels;

public partial class PageNumberItem : ObservableObject
{
    public PageNumberItem(string label, bool isCurrent, ICommand command)
    {
        Label = label;
        Page = int.TryParse(label, out int page) ? page : 0;
        IsCurrent = isCurrent;
        Command = command;
    }

    public string Label { get; }
    public int Page { get; }
    public bool IsCurrent { get; }
    public ICommand Command { get; }
}

public partial class ReceivableViewModel : ObservableObject
{
    public const int PageSize = 50;

    private readonly ReceivableAppService _appService;
    private readonly ReceivableExcelExportService _exportService;
    private readonly ICustomerTreeChangedNotifier _treeNotifier;
    private readonly SearchHistoryStore _searchHistory;
    private CustomerTreeNodeDto? _selectedNode;
    private bool _suppressTreeSelection;

    [ObservableProperty] private ObservableCollection<CustomerTreeNodeDto> _customerTree = new();
    [ObservableProperty] private ObservableCollection<MonthlyDebtDto> _debts = new();
    [ObservableProperty] private ObservableCollection<PageNumberItem> _pageNumbers = new();
    [ObservableProperty] private ObservableCollection<string> _customerSuggestions = new();
    [ObservableProperty] private ObservableCollection<string> _salesmanSuggestions = new();
    [ObservableProperty] private ObservableCollection<string> _treeCustomerSuggestions = new();
    [ObservableProperty] private ObservableCollection<string> _treeSalesmanSuggestions = new();

    [ObservableProperty] private string _searchCustomer = "";
    [ObservableProperty] private string _searchSalesman = "";
    [ObservableProperty] private string _treeCustomerKeyword = "";
    [ObservableProperty] private string _treeSalesmanKeyword = "";
    // 账期默认留空（加载全部）
    [ObservableProperty] private DateTime? _startMonthDate;
    [ObservableProperty] private DateTime? _endMonthDate;
    [ObservableProperty] private bool _negativeBalanceOnly;

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _hasPrevPage;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private string _allMenuTag = "Active";
    [ObservableProperty] private decimal _totalDebtAmount;
    [ObservableProperty] private string _totalDebtTitle = "总欠款合计";

    public ReceivableViewModel(
        ReceivableAppService appService,
        ReceivableExcelExportService exportService,
        ICustomerTreeChangedNotifier treeNotifier,
        SearchHistoryStore searchHistory)
    {
        _appService = appService;
        _exportService = exportService;
        _treeNotifier = treeNotifier;
        _searchHistory = searchHistory;
        _treeNotifier.TreeChanged += OnCustomerTreeChanged;
    }

    private async void OnCustomerTreeChanged(object? sender, EventArgs e)
    {
        await LoadTreeAsync();
    }

    public void OnTreeNodeSelected(CustomerTreeNodeDto node)
    {
        if (_suppressTreeSelection)
        {
            _suppressTreeSelection = false;
            return;
        }
        _selectedNode = node;
        AllMenuTag = "";
        _suppressTreeSelection = true;
        foreach (var c in CustomerTree)
            c.IsSelected = false;
        foreach (var c in CustomerTree)
        foreach (var s in c.Children)
            s.IsSelected = false;
        node.IsSelected = true;
        _ = LoadTreeDataAsync(node);
    }

    public void OnLoaded()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadTreeAsync();
        // 进入工具默认加载全部数据，不按账期过滤
        await LoadAllAsync();
    }

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

    [RelayCommand]
    private async Task LoadTreeAsync()
    {
        var tree = await _appService.GetCustomerTreeAsync();
        CustomerTree = new ObservableCollection<CustomerTreeNodeDto>(tree);
        FilterTree();
        RefreshCustomerSuggestions(SearchCustomer);
        RefreshSalesmanSuggestions(SearchSalesman);
        RefreshTreeCustomerSuggestions(TreeCustomerKeyword);
        RefreshTreeSalesmanSuggestions(TreeSalesmanKeyword);
    }

    partial void OnSearchCustomerChanged(string value) => RefreshCustomerSuggestions(value);

    partial void OnSearchSalesmanChanged(string value) => RefreshSalesmanSuggestions(value);

    private void RefreshCustomerSuggestions(string keyword)
    {
        CustomerSuggestions = BuildSuggestions(
            _searchHistory.Get("customer").Concat(CustomerTree.Select(c => c.Name)), keyword);
    }

    private void RefreshSalesmanSuggestions(string keyword)
    {
        SalesmanSuggestions = BuildSuggestions(_searchHistory.Get("salesman")
            .Concat(CustomerTree.SelectMany(c => c.Children).Select(s => s.Name)), keyword);
    }

    private void RefreshTreeCustomerSuggestions(string keyword)
        => TreeCustomerSuggestions = BuildSuggestions(_searchHistory.Get("tree-customer")
            .Concat(CustomerTree.Select(c => c.Name)), keyword);

    private void RefreshTreeSalesmanSuggestions(string keyword)
        => TreeSalesmanSuggestions = BuildSuggestions(_searchHistory.Get("tree-salesman")
            .Concat(CustomerTree.SelectMany(c => c.Children).Select(s => s.Name)), keyword);

    private static ObservableCollection<string> BuildSuggestions(IEnumerable<string> source, string keyword)
    {
        string term = keyword?.Trim() ?? string.Empty;
        return new ObservableCollection<string>(source
            .Where(name => string.IsNullOrEmpty(term)
                || name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    partial void OnTreeCustomerKeywordChanged(string value)
    {
        RefreshTreeCustomerSuggestions(value);
        FilterTree();
    }

    partial void OnTreeSalesmanKeywordChanged(string value)
    {
        RefreshTreeSalesmanSuggestions(value);
        FilterTree();
    }

    public void RememberTreeSearch(bool isCustomer)
    {
        _searchHistory.Remember(isCustomer ? "tree-customer" : "tree-salesman",
            isCustomer ? TreeCustomerKeyword : TreeSalesmanKeyword);
        if (isCustomer) RefreshTreeCustomerSuggestions(TreeCustomerKeyword);
        else RefreshTreeSalesmanSuggestions(TreeSalesmanKeyword);
    }

    private void FilterTree()
    {
        if (CustomerTree.Count == 0) return;
        foreach (var c in CustomerTree)
        {
            bool custMatch = string.IsNullOrWhiteSpace(TreeCustomerKeyword)
                || c.Name.Contains(TreeCustomerKeyword.Trim(), StringComparison.OrdinalIgnoreCase);
            bool hasVisibleSp = false;
            foreach (var s in c.Children)
            {
                bool spMatch = string.IsNullOrWhiteSpace(TreeSalesmanKeyword)
                    || s.Name.Contains(TreeSalesmanKeyword.Trim(), StringComparison.OrdinalIgnoreCase);
                s.IsVisible = custMatch && spMatch;
                if (s.IsVisible) hasVisibleSp = true;
            }
            c.IsVisible = custMatch && (hasVisibleSp || string.IsNullOrWhiteSpace(TreeSalesmanKeyword));
        }
    }

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        _selectedNode = null;
        AllMenuTag = "Active";
        foreach (var c in CustomerTree)
            c.IsSelected = false;
        foreach (var c in CustomerTree)
        foreach (var s in c.Children)
            s.IsSelected = false;
        // "全部"：清空所有查询条件，显示全部数据，不受账期范围限制
        SearchCustomer = "";
        SearchSalesman = "";
        StartMonthDate = null;
        EndMonthDate = null;
        NegativeBalanceOnly = false;
        await ApplyQueryAsync(NewQuery(), 1);
    }

    private CustomerTreeNodeDto? FindNodeById(long id, bool isCompany)
    {
        if (isCompany)
            return CustomerTree.FirstOrDefault(c => c.Id == id && c.IsCompany);
        foreach (var c in CustomerTree)
        {
            var s = c.Children.FirstOrDefault(x => x.Id == id && !x.IsCompany);
            if (s is not null) return s;
        }
        return null;
    }

    private async Task LoadTreeDataAsync(CustomerTreeNodeDto node)
    {
        QueryReceivablesQuery query;
        if (node.IsCompany)
        {
            query = new QueryReceivablesQuery(null, null, null, null, false, 1, PageSize, CustomerId: node.Id);
        }
        else
        {
            var parent = CustomerTree.FirstOrDefault(c => c.Children.Contains(node));
            query = new QueryReceivablesQuery(null, null, null, null, false, 1, PageSize,
                CustomerId: parent?.Id, SalesmanId: node.Id);
        }
        await ApplyQueryAsync(query);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        MonthKey? start = null, end = null;
        if (StartMonthDate is { } sd && MonthKey.TryParse($"{sd:yyyy-MM}", out var s)) start = s;
        if (EndMonthDate is { } ed && MonthKey.TryParse($"{ed:yyyy-MM}", out var e)) end = e;

        var query = new QueryReceivablesQuery(
            string.IsNullOrWhiteSpace(SearchCustomer) ? null : SearchCustomer.Trim(),
            string.IsNullOrWhiteSpace(SearchSalesman) ? null : SearchSalesman.Trim(),
            start, end, NegativeBalanceOnly, 1, PageSize);
        _searchHistory.Remember("customer", SearchCustomer);
        _searchHistory.Remember("salesman", SearchSalesman);
        RefreshCustomerSuggestions(SearchCustomer);
        RefreshSalesmanSuggestions(SearchSalesman);
        await ApplyQueryAsync(query);
    }

    [RelayCommand]
    private async Task ResetSearchAsync()
    {
        // 重置条件：清空所有已填条件（含账期），恢复到初始空状态加载全部
        SearchCustomer = "";
        SearchSalesman = "";
        StartMonthDate = null;
        EndMonthDate = null;
        NegativeBalanceOnly = false;
        await ApplyQueryAsync(NewQuery(), 1);
    }

    [RelayCommand]
    private async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == CurrentPage) return;
        CurrentPage = page;
        await ApplyQueryAsync(CurrentQuery ?? NewQuery(), page);
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (CurrentPage > 1)
            await GoToPageAsync(CurrentPage - 1);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
            await GoToPageAsync(CurrentPage + 1);
    }

    private QueryReceivablesQuery? CurrentQuery { get; set; }

    private async Task ApplyQueryAsync(QueryReceivablesQuery query, int page = 1)
    {
        var queryWithPage = query with { Page = page, PageSize = PageSize };
        var resultTask = _appService.QueryReceivablesAsync(queryWithPage);
        var totalDebtTask = _appService.GetTotalDebtAsync(query);
        await Task.WhenAll(resultTask, totalDebtTask);
        var (items, total, totalPages) = await resultTask;
        Debts = new ObservableCollection<MonthlyDebtDto>(items);
        TotalDebtAmount = await totalDebtTask;
        TotalDebtTitle = GetTotalDebtTitle(query);
        TotalCount = total;
        TotalPages = Math.Max(1, totalPages);
        CurrentPage = page;
        CurrentQuery = query;
        BuildPageNumbers();
    }

    private string GetTotalDebtTitle(QueryReceivablesQuery query)
    {
        if (query.SalesmanId is { } salesmanId)
        {
            var customer = CustomerTree.FirstOrDefault(c => c.Children.Any(s => s.Id == salesmanId));
            var salesman = customer?.Children.FirstOrDefault(s => s.Id == salesmanId);
            return customer is not null && salesman is not null
                ? $"{customer.Name} / {salesman.Name} 总欠款合计"
                : "业务员总欠款合计";
        }
        if (query.CustomerId is { } customerId)
            return $"{CustomerTree.FirstOrDefault(c => c.Id == customerId)?.Name ?? "客户"} 总欠款合计";
        return string.IsNullOrWhiteSpace(query.CustomerName)
            && string.IsNullOrWhiteSpace(query.SalesmanName)
            && query.StartMonth is null
            && query.EndMonth is null
            && !query.NegativeBalanceOnly
                ? "总欠款合计"
                : "筛选结果总欠款合计";
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

    [RelayCommand]
    private void OpenNewMonth()
    {
        var dlg = new InputDialog("新增月份欠款", "选择月份", "欠款金额 (元)", "对账备注", "新增",
            customerOptions: CustomerTree, requireDebtType: true)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        if (dlg.SelectedCustomer is null || dlg.SelectedSalesman is null)
        {
            MessageBox.Show("请选择客户和业务员！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!MonthKey.TryParse(dlg.Month, out var month))
        {
            MessageBox.Show("月份格式不正确，示例：2026-08", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ = AddDebtAsync(dlg.SelectedSalesman, dlg.SelectedCustomer.Id, month, dlg.Amount, dlg.Remark, dlg.DebtType);
    }

    private async Task AddDebtAsync(CustomerTreeNodeDto node, long customerId, MonthKey month, decimal amount, string remark, string debtType)
    {
        var cmd = new RecordDebtCommand(customerId, node.IsCompany ? null : node.Id, month, amount, remark, debtType);
        try
        {
            await _appService.RecordDebtAsync(cmd);
            MessageBox.Show($"已成功为 {node.Name} {month} 新增欠款: {amount:N2} 元", "成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadTreeAsync();

            var refreshed = FindNodeById(node.Id, node.IsCompany);
            if (refreshed is not null)
            {
                OnTreeNodeSelected(refreshed);
            }
            else
            {
                _selectedNode = null;
                AllMenuTag = "Active";
                await ApplyQueryAsync(CurrentQuery ?? NewQuery(), CurrentPage);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"新增失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var query = (CurrentQuery ?? NewQuery()) with { Page = 1, PageSize = int.MaxValue };
        var all = await _appService.QueryReceivablesAsync(query);
        var path = await _exportService.ExportAsync(all.Items);
        MessageBox.Show($"导出成功：{path}", "导出", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RecordPaymentRow(MonthlyDebtDto debt)
    {
        var dlg = new InputDialog($"{debt.Month} 登记收款", "所属月份", "收款金额 (元)", "备注信息",
            "确定", presetMonth: debt.Month, monthReadOnly: true,
            summary: new InputDialogSummary(
                debt.CustomerName,
                debt.SalesmanName,
                debt.Month,
                debt.DebtAmount,
                debt.Balance))
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        _ = RegisterPaymentAsync(debt, dlg.Amount, dlg.Remark);
    }

    private async Task RegisterPaymentAsync(MonthlyDebtDto debt, decimal amount, string remark)
    {
        try
        {
            var (debtId, debtAmt, collected, newPayment) = await _appService.RegisterPaymentAsync(new RegisterPaymentCommand(debt.Id, amount, remark));
            // 局部更新：追加收款明细到父行子列，并更新父行汇总，不整体刷新页面
            var parent = Debts.FirstOrDefault(d => d.Id == debtId);
            if (parent is not null)
            {
                parent.AddPayment(newPayment);
                parent.DebtAmount = debtAmt;
                parent.CollectedAmount = collected;
            }
            MessageBox.Show($"已成功为 {debt.CustomerName} {debt.Month} 登记收款: {amount:N2} 元\n备注: {remark}",
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"登记收款失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RecordDebtRow(MonthlyDebtDto debt)
    {
        var dlg = new InputDialog($"{debt.Month} 登记欠款", "所属月份", "欠款金额 (元)", "对账备注",
            "确定", presetMonth: debt.Month, monthReadOnly: true, requireDebtType: true)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        _ = AddRowDebtAsync(debt, dlg.Amount, dlg.Remark, dlg.DebtType);
    }

    private async Task AddRowDebtAsync(MonthlyDebtDto debt, decimal amount, string remark, string debtType)
    {
        try
        {
            await _appService.RecordDebtAsync(new RecordDebtCommand(debt.CustomerId, debt.SalesmanId, MonthKey.Parse(debt.Month), amount, remark, debtType));
            MessageBox.Show($"已成功为 {debt.CustomerName} {debt.Month} 登记欠款: {amount:N2} 元\n对账备注: {remark}",
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            await ApplyQueryAsync(CurrentQuery ?? NewQuery(), CurrentPage);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"登记欠款失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 编辑明细（数量/单价失去焦点触发）：保存到库并局部更新父行汇总，不整体刷新页面
    [RelayCommand]
    private async Task UpdateLineItemAsync(PaymentDto payment)
    {
        if (payment is null) return;
        try
        {
            var (debtId, debt, collected, balance) = await _appService.UpdateLineItemAsync(payment.MonthlyDebtId, payment.Id, payment.Quantity, payment.UnitPrice);
            UpdateParentSummary(debtId, debt, collected);
            await RefreshTotalDebtAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新明细失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task UpdateReconciliationRemarkAsync(PaymentDto payment)
    {
        if (payment is null) return;
        try
        {
            await _appService.UpdateReconciliationRemarkAsync(
                payment.MonthlyDebtId, payment.Id, payment.ReconciliationRemark);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存对账备注失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 编辑收款金额（失去焦点触发）：保存到库并局部更新父行汇总，不整体刷新页面
    [RelayCommand]
    private async Task UpdateCollectionAmountAsync(PaymentDto payment)
    {
        if (payment is null) return;
        try
        {
            var (debtId, debt, collected, balance) = await _appService.UpdateCollectionAmountAsync(payment.MonthlyDebtId, payment.Id, payment.Amount);
            UpdateParentSummary(debtId, debt, collected);
            await RefreshTotalDebtAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新收款金额失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 局部更新父行汇总金额（避免整体查询刷新导致滚动位置丢失）
    private void UpdateParentSummary(long debtId, decimal debtAmount, decimal collectedAmount)
    {
        var parent = Debts.FirstOrDefault(d => d.Id == debtId);
        if (parent is null) return;
        parent.DebtAmount = debtAmount;
        parent.CollectedAmount = collectedAmount;
    }

    private async Task RefreshTotalDebtAsync()
    {
        TotalDebtAmount = await _appService.GetTotalDebtAsync(CurrentQuery ?? NewQuery());
    }

    // 删除单条明细：从父行移除并重算汇总，刷新展示
    [RelayCommand]
    private async Task DeleteSubItemAsync(PaymentDto payment)
    {
        if (payment is null) return;
        var confirm = MessageBox.Show($"确定删除该条明细吗？\n日期：{payment.TradeDate:yyyy-MM-dd}  单号：{payment.DocumentNo}\n金额：{payment.Amount:N2}",
            "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            var (debtId, debtAmt, collected) = await _appService.DeleteLineItemAsync(payment.MonthlyDebtId, payment.Id);
            // 局部更新：从父行子列移除该明细，并更新父行汇总，不整体刷新页面
            var parent = Debts.FirstOrDefault(d => d.Id == debtId);
            if (parent is not null)
            {
                parent.RemovePayment(payment);
                parent.DebtAmount = debtAmt;
                parent.CollectedAmount = collected;
            }
            await RefreshTotalDebtAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除明细失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private QueryReceivablesQuery NewQuery()
        => new(null, null, null, null, false, 1, PageSize);
}
