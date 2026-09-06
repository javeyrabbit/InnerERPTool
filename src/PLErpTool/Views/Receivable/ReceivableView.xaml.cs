using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PLErpTool.Application.Receivable.Dtos;
using PLErpTool.ViewModels;

namespace PLErpTool.Views.Receivable;

public partial class ReceivableView : UserControl
{
    public ReceivableView()
    {
        InitializeComponent();
    }

    private void SearchComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is not TextBox textBox) return;

        textBox.Padding = new Thickness(0);
        textBox.TextAlignment = TextAlignment.Left;
        textBox.VerticalContentAlignment = VerticalAlignment.Center;
        if (comboBox.Name is "TreeCustomerSearchBox" or "TreeSalesmanSearchBox")
        {
            textBox.TextChanged -= TreeSearchTextBox_TextChanged;
            textBox.TextChanged += TreeSearchTextBox_TextChanged;
        }
    }

    private void SearchComboBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (e.Key is Key.Escape or Key.Enter or Key.Tab or Key.Up or Key.Down or Key.PageUp or Key.PageDown)
            return;

        comboBox.IsDropDownOpen = true;
    }

    private void TreeSearchComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not ReceivableViewModel vm || sender is not ComboBox comboBox) return;
        if (string.IsNullOrWhiteSpace(comboBox.Text))
        {
            comboBox.SelectedItem = null;
            comboBox.SelectedIndex = -1;
            comboBox.Text = string.Empty;
        }
        vm.RememberTreeSearch(comboBox.Name == "TreeCustomerSearchBox");
    }

    private void TreeSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !string.IsNullOrWhiteSpace(textBox.Text)) return;
        if (FindAncestor<ComboBox>(textBox) is not { } comboBox) return;

        comboBox.SelectedItem = null;
        comboBox.SelectedIndex = -1;
    }

    private void OnTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is ReceivableViewModel vm && e.NewValue is CustomerTreeNodeDto node)
            vm.OnTreeNodeSelected(node);
    }

    private void OnExpandToggleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        DependencyObject? d = fe;
        while (d is not null && d is not DataGridRow)
            d = VisualTreeHelper.GetParent(d);
        if (d is DataGridRow row)
            row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void CustomerTreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled)
        {
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            var parent = ((Control)sender).Parent as UIElement;
            parent?.RaiseEvent(eventArg);
        }
    }

    // 数量/单价/金额失去焦点触发：按类型调用对应的编辑命令
    private void SubItem_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PaymentDto payment) return;
        if (DataContext is not ReceivableViewModel vm) return;
        if (payment.Type == "欠款")
            vm.UpdateLineItemCommand.Execute(payment);
        else if (payment.Type == "收款")
            vm.UpdateCollectionAmountCommand.Execute(payment);
    }

    private void ReconciliationRemark_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PaymentDto payment }) return;
        if (DataContext is not ReceivableViewModel vm) return;
        vm.UpdateReconciliationRemarkCommand.Execute(payment);
    }

    private void DebtProductFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: string product } comboBox ||
            comboBox.DataContext is not MonthlyDebtDto debt)
            return;

        debt.DebtProductKeyword = product;
        comboBox.Dispatcher.BeginInvoke(() => comboBox.Text = product);
    }

    private void CustomerMaterialFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: string material } comboBox ||
            comboBox.DataContext is not MonthlyDebtDto debt)
            return;

        debt.CustomerMaterialKeyword = material;
        comboBox.Dispatcher.BeginInvoke(() => comboBox.Text = material);
    }

    private void DebtTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: MonthlyDebtDto debt } comboBox) return;
        debt.DebtTypeKeyword = comboBox.SelectedItem as string;
    }

    // 子表滚轮事件向上抛给外层 DataGrid，避免内部 ScrollViewer 吞噬导致卡死
    private void SubTable_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;

        var mainScrollViewer = GetMainGridScrollViewer();
        if (mainScrollViewer is null) return;

        e.Handled = true;
        var wheelLines = SystemParameters.WheelScrollLines;
        var pixelsPerNotch = wheelLines > 0 ? wheelLines * 36d : mainScrollViewer.ViewportHeight;
        var offset = mainScrollViewer.VerticalOffset - Math.Sign(e.Delta) * pixelsPerNotch;
        mainScrollViewer.ScrollToVerticalOffset(Math.Clamp(offset, 0, mainScrollViewer.ScrollableHeight));
    }

    private ScrollViewer? GetMainGridScrollViewer()
    {
        if (ReceivableDataGrid.Template.FindName("DG_ScrollViewer", ReceivableDataGrid) is ScrollViewer scrollViewer)
            return scrollViewer;

        return FindDescendant<ScrollViewer>(ReceivableDataGrid);
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        var current = element;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject? element) where T : DependencyObject
    {
        if (element is null) return null;

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T match) return match;

            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }

        return null;
    }
}
