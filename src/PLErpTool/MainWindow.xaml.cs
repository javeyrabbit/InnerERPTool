using System.Windows;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;

namespace PLErpTool;

public partial class MainWindow : FluentWindow
{
    private const double ExpandedNavigationWidth = 230;
    private const double CollapsedNavigationWidth = 64;
    private bool _isNavigationCollapsed;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.OnWindowLoaded();
        };
    }

    private void OnToggleNavigation(object sender, RoutedEventArgs e)
    {
        _isNavigationCollapsed = !_isNavigationCollapsed;
        NavigationColumn.Width = new GridLength(
            _isNavigationCollapsed ? CollapsedNavigationWidth : ExpandedNavigationWidth);

        var itemMargin = _isNavigationCollapsed
            ? new Thickness(8, 0, 8, 8)
            : new Thickness(14, 0, 14, 8);
        var itemPadding = _isNavigationCollapsed
            ? new Thickness(0)
            : new Thickness(12, 0, 12, 0);

        CollapseNavigationButton.Margin = _isNavigationCollapsed
            ? new Thickness(6, 0, 6, 10)
            : new Thickness(12, 0, 0, 10);
        ReceivableNavigationButton.Margin = itemMargin;
        AccountNavigationButton.Margin = itemMargin;
        PersonnelNavigationButton.Margin = itemMargin;
        ReceivableNavigationButton.Padding = itemPadding;
        AccountNavigationButton.Padding = itemPadding;
        PersonnelNavigationButton.Padding = itemPadding;
        ReceivableNavigationButton.HorizontalContentAlignment = _isNavigationCollapsed
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        AccountNavigationButton.HorizontalContentAlignment = _isNavigationCollapsed
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        PersonnelNavigationButton.HorizontalContentAlignment = _isNavigationCollapsed
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        ReceivableNavigationIcon.Margin = _isNavigationCollapsed
            ? new Thickness(0)
            : new Thickness(0, 0, 14, 0);
        AccountNavigationIcon.Margin = _isNavigationCollapsed
            ? new Thickness(0)
            : new Thickness(0, 0, 14, 0);
        PersonnelNavigationIcon.Margin = _isNavigationCollapsed
            ? new Thickness(0)
            : new Thickness(0, 0, 14, 0);

        if (!_isNavigationCollapsed)
        {
            ShowNavigationText(ReceivableNavigationText);
            ShowNavigationText(AccountNavigationText);
            ShowNavigationText(PersonnelNavigationText);
        }

        AnimateNavigationText(ReceivableNavigationText, _isNavigationCollapsed ? 0 : 1, _isNavigationCollapsed);
        AnimateNavigationText(AccountNavigationText, _isNavigationCollapsed ? 0 : 1, _isNavigationCollapsed);
        AnimateNavigationText(PersonnelNavigationText, _isNavigationCollapsed ? 0 : 1, _isNavigationCollapsed);
    }

    private static void ShowNavigationText(UIElement navigationText)
    {
        navigationText.BeginAnimation(OpacityProperty, null);
        navigationText.Opacity = 0;
        navigationText.Visibility = Visibility.Visible;
    }

    private static void AnimateNavigationText(UIElement navigationText, double targetOpacity, bool hideAfterAnimation)
    {
        navigationText.Visibility = Visibility.Visible;
        var textOpacity = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (hideAfterAnimation)
            textOpacity.Completed += (_, _) => navigationText.Visibility = Visibility.Collapsed;

        navigationText.BeginAnimation(OpacityProperty, textOpacity);
    }
}
