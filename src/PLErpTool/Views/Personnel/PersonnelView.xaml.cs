using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PLErpTool.ViewModels;

namespace PLErpTool.Views.Personnel;

public partial class PersonnelView : UserControl
{
    public PersonnelView()
    {
        InitializeComponent();
    }

    private void SearchComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is not TextBox textBox) return;

        textBox.Padding = new Thickness(0, 0, 0, 1);
        textBox.VerticalAlignment = VerticalAlignment.Stretch;
        textBox.VerticalContentAlignment = VerticalAlignment.Center;
    }

    private void SearchComboBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (e.Key is Key.Escape or Key.Enter or Key.Tab or Key.Up or Key.Down or Key.PageUp or Key.PageDown)
            return;

        comboBox.IsDropDownOpen = true;
    }

    private void SearchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string selectedText)
            return;

        if (DataContext is PersonnelViewModel viewModel)
        {
            if (comboBox.Name == "CustomerSearchBox")
                viewModel.SearchCustomer = selectedText;
            else if (comboBox.Name == "SalesmanSearchBox")
                viewModel.SearchSalesman = selectedText;
        }

        // WPF UI 的编辑型 ComboBox 会在 SelectionChanged 后重置内部 TextBox；
        // 在绑定与模板完成本轮更新后再回填，确保选项始终显示在输入框内。
        comboBox.Dispatcher.BeginInvoke(() =>
        {
            comboBox.Text = selectedText;
            comboBox.IsDropDownOpen = false;
        });
    }
}
