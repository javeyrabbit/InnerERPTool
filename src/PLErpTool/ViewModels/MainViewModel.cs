using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLErpTool.Views;

namespace PLErpTool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ReceivableViewModel _receivableVm;
    private readonly AccountViewModel _accountVm;
    private readonly PersonnelViewModel _personnelVm;

    [ObservableProperty] private ObservableObject? _currentView;
    [ObservableProperty] private string _receivableMenuTag = "Active";
    [ObservableProperty] private string _accountMenuTag = "";
    [ObservableProperty] private string _personnelMenuTag = "";

    public MainViewModel(ReceivableViewModel receivableVm, AccountViewModel accountVm, PersonnelViewModel personnelVm)
    {
        _receivableVm = receivableVm;
        _accountVm = accountVm;
        _personnelVm = personnelVm;
        _currentView = _receivableVm;
    }

    [RelayCommand]
    private void SwitchToReceivable()
    {
        CurrentView = _receivableVm;
        ReceivableMenuTag = "Active";
        AccountMenuTag = "";
        PersonnelMenuTag = "";
    }

    [RelayCommand]
    private void SwitchToAccount()
    {
        CurrentView = _accountVm;
        ReceivableMenuTag = "";
        AccountMenuTag = "Active";
        PersonnelMenuTag = "";
    }

    [RelayCommand]
    private void SwitchToPersonnel()
    {
        CurrentView = _personnelVm;
        ReceivableMenuTag = "";
        AccountMenuTag = "";
        PersonnelMenuTag = "Active";
        _personnelVm.OnLoaded();
    }

    public void OnWindowLoaded()
    {
        _receivableVm.OnLoaded();
        _accountVm.OnLoaded();
    }
}
