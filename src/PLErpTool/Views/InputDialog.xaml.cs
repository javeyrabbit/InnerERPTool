using System;
using System.Windows;
using System.Windows.Controls;
using PLErpTool.Application.Receivable.Dtos;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace PLErpTool.Views;

public sealed record InputDialogSummary(
    string Company,
    string Salesman,
    string Month,
    decimal CurrentDebt,
    decimal Balance);

public partial class InputDialog : FluentWindow
{
    public InputDialog(string title, string monthLabel, string amountLabel, string remarkLabel,
        string confirmText, string? presetMonth = null, bool monthReadOnly = false,
        InputDialogSummary? summary = null,
        IEnumerable<CustomerTreeNodeDto>? customerOptions = null,
        bool requireDebtType = false)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MonthLabel.Text = monthLabel;
        AmountLabel.Text = amountLabel;
        RemarkLabel.Text = remarkLabel;
        ConfirmBtn.Content = confirmText;
        var selectedDate = TryParseMonth(presetMonth, out var parsed) ? parsed : DateTime.Today;
        for (int year = DateTime.Today.Year - 10; year <= DateTime.Today.Year + 10; year++)
            YearInput.Items.Add(year);
        for (int month = 1; month <= 12; month++)
            MonthSelector.Items.Add($"{month:D2} 月");

        YearInput.SelectedItem = selectedDate.Year;
        MonthSelector.SelectedIndex = selectedDate.Month - 1;

        if (monthReadOnly)
        {
            MonthPickerPanel.Visibility = Visibility.Collapsed;
            MonthInput.Visibility = Visibility.Visible;
            MonthInput.Text = selectedDate.ToString("yyyy-MM");
            MonthInput.IsReadOnly = true;
        }

        if (summary is not null)
        {
            SummaryPanel.Visibility = Visibility.Visible;
            SummaryCompanyText.Text = summary.Company;
            SummarySalesmanText.Text = string.IsNullOrWhiteSpace(summary.Salesman) ? "未设置" : summary.Salesman;
            SummaryMonthText.Text = summary.Month;
            SummaryDebtText.Text = $"{summary.CurrentDebt:N2} 元";
            SummaryBalanceText.Text = $"{summary.Balance:N2} 元";
            SummaryBalanceText.Foreground = summary.Balance < 0m
                ? System.Windows.Media.Brushes.Red
                : System.Windows.Media.Brushes.SeaGreen;
        }

        if (customerOptions is not null)
        {
            var customers = customerOptions.ToList();
            CustomerSalesmanPanel.Visibility = Visibility.Visible;
            CustomerSelector.ItemsSource = customers;
        }

        if (requireDebtType)
        {
            DebtTypePanel.Visibility = Visibility.Visible;
            DebtTypeSelector.ItemsSource = new[] { "其他欠款", "退货" };
        }
    }

    public string Month { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Remark { get; private set; } = "";
    public string DebtType { get; private set; } = "";
    public CustomerTreeNodeDto? SelectedCustomer { get; private set; }
    public CustomerTreeNodeDto? SelectedSalesman { get; private set; }

    private void OnCustomerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedCustomer = CustomerSelector.SelectedItem as CustomerTreeNodeDto;
        SelectedSalesman = null;
        SalesmanSelector.SelectedItem = null;
        SalesmanSelector.ItemsSource = SelectedCustomer?.Children;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (CustomerSalesmanPanel.Visibility == Visibility.Visible)
        {
            SelectedCustomer = CustomerSelector.SelectedItem as CustomerTreeNodeDto;
            SelectedSalesman = SalesmanSelector.SelectedItem as CustomerTreeNodeDto;
            if (SelectedCustomer is null || SelectedSalesman is null)
            {
                MessageBox.Show(this, "请选择客户和业务员！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        string month;
        if (MonthPickerPanel.Visibility == Visibility.Visible)
        {
            if (YearInput.SelectedItem is not int year || MonthSelector.SelectedIndex < 0)
            {
                MessageBox.Show(this, "请选择所属年份和月份！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            month = $"{year:D4}-{MonthSelector.SelectedIndex + 1:D2}";
        }
        else
        {
            month = MonthInput.Text.Trim();
        }

        if (string.IsNullOrWhiteSpace(month))
        {
            MessageBox.Show(this, "请输入所属月份！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!decimal.TryParse(AmountInput.Text.Trim(), out decimal amount) || amount <= 0m)
        {
            MessageBox.Show(this, "请输入有效的正数金额！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (DebtTypePanel.Visibility == Visibility.Visible && DebtTypeSelector.SelectedItem is not string debtType)
        {
            MessageBox.Show(this, "请选择欠款类型。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Month = month;
        Amount = amount;
        Remark = RemarkInput.Text.Trim();
        DebtType = DebtTypeSelector.SelectedItem as string ?? string.Empty;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private static bool TryParseMonth(string? text, out DateTime month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return DateTime.TryParseExact(text, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out month);
    }
}
