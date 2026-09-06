using System.Windows;
using PLErpTool.Application.Receivable.Dtos;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace PLErpTool.Views;

public enum PersonnelEditMode
{
    Customer,
    Salesman
}

public partial class PersonnelEditDialog : FluentWindow
{
    private readonly PersonnelEditMode _mode;

    public PersonnelEditDialog(PersonnelEditMode mode, IEnumerable<CustomerTreeNodeDto>? customers = null)
    {
        InitializeComponent();
        _mode = mode;

        var isCustomer = mode == PersonnelEditMode.Customer;
        Title = isCustomer ? "新增客户" : "新增业务员";
        TitleText.Text = Title;
        ConfirmButton.Content = isCustomer ? "新增" : "新增";
        CustomerFields.Visibility = isCustomer ? Visibility.Visible : Visibility.Collapsed;
        SalesmanFields.Visibility = isCustomer ? Visibility.Collapsed : Visibility.Visible;

        if (!isCustomer)
            CustomerSelector.ItemsSource = customers?.ToList() ?? [];
    }

    public string CustomerShortName { get; private set; } = "";
    public string CustomerFullName { get; private set; } = "";
    public CustomerTreeNodeDto? SelectedCustomer { get; private set; }
    public string SalesmanName { get; private set; } = "";

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (_mode == PersonnelEditMode.Customer)
        {
            CustomerShortName = CustomerShortNameInput.Text.Trim();
            CustomerFullName = CustomerFullNameInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(CustomerShortName))
            {
                MessageBox.Show(this, "请输入客户简称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            SelectedCustomer = CustomerSelector.SelectedItem as CustomerTreeNodeDto;
            SalesmanName = SalesmanNameInput.Text.Trim();
            if (SelectedCustomer is null)
            {
                MessageBox.Show(this, "请选择所属客户！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(SalesmanName))
            {
                MessageBox.Show(this, "请输入业务员姓名！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
