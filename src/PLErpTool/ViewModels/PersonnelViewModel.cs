using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLErpTool.Application.Account;
using PLErpTool.Application.Receivable;
using PLErpTool.Application.Receivable.Dtos;
using PLErpTool.Views;

namespace PLErpTool.ViewModels;

public partial class PersonnelViewModel : ObservableObject
{
    private readonly ReceivableAppService _appService;
    private readonly ICustomerTreeChangedNotifier _treeNotifier;

    [ObservableProperty] private ObservableCollection<PersonnelInfoDto> _personnelRecords = new();
    [ObservableProperty] private string _searchCustomer = "";
    [ObservableProperty] private string _searchSalesman = "";
    private readonly List<string> _allCustomerSuggestions = new();
    private readonly List<string> _allSalesmanSuggestions = new();

    public ObservableCollection<string> CustomerSuggestions { get; } = new();
    public ObservableCollection<string> SalesmanSuggestions { get; } = new();

    public PersonnelViewModel(ReceivableAppService appService, ICustomerTreeChangedNotifier treeNotifier)
    {
        _appService = appService;
        _treeNotifier = treeNotifier;
    }

    public void OnLoaded() => _ = InitializeAsync();

    private async Task InitializeAsync()
    {
        await LoadSuggestionsAsync();
        await SearchAsync();
    }

    private async Task LoadSuggestionsAsync()
    {
        var records = await _appService.GetPersonnelAsync(null, null);

        _allCustomerSuggestions.Clear();
        _allCustomerSuggestions.AddRange(records
            .Select(record => record.CustomerShortName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name));

        _allSalesmanSuggestions.Clear();
        _allSalesmanSuggestions.AddRange(records
            .Select(record => record.SalesmanName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name));

        RefreshCustomerSuggestions();
        RefreshSalesmanSuggestions();
    }

    partial void OnSearchCustomerChanged(string value) => RefreshCustomerSuggestions();

    partial void OnSearchSalesmanChanged(string value) => RefreshSalesmanSuggestions();

    private void RefreshCustomerSuggestions()
        => RefreshSuggestions(CustomerSuggestions, _allCustomerSuggestions, SearchCustomer);

    private void RefreshSalesmanSuggestions()
        => RefreshSuggestions(SalesmanSuggestions, _allSalesmanSuggestions, SearchSalesman);

    private static void RefreshSuggestions(
        ObservableCollection<string> target,
        IEnumerable<string> source,
        string? keyword)
    {
        var term = keyword?.Trim() ?? string.Empty;
        var matches = source
            .Where(name => string.IsNullOrWhiteSpace(term)
                || name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        target.Clear();
        foreach (var match in matches)
            target.Add(match);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var records = await _appService.GetPersonnelAsync(SearchCustomer, SearchSalesman);
            PersonnelRecords = new ObservableCollection<PersonnelInfoDto>(records);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询人员信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ResetSearchAsync()
    {
        SearchCustomer = "";
        SearchSalesman = "";
        await SearchAsync();
    }

    [RelayCommand]
    private async Task AddCustomerAsync()
    {
        var dialog = new PersonnelEditDialog(PersonnelEditMode.Customer)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _appService.AddCustomerAsync(dialog.CustomerShortName, dialog.CustomerFullName);
            _treeNotifier.NotifyChanged();
            await LoadSuggestionsAsync();
            await SearchAsync();
            MessageBox.Show("客户已新增。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"新增客户失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task AddSalesmanAsync()
    {
        var customers = await _appService.GetCustomerTreeAsync();
        if (customers.Count == 0)
        {
            MessageBox.Show("请先新增客户，再新增业务员。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new PersonnelEditDialog(PersonnelEditMode.Salesman, customers)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.SelectedCustomer is null) return;

        try
        {
            await _appService.AddSalesmanAsync(dialog.SelectedCustomer.Id, dialog.SalesmanName);
            _treeNotifier.NotifyChanged();
            await LoadSuggestionsAsync();
            await SearchAsync();
            MessageBox.Show("业务员已新增。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"新增业务员失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
