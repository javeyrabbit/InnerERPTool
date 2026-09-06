using System.Windows;
using System.Windows.Controls;

namespace PLErpTool.Views.Controls;

public partial class MonthPicker : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(MonthPicker),
            new FrameworkPropertyMetadata(null, OnSelectedDateChanged) { BindsTwoWayByDefault = true });

    private bool _isSynchronizing;

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public MonthPicker()
    {
        InitializeComponent();

        for (var year = DateTime.Today.Year - 10; year <= DateTime.Today.Year + 10; year++)
            YearCombo.Items.Add(year);
        for (var month = 1; month <= 12; month++)
            MonthCombo.Items.Add($"{month:D2}月");

        SynchronizeSelection(SelectedDate ?? DateTime.Today);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MonthPicker picker || picker._isSynchronizing || e.NewValue is not DateTime date)
            return;

        picker.SetMonth(date);
    }

    private void OnToggleClicked(object sender, RoutedEventArgs e)
    {
        MonthPopup.IsOpen = PickerToggle.IsChecked == true;
        if (MonthPopup.IsOpen)
            SynchronizeSelection(SelectedDate ?? DateTime.Today);
    }

    private void OnPopupClosed(object? sender, EventArgs e) => PickerToggle.IsChecked = false;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || YearCombo.SelectedItem is not int year || MonthCombo.SelectedIndex < 0)
            return;

        SetMonth(new DateTime(year, MonthCombo.SelectedIndex + 1, 1));
        if (sender == MonthCombo)
            MonthPopup.IsOpen = false;
    }

    private void SetMonth(DateTime date)
    {
        var month = new DateTime(date.Year, date.Month, 1);
        _isSynchronizing = true;
        try
        {
            SelectedDate = month;
            SynchronizeSelection(month);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void SynchronizeSelection(DateTime date)
    {
        _isSynchronizing = true;
        try
        {
            YearCombo.SelectedItem = date.Year;
            MonthCombo.SelectedIndex = date.Month - 1;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }
}
